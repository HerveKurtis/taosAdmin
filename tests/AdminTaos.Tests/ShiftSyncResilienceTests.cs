using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>
/// La projection de l'état de service est une commodité pour le responsable. La timesheet est
/// la source de vérité. Un refus d'écriture sur la projection ne doit jamais empêcher quelqu'un
/// de démarrer, de mettre en pause ou de terminer son service.
/// </summary>
public class ShiftSyncResilienceTests : BunitContext
{
    /// <summary>Refuse toute écriture d'affectation, comme le ferait une règle Firestore trop serrée.</summary>
    sealed class RefusesAssignmentWrites : InMemoryDataService, IDataService
    {
        Task IDataService.UpdateAssignmentAsync(Assignment a)
            => throw new InvalidOperationException("Missing or insufficient permissions.");
    }

    [Fact]
    public async Task Un_refus_d_ecriture_ne_remonte_pas_en_exception()
    {
        var db = new RefusesAssignmentWrites();
        var a = (await db.GetAssignmentsForEventAsync("evt-gala")).First();

        var ex = await Record.ExceptionAsync(() =>
            ShiftSync.ApplyAsync(db, a, new Timesheet { AssignmentId = a.Id, StartedAt = DateTime.Now }));

        Assert.Null(ex);
    }

    [Fact]
    public async Task Le_demarrage_de_service_aboutit_malgre_un_refus_sur_la_projection()
    {
        var db = new RefusesAssignmentWrites();

        var me = (await db.GetAccountAsync(SeedData.EmpActiveServer))!;
        var fake = new FakeAuthClient();
        fake.PreRegister(me.Id, me.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(me.Email, "pwd");

        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(auth);

        // evt-today : Marc y est confirmé et peut démarrer le jour même.
        var cut = Render<AdminTaos.Pages.Employee.EHome>();
        var bouton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Commencer mon shift"));
        Assert.NotNull(bouton);

        bouton!.Click();

        // La timesheet, elle, est bien enregistrée : le service a réellement démarré.
        var asg = (await db.GetAssignmentsForAccountAsync(me.Id)).First(x => x.EventId == "evt-today");
        var ts = await db.GetTimesheetForAssignmentAsync(asg.Id);
        Assert.NotNull(ts);
        Assert.NotNull(ts!.StartedAt);
        Assert.Equal(TimesheetStatus.InProgress, ts.Status);
    }

    /// <summary>Compte les lectures pour vérifier ce que coûte un rafraîchissement.</summary>
    sealed class CountingReads : InMemoryDataService, IDataService
    {
        public int Profils { get; private set; }
        public int Roles { get; private set; }
        public int Affectations { get; private set; }

        Task<List<Profile>> IDataService.GetProfilesAsync()
        { Profils++; return base.GetProfilesAsync(); }
        Task<List<JobRole>> IDataService.GetJobRolesAsync()
        { Roles++; return base.GetJobRolesAsync(); }
        Task<List<Assignment>> IDataService.GetAssignmentsForEventAsync(string eventId)
        { Affectations++; return base.GetAssignmentsForEventAsync(eventId); }
    }

    [Fact]
    public async Task Un_rafraichissement_ne_relit_que_les_affectations()
    {
        var db = new CountingReads();
        var e = await db.CreateEventAsync(new ServiceEvent {
            Id = "evt-r", Name = "Service", Date = DateOnly.FromDateTime(DateTime.Today),
            MeetingTime = new TimeOnly(0, 0), ExpectedEndTime = new TimeOnly(23, 59),
            ResponsableAccountId = SeedData.EmpActiveServer });
        await db.CreateAssignmentAsync(new Assignment {
            Id = "a-r", EventId = e.Id, AccountId = SeedData.EmpActiveServer,
            JobRoleId = SeedData.RoleServer, Status = AssignmentStatus.Confirmed });

        var me = (await db.GetAccountAsync(SeedData.EmpActiveServer))!;
        var fake = new FakeAuthClient();
        fake.PreRegister(me.Id, me.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(me.Email, "pwd");

        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(auth);

        var cut = Render<AdminTaos.Pages.Shared.EventTeam>(p => p.Add(x => x.Id, "evt-r"));
        var profilsApresChargement = db.Profils;
        var rolesApresChargement = db.Roles;
        var affectationsApresChargement = db.Affectations;

        await cut.InvokeAsync(() => cut.Instance.RefreshAsync());

        Assert.Equal(profilsApresChargement, db.Profils);          // rien de relu
        Assert.Equal(rolesApresChargement, db.Roles);              // rien de relu
        Assert.Equal(affectationsApresChargement + 1, db.Affectations);
    }
}
