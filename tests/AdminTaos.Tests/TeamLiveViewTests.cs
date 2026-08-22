using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>Vue d'ensemble du soir : qui a démarré, qui est en pause, qui n'est pas venu.</summary>
public class TeamLiveViewTests : BunitContext
{
    /// <summary>Un event en cours quelle que soit l'heure du test, avec Marc et Sarah confirmés.</summary>
    async Task<InMemoryDataService> EventEnCours()
    {
        var db = new InMemoryDataService();

        var e = await db.CreateEventAsync(new ServiceEvent {
            Id = "evt-live", Name = "Service du soir", Venue = "Villa Empain",
            Date = DateOnly.FromDateTime(DateTime.Today),
            MeetingTime = new TimeOnly(0, 0), ExpectedEndTime = new TimeOnly(23, 59),
            ResponsableAccountId = SeedData.EmpActiveServer });

        await db.CreateAssignmentAsync(new Assignment {
            Id = "a-marc", EventId = e.Id, AccountId = SeedData.EmpActiveServer,
            JobRoleId = SeedData.RoleServer, Status = AssignmentStatus.Confirmed });
        await db.CreateAssignmentAsync(new Assignment {
            Id = "a-sarah", EventId = e.Id, AccountId = SeedData.EmpActiveHost,
            JobRoleId = SeedData.RoleHost, Status = AssignmentStatus.Confirmed });

        var me = (await db.GetAccountAsync(SeedData.EmpActiveServer))!;
        var fake = new FakeAuthClient();
        fake.PreRegister(me.Id, me.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(me.Email, "pwd");

        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(auth);
        return db;
    }

    IRenderedComponent<AdminTaos.Pages.Shared.EventTeam> Page() =>
        Render<AdminTaos.Pages.Shared.EventTeam>(p => p.Add(x => x.Id, "evt-live"));

    static async Task Etat(InMemoryDataService db, string asgId, ShiftState s)
    {
        var a = (await db.GetAssignmentsForEventAsync("evt-live")).Single(x => x.Id == asgId);
        a.Shift = s;
        a.ShiftSince = s == ShiftState.NotStarted ? null : DateTime.Now.AddHours(-1);
        if (s != ShiftState.NotStarted) a.Presence = PresenceStatus.Present;
        await db.UpdateAssignmentAsync(a);
    }

    [Fact]
    public async Task Au_depart_personne_n_a_commence()
    {
        await EventEnCours();
        var cut = Page();

        Assert.Contains("0 en service · 0 en pause · 2 pas commencé · 0 terminé",
            cut.Find(".shift-tally").TextContent);
        Assert.Equal(2, cut.FindAll("span.shift-state.soon").Count);
    }

    [Fact]
    public async Task Chaque_etat_a_sa_couleur()
    {
        var db = await EventEnCours();
        await Etat(db, "a-marc", ShiftState.InService);
        await Etat(db, "a-sarah", ShiftState.OnBreak);

        var cut = Page();

        Assert.Single(cut.FindAll("span.shift-state.live"));
        Assert.Single(cut.FindAll("span.shift-state.paused"));
        Assert.Contains("En service", cut.Markup);
        Assert.Contains("En pause", cut.Markup);
    }

    [Fact]
    public async Task Le_recapitulatif_suit_les_etats()
    {
        var db = await EventEnCours();
        await Etat(db, "a-marc", ShiftState.InService);
        await Etat(db, "a-sarah", ShiftState.Finished);

        Assert.Contains("1 en service · 0 en pause · 0 pas commencé · 1 terminé",
            Page().Find(".shift-tally").TextContent);
    }

    [Fact]
    public async Task L_heure_de_debut_et_la_duree_sont_affichees()
    {
        var db = await EventEnCours();
        await Etat(db, "a-marc", ShiftState.InService);

        var ligne = Page().Find($".team-member[data-account-id='{SeedData.EmpActiveServer}']");
        Assert.Contains("depuis", ligne.TextContent);
        Assert.Contains("1 h 00", ligne.TextContent);
    }

    [Fact]
    public async Task Un_service_termine_n_affiche_pas_de_duree_qui_court()
    {
        var db = await EventEnCours();
        await Etat(db, "a-marc", ShiftState.Finished);

        var ligne = Page().Find($".team-member[data-account-id='{SeedData.EmpActiveServer}']");
        Assert.Contains("Terminé", ligne.TextContent);
        Assert.DoesNotContain("(1 h 00)", ligne.TextContent);
    }

    [Fact]
    public async Task Qui_a_demarre_est_pointe_present_sans_geste_du_responsable()
    {
        var db = await EventEnCours();
        var a = (await db.GetAssignmentsForEventAsync("evt-live")).Single(x => x.Id == "a-marc");

        await ShiftSync.ApplyAsync(db, a, new Timesheet { AssignmentId = a.Id, StartedAt = DateTime.Now });

        var cut = Page();
        Assert.Contains("1 présent", cut.Find(".presence-count").TextContent);
        Assert.Single(cut.FindAll("span.shift-state.live"));
    }

    [Fact]
    public async Task Le_responsable_peut_toujours_donner_quelqu_un_absent()
    {
        var db = await EventEnCours();
        var cut = Page();

        cut.Find($".team-member[data-account-id='{SeedData.EmpActiveHost}'] button.presence-absent").Click();

        Assert.Equal(PresenceStatus.Absent,
            (await db.GetAssignmentsForEventAsync("evt-live")).Single(x => x.Id == "a-sarah").Presence);
        Assert.Contains("1 absent", cut.Find(".presence-count").TextContent);
    }

    [Fact]
    public async Task Un_event_termine_n_affiche_pas_le_recapitulatif_de_service()
    {
        var db = new InMemoryDataService();
        Services.AddSingleton<IDataService>(db);
        var me = (await db.GetAccountAsync(SeedData.MgrId))!;
        var fake = new FakeAuthClient();
        fake.PreRegister(me.Id, me.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(me.Email, "pwd");
        Services.AddSingleton(auth);

        var cut = Render<AdminTaos.Pages.Shared.EventTeam>(p => p.Add(x => x.Id, "evt-past"));

        Assert.Empty(cut.FindAll(".shift-tally"));
    }
}
