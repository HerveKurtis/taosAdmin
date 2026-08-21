using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

public class EventTeamTests : BunitContext
{
    /// <summary>Connecte le compte donné et désigne Marc responsable du gala, sauf indication contraire.</summary>
    async Task<InMemoryDataService> Setup(string signedInAs, string? responsable = SeedData.EmpActiveServer)
    {
        var db = new InMemoryDataService();

        var e = (await db.GetEventAsync("evt-gala"))!;
        e.ResponsableAccountId = responsable;
        await db.UpdateEventAsync(e);

        await db.CreateAssignmentAsync(new Assignment {
            Id = "asg-gala-host", EventId = "evt-gala", AccountId = SeedData.EmpActiveHost,
            JobRoleId = SeedData.RoleHost, Status = AssignmentStatus.Confirmed });

        var me = (await db.GetAccountAsync(signedInAs))!;
        var fake = new FakeAuthClient();
        fake.PreRegister(me.Id, me.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(me.Email, "pwd");

        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(auth);
        return db;
    }

    IRenderedComponent<AdminTaos.Pages.Shared.EventTeam> Page() =>
        Render<AdminTaos.Pages.Shared.EventTeam>(p => p.Add(x => x.Id, "evt-gala"));

    [Fact]
    public async Task The_designated_responsable_sees_the_team()
    {
        await Setup(SeedData.EmpActiveServer);
        var cut = Page();

        Assert.Empty(cut.FindAll(".team-denied"));
        Assert.Equal(2, cut.FindAll(".team-member").Count);
        Assert.Contains("Marc D.", cut.Markup);
        Assert.Contains("Sarah K.", cut.Markup);
    }

    [Fact]
    public async Task An_admin_may_supervise_the_page()
    {
        await Setup(SeedData.MgrId);
        Assert.Empty(Page().FindAll(".team-denied"));
    }

    [Fact]
    public async Task A_collaborateur_who_is_not_the_responsable_is_refused()
    {
        await Setup(SeedData.EmpActiveHost);
        var cut = Page();

        Assert.Single(cut.FindAll(".team-denied"));
        Assert.Empty(cut.FindAll(".team-member"));
    }

    [Fact]
    public async Task The_event_briefing_is_shown()
    {
        await Setup(SeedData.EmpActiveServer);
        var cut = Page();

        Assert.Contains("Hôtel Plaza", cut.Markup);
        Assert.Contains("Arriver 15 min avant", cut.Markup);
    }

    [Fact]
    public async Task A_phone_number_is_a_tel_link_and_its_absence_is_stated()
    {
        var db = await Setup(SeedData.EmpActiveServer);
        var sarah = (await db.GetProfilesAsync()).Single(p => p.Id == SeedData.EmpActiveHost);
        sarah.Phone = "0470 99 88 77";
        await db.UpsertProfileAsync(sarah);

        var cut = Page();

        var link = cut.Find($".team-member[data-account-id='{SeedData.EmpActiveHost}'] a.team-phone");
        Assert.Equal("tel:0470998877", link.GetAttribute("href"));

        var marc = cut.Find($".team-member[data-account-id='{SeedData.EmpActiveServer}']");
        Assert.Empty(marc.QuerySelectorAll("a.team-phone"));
        Assert.Contains("Téléphone non renseigné", marc.TextContent);
    }

    [Fact]
    public async Task Only_confirmed_assignments_appear()
    {
        var db = await Setup(SeedData.EmpActiveServer);
        await db.CreateAssignmentAsync(new Assignment {
            Id = "asg-gala-pending", EventId = "evt-gala", AccountId = SeedData.EmpPending,
            JobRoleId = SeedData.RoleServer, Status = AssignmentStatus.PendingApproval });

        Assert.Equal(2, Page().FindAll(".team-member").Count);
    }

    [Fact]
    public async Task Everyone_starts_as_expected()
    {
        await Setup(SeedData.EmpActiveServer);
        var cut = Page();
        Assert.Contains("0 présent · 0 absent · 2 attendus", cut.Find(".presence-count").TextContent);
    }

    [Fact]
    public async Task Marking_someone_present_persists_and_updates_the_count()
    {
        var db = await Setup(SeedData.EmpActiveServer);
        var cut = Page();

        cut.Find($".team-member[data-account-id='{SeedData.EmpActiveHost}'] button.presence-present").Click();

        var asg = (await db.GetAssignmentsForEventAsync("evt-gala"))
            .Single(a => a.AccountId == SeedData.EmpActiveHost);
        Assert.Equal(PresenceStatus.Present, asg.Presence);
        Assert.Contains("1 présent · 0 absent · 1 attendu", cut.Find(".presence-count").TextContent);
    }

    [Fact]
    public async Task Marking_someone_absent_then_present_again_switches_cleanly()
    {
        var db = await Setup(SeedData.EmpActiveServer);
        var cut = Page();
        var row = $".team-member[data-account-id='{SeedData.EmpActiveHost}']";

        cut.Find($"{row} button.presence-absent").Click();
        Assert.Equal(PresenceStatus.Absent, (await db.GetAssignmentsForEventAsync("evt-gala"))
            .Single(a => a.AccountId == SeedData.EmpActiveHost).Presence);

        cut.Find($"{row} button.presence-present").Click();
        Assert.Equal(PresenceStatus.Present, (await db.GetAssignmentsForEventAsync("evt-gala"))
            .Single(a => a.AccountId == SeedData.EmpActiveHost).Presence);
    }

    [Fact]
    public async Task A_refused_write_is_shown_instead_of_failing_silently()
    {
        // Un seul service pour l'authentification ET la page, sinon les deux voient des données
        // différentes. RefusingDataService reconstruit le seed puis désigne le responsable.
        var db = new RefusingDataService();

        var me = (await db.GetAccountAsync(SeedData.EmpActiveServer))!;
        var fake = new FakeAuthClient();
        fake.PreRegister(me.Id, me.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(me.Email, "pwd");

        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(auth);

        var cut = Page();
        cut.FindAll("button.presence-present").First().Click();

        Assert.Contains("Échec du pointage", cut.Markup);
    }

    [Fact]
    public async Task The_responsable_gets_a_banner_and_a_link_on_the_event_page()
    {
        await Setup(SeedData.EmpActiveServer);
        var cut = Render<AdminTaos.Pages.Employee.EEventDetail>(p => p.Add(x => x.Id, "evt-gala"));

        Assert.Contains("Tu es responsable du jour", cut.Markup);
        Assert.Contains("e/events/evt-gala/equipe", cut.Markup);
    }

    [Fact]
    public async Task A_plain_collaborateur_gets_no_banner()
    {
        await Setup(SeedData.EmpActiveHost);
        var cut = Render<AdminTaos.Pages.Employee.EEventDetail>(p => p.Add(x => x.Id, "evt-gala"));

        Assert.DoesNotContain("Tu es responsable du jour", cut.Markup);
        Assert.DoesNotContain("/equipe", cut.Markup);
    }

    [Fact]
    public async Task The_admin_event_page_links_to_the_team_page()
    {
        await Setup(SeedData.MgrId);
        var cut = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-gala"));

        Assert.Contains("m/events/evt-gala/equipe", cut.Markup);
    }

    [Fact]
    public async Task Each_member_shows_a_photo_or_their_initials()
    {
        var db = await Setup(SeedData.EmpActiveServer);
        var sarah = (await db.GetProfilesAsync()).Single(p => p.Id == SeedData.EmpActiveHost);
        sarah.PhotoUrl = "https://example.test/sarah.jpg";
        await db.UpsertProfileAsync(sarah);

        var cut = Page();

        var photo = cut.Find($".team-member[data-account-id='{SeedData.EmpActiveHost}'] img.team-avatar");
        Assert.Equal("https://example.test/sarah.jpg", photo.GetAttribute("src"));

        var initials = cut.Find($".team-member[data-account-id='{SeedData.EmpActiveServer}'] .team-initials");
        Assert.Equal("MD", initials.TextContent.Trim());
    }

    [Fact]
    public async Task The_role_group_states_the_expected_headcount()
    {
        await Setup(SeedData.EmpActiveServer);
        var cut = Page();

        // Le gala demande 4 Serveurs et 2 Hôtesses ; un de chaque est confirmé.
        Assert.Contains("Serveur · 1 / 4", cut.Markup);
        Assert.Contains("Hôtesse · 1 / 2", cut.Markup);
    }

    [Fact]
    public async Task A_role_without_a_declared_headcount_shows_no_target()
    {
        var db = await Setup(SeedData.EmpActiveServer);
        var e = (await db.GetEventAsync("evt-gala"))!;
        e.RoleNeeds.Clear();
        await db.UpdateEventAsync(e);

        var cut = Page();
        Assert.DoesNotContain(cut.FindAll(".lab"), l => l.TextContent.Contains(" / "));
    }

    [Fact]
    public async Task The_team_loads_without_ever_reading_the_accounts_collection()
    {
        // Reproduit le refus Firestore : un collaborateur n'a pas le droit de lire /accounts.
        // Si la page en dépend, elle casse en production pour tout responsable non-admin.
        var db = new AccountsLockedDataService();

        var me = (await db.GetAccountForSignInAsync(SeedData.EmpActiveServer))!;
        var fake = new FakeAuthClient();
        fake.PreRegister(me.Id, me.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(me.Email, "pwd");

        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(auth);

        var cut = Page();

        Assert.Empty(cut.FindAll(".team-denied"));
        Assert.Equal(2, cut.FindAll(".team-member").Count);
        Assert.Contains("Sarah K.", cut.Markup);
    }

    [Fact]
    public async Task A_member_without_a_shared_profile_is_still_listed()
    {
        var db = await Setup(SeedData.EmpActiveServer);
        await db.DeleteProfileAsync(SeedData.EmpActiveHost);

        var cut = Page();

        Assert.Equal(2, cut.FindAll(".team-member").Count);
        Assert.Contains("Profil incomplet", cut.Markup);
    }

    /// <summary>Refuse la lecture de /accounts comme le feraient les règles pour un collaborateur.</summary>
    sealed class AccountsLockedDataService : InMemoryDataService, IDataService
    {
        public AccountsLockedDataService()
        {
            var e = GetEventAsync("evt-gala").Result!;
            e.ResponsableAccountId = SeedData.EmpActiveServer;
            UpdateEventAsync(e).Wait();

            CreateAssignmentAsync(new Assignment {
                Id = "asg-gala-host", EventId = "evt-gala", AccountId = SeedData.EmpActiveHost,
                JobRoleId = SeedData.RoleHost, Status = AssignmentStatus.Confirmed }).Wait();
        }

        /// <summary>Accès direct réservé au test : la connexion lit bien son propre compte.</summary>
        public Task<Account?> GetAccountForSignInAsync(string id) => base.GetAccountAsync(id);

        Task<List<Account>> IDataService.GetAccountsAsync()
            => throw new InvalidOperationException("Missing or insufficient permissions.");
    }

    /// <summary>Lit normalement, refuse toute écriture d'assignation — tient lieu de refus des règles Firestore.</summary>
    sealed class RefusingDataService : InMemoryDataService, IDataService
    {
        public RefusingDataService()
        {
            // InMemoryDataService est synchrone sous le capot : ces Task sont déjà complétées.
            var e = GetEventAsync("evt-gala").Result!;
            e.ResponsableAccountId = SeedData.EmpActiveServer;
            UpdateEventAsync(e).Wait();

            CreateAssignmentAsync(new Assignment {
                Id = "asg-gala-host", EventId = "evt-gala", AccountId = SeedData.EmpActiveHost,
                JobRoleId = SeedData.RoleHost, Status = AssignmentStatus.Confirmed }).Wait();
        }

        Task IDataService.UpdateAssignmentAsync(Assignment a)
            => throw new InvalidOperationException("Missing or insufficient permissions.");
    }
}
