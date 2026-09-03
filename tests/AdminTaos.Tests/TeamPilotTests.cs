using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>
/// L'admin et le responsable pilotent les compteurs de l'équipe depuis le tableau : démarrer
/// pour quelqu'un qui a oublié, envoyer en pause, arrêter. Passé 48 h après la fin de l'event,
/// seul l'admin garde la main.
/// </summary>
public class TeamPilotTests : BunitContext
{
    InMemoryDataService _db = default!;

    /// <summary>Event toujours en cours, Marc assigné, connecté sous le compte demandé.</summary>
    async Task<ServiceEvent> Scene(string signedInAs, string? responsable = SeedData.EmpActiveHost,
                                   DateOnly? date = null, TimeOnly? debut = null, TimeOnly? fin = null)
    {
        _db = new InMemoryDataService();

        var e = await _db.CreateEventAsync(new ServiceEvent {
            Id = "evt-pilot", Name = "Réception", Venue = "Concert Noble",
            Date = date ?? DateOnly.FromDateTime(DateTime.Today),
            MeetingTime = debut ?? new TimeOnly(0, 0),
            ExpectedEndTime = fin ?? new TimeOnly(23, 59),
            ResponsableAccountId = responsable });

        await _db.CreateAssignmentAsync(new Assignment {
            Id = "a-pilot", EventId = e.Id, AccountId = SeedData.EmpActiveServer,
            JobRoleId = SeedData.RoleServer, Status = AssignmentStatus.Confirmed });

        var me = (await _db.GetAccountAsync(signedInAs))!;
        var fake = new FakeAuthClient();
        fake.PreRegister(me.Id, me.Email, "pwd");
        var auth = new AuthState(fake, _db);
        await auth.LoginAsync(me.Email, "pwd");

        Services.AddSingleton<IDataService>(_db);
        Services.AddSingleton(auth);
        return e;
    }

    IRenderedComponent<AdminTaos.Components.TeamBoard> Board(ServiceEvent e) =>
        Render<AdminTaos.Components.TeamBoard>(p => p.Add(x => x.Event, e));

    async Task<Assignment> Asg() => (await _db.GetAssignmentsForEventAsync("evt-pilot")).Single();
    async Task<Timesheet?> Ts() => await _db.GetTimesheetForAssignmentAsync("a-pilot");

    // ---------- qui voit les commandes ----------

    [Fact]
    public async Task L_admin_voit_les_commandes()
    {
        var e = await Scene(SeedData.MgrId);
        Assert.NotEmpty(Board(e).FindAll("button.pilot-start"));
    }

    [Fact]
    public async Task Le_responsable_voit_les_commandes()
    {
        var e = await Scene(SeedData.EmpActiveHost);
        Assert.NotEmpty(Board(e).FindAll("button.pilot-start"));
    }

    [Fact]
    public async Task Un_collaborateur_quelconque_ne_voit_aucune_commande()
    {
        var e = await Scene(SeedData.EmpActiveServer, responsable: SeedData.EmpActiveHost);
        var cut = Board(e);
        Assert.Empty(cut.FindAll("button.pilot-start"));
        Assert.Empty(cut.FindAll("button.pilot-pause"));
        Assert.Empty(cut.FindAll("button.pilot-finish"));
    }

    [Fact]
    public async Task Passe_quarante_huit_heures_le_responsable_perd_la_main_et_on_le_lui_dit()
    {
        var hier = DateOnly.FromDateTime(DateTime.Today.AddDays(-3));
        var e = await Scene(SeedData.EmpActiveHost, date: hier,
                            debut: new TimeOnly(18, 0), fin: new TimeOnly(23, 0));

        var cut = Board(e);

        Assert.Empty(cut.FindAll("button.pilot-start"));
        Assert.Contains("Passé 48 h", cut.Markup);
    }

    [Fact]
    public async Task Passe_quarante_huit_heures_l_admin_garde_la_main()
    {
        var vieux = DateOnly.FromDateTime(DateTime.Today.AddDays(-3));
        var e = await Scene(SeedData.MgrId, date: vieux,
                            debut: new TimeOnly(18, 0), fin: new TimeOnly(23, 0));

        var cut = Board(e);

        Assert.NotEmpty(cut.FindAll("button.pilot-start"));
        Assert.DoesNotContain("Passé 48 h", cut.Markup);
    }

    // ---------- démarrer ----------

    [Fact]
    public async Task Demarrer_pour_quelqu_un_lance_son_compteur_a_l_heure_proposee()
    {
        var e = await Scene(SeedData.EmpActiveHost);
        var cut = Board(e);

        cut.Find("button.pilot-start").Click();          // ouvre la saisie d'heure
        cut.Find("button.pilot-start-confirm").Click();  // valide l'heure proposée

        var ts = await Ts();
        Assert.NotNull(ts);
        Assert.NotNull(ts!.StartedAt);
        Assert.Equal(ShiftState.InService, (await Asg()).Shift);
    }

    [Fact]
    public async Task L_heure_de_demarrage_est_modifiable()
    {
        var e = await Scene(SeedData.EmpActiveHost);
        var cut = Board(e);

        cut.Find("button.pilot-start").Click();
        cut.Find("input.pilot-start-time").Change("17:05");
        cut.Find("button.pilot-start-confirm").Click();

        var ts = await Ts();
        Assert.Equal(17, ts!.StartedAt!.Value.Hour);
        Assert.Equal(5, ts.StartedAt.Value.Minute);
    }

    [Fact]
    public async Task Demarrer_pour_quelqu_un_garde_trace_de_l_auteur()
    {
        var e = await Scene(SeedData.EmpActiveHost);
        var cut = Board(e);

        cut.Find("button.pilot-start").Click();
        cut.Find("button.pilot-start-confirm").Click();

        Assert.Equal(SeedData.EmpActiveHost, (await Ts())!.StartedByAccountId);
    }

    [Fact]
    public async Task On_ne_demarre_pas_quelqu_un_donne_absent()
    {
        var e = await Scene(SeedData.EmpActiveHost);
        var a = await Asg();
        a.Presence = PresenceStatus.Absent;
        await _db.UpdateAssignmentAsync(a);

        var cut = Board(e);

        Assert.Empty(cut.FindAll("button.pilot-start"));
        Assert.Contains("Marqué absent", cut.Markup);
    }

    // ---------- pause, reprise, arrêt ----------

    [Fact]
    public async Task Envoyer_en_pause_puis_faire_reprendre()
    {
        var e = await Scene(SeedData.EmpActiveHost);
        await ShiftOps.StartAsync(_db, await Asg(), DateTime.Now.AddHours(-2), by: null);
        var cut = Board(e);

        cut.Find("button.pilot-pause").Click();
        Assert.Equal(ShiftState.OnBreak, (await Asg()).Shift);
        Assert.Equal(SeedData.EmpActiveHost, (await Ts())!.OpenBreak!.StartedByAccountId);

        cut.Find("button.pilot-resume").Click();
        Assert.Equal(ShiftState.InService, (await Asg()).Shift);
    }

    [Fact]
    public async Task Arreter_le_compteur_de_quelqu_un()
    {
        var e = await Scene(SeedData.EmpActiveHost);
        await ShiftOps.StartAsync(_db, await Asg(), DateTime.Now.AddHours(-3), by: null);
        var cut = Board(e);

        cut.Find("button.pilot-finish").Click();

        var ts = await Ts();
        Assert.NotNull(ts!.EndedAt);
        Assert.Equal(SeedData.EmpActiveHost, ts.EndedByAccountId);
        Assert.Equal(ShiftState.Finished, (await Asg()).Shift);
    }

    [Fact]
    public async Task Les_commandes_suivent_l_etat_de_la_personne()
    {
        var e = await Scene(SeedData.EmpActiveHost);

        // pas commencé : démarrer seulement
        var cut = Board(e);
        Assert.NotEmpty(cut.FindAll("button.pilot-start"));
        Assert.Empty(cut.FindAll("button.pilot-pause"));
        Assert.Empty(cut.FindAll("button.pilot-finish"));

        // en service : pause et arrêt, plus de démarrage
        await ShiftOps.StartAsync(_db, await Asg(), DateTime.Now.AddHours(-1), by: null);
        cut = Board(e);
        Assert.Empty(cut.FindAll("button.pilot-start"));
        Assert.NotEmpty(cut.FindAll("button.pilot-pause"));
        Assert.NotEmpty(cut.FindAll("button.pilot-finish"));

        // terminé : plus aucune commande
        await ShiftOps.FinishAsync(_db, await Asg(), (await Ts())!, by: null);
        cut = Board(e);
        Assert.Empty(cut.FindAll("button.pilot-start"));
        Assert.Empty(cut.FindAll("button.pilot-pause"));
        Assert.Empty(cut.FindAll("button.pilot-finish"));
    }

    [Fact]
    public async Task Un_echec_d_ecriture_est_affiche_et_non_avale()
    {
        var e = await Scene(SeedData.EmpActiveHost);
        Services.AddSingleton<IDataService>(new RefuseTimesheetWrites());

        var cut = Render<AdminTaos.Components.TeamBoard>(p => p.Add(x => x.Event, e));
        cut.Find("button.pilot-start").Click();
        cut.Find("button.pilot-start-confirm").Click();

        Assert.Contains("Échec", cut.Markup);
    }

    sealed class RefuseTimesheetWrites : InMemoryDataService, IDataService
    {
        public RefuseTimesheetWrites()
        {
            CreateEventAsync(new ServiceEvent {
                Id = "evt-pilot", Name = "Réception",
                Date = DateOnly.FromDateTime(DateTime.Today),
                MeetingTime = new TimeOnly(0, 0), ExpectedEndTime = new TimeOnly(23, 59),
                ResponsableAccountId = SeedData.EmpActiveHost }).Wait();
            CreateAssignmentAsync(new Assignment {
                Id = "a-pilot", EventId = "evt-pilot", AccountId = SeedData.EmpActiveServer,
                JobRoleId = SeedData.RoleServer, Status = AssignmentStatus.Confirmed }).Wait();
        }

        Task<Timesheet> IDataService.CreateTimesheetAsync(Timesheet t)
            => throw new InvalidOperationException("Missing or insufficient permissions.");
    }
}
