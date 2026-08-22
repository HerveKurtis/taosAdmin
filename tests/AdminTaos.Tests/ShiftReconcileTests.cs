using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>
/// Les services déjà démarrés avant l'arrivée de la projection s'affichaient « Pas commencé ».
/// Le rattrapage ne se déclenchait qu'à la réouverture de l'écran de shift — donc jamais pour
/// quelqu'un déjà en plein service. Il doit se faire partout où la timesheet est lisible.
/// </summary>
public class ShiftReconcileTests : BunitContext
{
    async Task<InMemoryDataService> ServiceDejaDemarre()
    {
        var db = new InMemoryDataService();

        var e = await db.CreateEventAsync(new ServiceEvent {
            Id = "evt-rec", Name = "Réception", Venue = "Concert Noble",
            Date = DateOnly.FromDateTime(DateTime.Today),
            MeetingTime = new TimeOnly(0, 0), ExpectedEndTime = new TimeOnly(23, 59),
            ResponsableAccountId = SeedData.EmpActiveHost });

        // Affectation « ancienne » : la timesheet tourne, la projection est restée vierge.
        var a = await db.CreateAssignmentAsync(new Assignment {
            Id = "a-rec", EventId = e.Id, AccountId = SeedData.EmpActiveServer,
            JobRoleId = SeedData.RoleServer, Status = AssignmentStatus.Confirmed });
        await db.CreateTimesheetAsync(new Timesheet {
            Id = "ts-rec", AssignmentId = a.Id,
            StartedAt = DateTime.Now.AddHours(-2), Status = TimesheetStatus.InProgress });

        return db;
    }

    async Task Connecte(InMemoryDataService db, string uid)
    {
        var me = (await db.GetAccountAsync(uid))!;
        var fake = new FakeAuthClient();
        fake.PreRegister(me.Id, me.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(me.Email, "pwd");
        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(auth);
    }

    [Fact]
    public async Task L_admin_qui_ouvre_la_page_equipe_repare_les_etats_perimes()
    {
        var db = await ServiceDejaDemarre();
        await Connecte(db, SeedData.MgrId);

        var cut = Render<AdminTaos.Pages.Shared.EventTeam>(p => p.Add(x => x.Id, "evt-rec"));

        Assert.Contains("En service", cut.Markup);
        Assert.Single(cut.FindAll("span.shift-state.live"));
        Assert.Contains("1 en service", cut.Find(".shift-tally").TextContent);

        // La réparation est écrite : le responsable, qui ne lit pas les timesheets, en profite.
        var a = (await db.GetAssignmentsForEventAsync("evt-rec")).Single();
        Assert.Equal(ShiftState.InService, a.Shift);
        Assert.Equal(PresenceStatus.Present, a.Presence);
    }

    [Fact]
    public async Task Le_collaborateur_qui_ouvre_son_accueil_repare_sa_propre_ligne()
    {
        var db = await ServiceDejaDemarre();
        await Connecte(db, SeedData.EmpActiveServer);

        Render<AdminTaos.Pages.Employee.EHome>();

        var a = (await db.GetAssignmentsForEventAsync("evt-rec")).Single();
        Assert.Equal(ShiftState.InService, a.Shift);
    }

    [Fact]
    public async Task Le_collaborateur_qui_ouvre_la_fiche_de_l_event_repare_sa_propre_ligne()
    {
        var db = await ServiceDejaDemarre();
        await Connecte(db, SeedData.EmpActiveServer);

        Render<AdminTaos.Pages.Employee.EEventDetail>(p => p.Add(x => x.Id, "evt-rec"));

        var a = (await db.GetAssignmentsForEventAsync("evt-rec")).Single();
        Assert.Equal(ShiftState.InService, a.Shift);
    }

    [Fact]
    public async Task Une_pause_en_cours_est_reparee_elle_aussi()
    {
        var db = await ServiceDejaDemarre();
        var ts = (await db.GetTimesheetAsync("ts-rec"))!;
        ts.Breaks.Add(new ShiftBreak { StartedAt = DateTime.Now.AddMinutes(-20) });
        await db.UpdateTimesheetAsync(ts);
        await Connecte(db, SeedData.MgrId);

        var cut = Render<AdminTaos.Pages.Shared.EventTeam>(p => p.Add(x => x.Id, "evt-rec"));

        Assert.Single(cut.FindAll("span.shift-state.paused"));
        Assert.Contains("1 en pause", cut.Find(".shift-tally").TextContent);
    }

    [Fact]
    public async Task Le_responsable_non_admin_ne_tente_pas_de_lire_les_timesheets()
    {
        var db = new RefuseTimesheets();
        await Connecte(db, SeedData.EmpActiveHost);   // Sarah, responsable, pas admin

        var cut = Render<AdminTaos.Pages.Shared.EventTeam>(p => p.Add(x => x.Id, "evt-rec"));

        Assert.Empty(cut.FindAll(".team-denied"));
        Assert.Single(cut.FindAll(".team-member"));
    }

    /// <summary>Refuse la lecture des timesheets, comme la règle le fait pour un non-admin.</summary>
    sealed class RefuseTimesheets : InMemoryDataService, IDataService
    {
        public RefuseTimesheets()
        {
            var e = CreateEventAsync(new ServiceEvent {
                Id = "evt-rec", Name = "Réception",
                Date = DateOnly.FromDateTime(DateTime.Today),
                MeetingTime = new TimeOnly(0, 0), ExpectedEndTime = new TimeOnly(23, 59),
                ResponsableAccountId = SeedData.EmpActiveHost }).Result;
            CreateAssignmentAsync(new Assignment {
                Id = "a-rec", EventId = e.Id, AccountId = SeedData.EmpActiveServer,
                JobRoleId = SeedData.RoleServer, Status = AssignmentStatus.Confirmed }).Wait();
        }

        Task<List<Timesheet>> IDataService.GetTimesheetsAsync()
            => throw new InvalidOperationException("Missing or insufficient permissions.");
    }
}
