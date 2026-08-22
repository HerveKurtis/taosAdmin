using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>
/// L'action mise en avant sur la fiche d'un event suit son état : on prépare avant, on pilote
/// pendant. Les deux restent atteignables — il arrive de compléter une équipe en plein service.
/// </summary>
public class EventActionsTests : BunitContext
{
    async Task<InMemoryDataService> Db()
    {
        var db = new InMemoryDataService();
        await db.CreateEventAsync(new ServiceEvent {
            Id = "evt-live2", Name = "En cours", Date = DateOnly.FromDateTime(DateTime.Today),
            MeetingTime = new TimeOnly(0, 0), ExpectedEndTime = new TimeOnly(23, 59) });
        await db.CreateEventAsync(new ServiceEvent {
            Id = "evt-soon2", Name = "À venir", Date = DateOnly.FromDateTime(DateTime.Today.AddDays(4)),
            MeetingTime = new TimeOnly(18, 0), ExpectedEndTime = new TimeOnly(23, 0) });
        // MEventDetail affiche le tableau d'équipe en ligne pendant un service : il lui faut
        // une session, comme en production.
        var me = (await db.GetAccountAsync(SeedData.MgrId))!;
        var fake = new FakeAuthClient();
        fake.PreRegister(me.Id, me.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(me.Email, "pwd");

        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(auth);
        return db;
    }

    static string ClasseDu(IRenderedComponent<AdminTaos.Pages.Manager.MEventDetail> cut, string texte)
        => cut.FindAll("a.btn").Single(a => a.TextContent.Contains(texte)).ClassName ?? "";

    [Fact]
    public async Task Avant_l_event_c_est_l_assignation_qui_est_mise_en_avant()
    {
        await Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-soon2"));

        Assert.Contains("primary", ClasseDu(cut, "Assigner du personnel"));
        Assert.Contains("ghost", ClasseDu(cut, "Équipe du jour"));
    }

    [Fact]
    public async Task Pendant_l_event_c_est_l_equipe_du_jour_qui_est_mise_en_avant()
    {
        await Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-live2"));

        Assert.Contains("primary", ClasseDu(cut, "Équipe du jour"));
        Assert.Contains("ghost", ClasseDu(cut, "Assigner du personnel"));
    }

    [Fact]
    public async Task Les_deux_actions_restent_atteignables_dans_les_deux_cas()
    {
        await Db();

        var enCours = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-live2"));
        Assert.Contains("m/events/evt-live2/assign", enCours.Markup);
        Assert.Contains("m/events/evt-live2/equipe", enCours.Markup);

        var aVenir = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-soon2"));
        Assert.Contains("m/events/evt-soon2/assign", aVenir.Markup);
        Assert.Contains("m/events/evt-soon2/equipe", aVenir.Markup);
    }

    [Fact]
    public async Task Un_event_en_cours_affiche_l_equipe_sans_clic_supplementaire()
    {
        var db = await Db();
        await db.CreateAssignmentAsync(new Assignment {
            Id = "a-live2", EventId = "evt-live2", AccountId = SeedData.EmpActiveServer,
            JobRoleId = SeedData.RoleServer, Status = AssignmentStatus.Confirmed });

        var cut = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-live2"));

        Assert.Contains("Équipe du jour", cut.Markup);
        Assert.Single(cut.FindAll(".team-member"));
        Assert.NotEmpty(cut.FindAll(".shift-tally"));
        Assert.NotEmpty(cut.FindAll("button.presence-present"));
    }

    [Fact]
    public async Task Un_event_a_venir_n_affiche_pas_le_tableau_d_equipe()
    {
        await Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-soon2"));

        Assert.Empty(cut.FindAll(".shift-tally"));
        Assert.Empty(cut.FindAll("button.presence-present"));
    }

    [Fact]
    public async Task Un_event_termine_ne_met_rien_en_avant()
    {
        await Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-past"));

        Assert.DoesNotContain("primary", ClasseDu(cut, "Assigner du personnel"));
        Assert.DoesNotContain("primary", ClasseDu(cut, "Équipe du jour"));
    }

    [Fact]
    public async Task Pendant_le_service_la_liste_n_est_affichee_qu_une_fois()
    {
        var db = await Db();
        await db.CreateAssignmentAsync(new Assignment {
            Id = "a-dup", EventId = "evt-live2", AccountId = SeedData.EmpActiveServer,
            JobRoleId = SeedData.RoleServer, Status = AssignmentStatus.Confirmed });

        var cut = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-live2"));

        Assert.DoesNotContain("Personnel assigné", cut.Markup);
        Assert.Single(cut.FindAll(".presence-count"));      // un seul décompte de présences
        Assert.Empty(cut.FindAll(".presence-tally"));       // l'ancien panneau a cédé la place
        Assert.Single(cut.FindAll(".team-member"));
    }

    [Fact]
    public async Task Le_retrait_reste_possible_pendant_le_service()
    {
        var db = await Db();
        await db.CreateAssignmentAsync(new Assignment {
            Id = "a-rm", EventId = "evt-live2", AccountId = SeedData.EmpActiveServer,
            JobRoleId = SeedData.RoleServer, Status = AssignmentStatus.Confirmed });

        var cut = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-live2"));

        cut.Find("button.asg-remove").Click();
        cut.Find("button.asg-remove-confirm").Click();

        Assert.Empty(await db.GetAssignmentsForEventAsync("evt-live2"));
    }

    [Fact]
    public async Task Le_responsable_ne_recoit_aucun_bouton_de_retrait()
    {
        var db = await Db();
        var e = (await db.GetEventAsync("evt-live2"))!;
        e.ResponsableAccountId = SeedData.EmpActiveHost;
        await db.UpdateEventAsync(e);
        await db.CreateAssignmentAsync(new Assignment {
            Id = "a-resp", EventId = "evt-live2", AccountId = SeedData.EmpActiveServer,
            JobRoleId = SeedData.RoleServer, Status = AssignmentStatus.Confirmed });

        var sarah = (await db.GetAccountAsync(SeedData.EmpActiveHost))!;
        var fake = new FakeAuthClient();
        fake.PreRegister(sarah.Id, sarah.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(sarah.Email, "pwd");
        Services.AddSingleton(auth);

        var cut = Render<AdminTaos.Pages.Shared.EventTeam>(p => p.Add(x => x.Id, "evt-live2"));

        Assert.Single(cut.FindAll(".team-member"));
        Assert.Empty(cut.FindAll("button.asg-remove"));
    }
}
