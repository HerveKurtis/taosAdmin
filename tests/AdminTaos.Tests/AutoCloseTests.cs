using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>
/// Un service oublié ouvert se referme huit heures après la fin de l'event, à l'heure de fin
/// de l'event. L'application n'a aucun serveur qui tourne : la fermeture s'applique dès que
/// quelqu'un de qualifié consulte — l'admin sur sa liste, ou la personne sur son accueil.
/// </summary>
public class AutoCloseTests : BunitContext
{
    static ServiceEvent Fini(int ilYAHeures) => new() {
        Id = "evt-auto", Name = "Réception",
        Date = DateOnly.FromDateTime(DateTime.Now.AddHours(-ilYAHeures - 5)),
        MeetingTime = TimeOnly.FromDateTime(DateTime.Now.AddHours(-ilYAHeures - 5)),
        ExpectedEndTime = TimeOnly.FromDateTime(DateTime.Now.AddHours(-ilYAHeures)) };

    // ---------- la règle ----------

    [Fact]
    public void Avant_huit_heures_on_ne_ferme_rien()
    {
        var e = Fini(7);
        var ts = new Timesheet { StartedAt = e.EndInstant().AddHours(-4) };
        Assert.False(ShiftOps.DoitFermerAuto(ts, e, DateTime.Now));
    }

    [Fact]
    public void Passe_huit_heures_on_ferme()
    {
        var e = Fini(9);
        var ts = new Timesheet { StartedAt = e.EndInstant().AddHours(-4) };
        Assert.True(ShiftOps.DoitFermerAuto(ts, e, DateTime.Now));
    }

    [Fact]
    public void Un_service_deja_termine_n_est_pas_retouche()
    {
        var e = Fini(30);
        var ts = new Timesheet { StartedAt = e.EndInstant().AddHours(-4), EndedAt = e.EndInstant() };
        Assert.False(ShiftOps.DoitFermerAuto(ts, e, DateTime.Now));
    }

    [Fact]
    public void Un_service_jamais_demarre_n_est_pas_ferme()
        => Assert.False(ShiftOps.DoitFermerAuto(new Timesheet(), Fini(30), DateTime.Now));

    // ---------- la fermeture ----------

    static async Task<(InMemoryDataService Db, Assignment A, Timesheet Ts, ServiceEvent E)> Oubli(int ilYAHeures)
    {
        var db = new InMemoryDataService();
        var e = await db.CreateEventAsync(Fini(ilYAHeures));
        var a = await db.CreateAssignmentAsync(new Assignment {
            Id = "a-auto", EventId = e.Id, AccountId = SeedData.EmpActiveServer,
            JobRoleId = SeedData.RoleServer, Status = AssignmentStatus.Confirmed });
        var ts = await ShiftOps.StartAsync(db, a, e.EndInstant().AddHours(-4), by: null);
        return (db, a, ts, e);
    }

    [Fact]
    public async Task La_fin_retenue_est_l_heure_de_fin_de_l_event()
    {
        var (db, a, ts, e) = await Oubli(9);

        Assert.True(await ShiftOps.FermerAutoSiNecessaireAsync(db, a, ts, e));

        Assert.Equal(e.EndInstant(), ts.EndedAt);
        Assert.Equal(TimesheetStatus.ToSend, ts.Status);
        Assert.True(ts.AutoClosed);
        Assert.Equal(ShiftState.Finished, a.Shift);
        Assert.Equal(TimeSpan.FromHours(4), ts.Duration);
    }

    [Fact]
    public async Task Une_pause_restee_ouverte_se_referme_au_meme_instant()
    {
        var (db, a, ts, e) = await Oubli(9);
        await ShiftOps.PauseAsync(db, a, ts, by: null);

        await ShiftOps.FermerAutoSiNecessaireAsync(db, a, ts, e);

        Assert.False(ts.IsOnBreak);
        Assert.Equal(ts.EndedAt, ts.Breaks[0].EndedAt);
    }

    [Fact]
    public async Task Un_service_demarre_apres_la_fin_de_l_event_ne_donne_pas_une_duree_negative()
    {
        var db = new InMemoryDataService();
        var e = await db.CreateEventAsync(Fini(20));
        var a = await db.CreateAssignmentAsync(new Assignment {
            Id = "a-tard", EventId = e.Id, AccountId = SeedData.EmpActiveServer,
            JobRoleId = SeedData.RoleServer, Status = AssignmentStatus.Confirmed });
        // démarré deux heures APRÈS la fin de l'event
        var ts = await ShiftOps.StartAsync(db, a, e.EndInstant().AddHours(2), by: null);

        await ShiftOps.FermerAutoSiNecessaireAsync(db, a, ts, e);

        Assert.Equal(ts.StartedAt, ts.EndedAt);
        Assert.Equal(TimeSpan.Zero, ts.Duration);
    }

    [Fact]
    public async Task Rien_n_est_ecrit_quand_le_delai_n_est_pas_atteint()
    {
        var (db, a, ts, e) = await Oubli(3);

        Assert.False(await ShiftOps.FermerAutoSiNecessaireAsync(db, a, ts, e));
        Assert.Null(ts.EndedAt);
    }

    // ---------- là où ça s'applique ----------

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
    public async Task La_liste_des_timesheets_balaie_meme_les_events_passes()
    {
        var (db, _, ts, e) = await Oubli(40);   // event terminé depuis presque deux jours
        await Connecte(db, SeedData.MgrId);

        Render<AdminTaos.Pages.Manager.MTimesheets>();

        var apres = (await db.GetTimesheetAsync(ts.Id))!;
        Assert.Equal(e.EndInstant(), apres.EndedAt);
        Assert.True(apres.AutoClosed);
    }

    [Fact]
    public async Task Le_collaborateur_referme_son_service_oublie_depuis_Mes_heures()
    {
        // L'accueil ne liste que les events non terminés : un service oublié porte forcément
        // sur un event passé, il ne pouvait donc jamais s'y refermer.
        var (db, _, ts, e) = await Oubli(12);
        await Connecte(db, SeedData.EmpActiveServer);

        Render<AdminTaos.Pages.Employee.EHours>();

        var apres = (await db.GetTimesheetAsync(ts.Id))!;
        Assert.Equal(e.EndInstant(), apres.EndedAt);
        Assert.True(apres.AutoClosed);
    }

    [Fact]
    public async Task Le_tableau_d_equipe_referme_les_services_oublies()
    {
        var (db, _, ts, e) = await Oubli(12);
        await Connecte(db, SeedData.MgrId);

        Render<AdminTaos.Components.TeamBoard>(p => p.Add(x => x.Event, e));

        Assert.NotNull((await db.GetTimesheetAsync(ts.Id))!.EndedAt);
    }

    // ---------- l'admin doit savoir que ces heures ne sont pas déclarées ----------

    [Fact]
    public async Task La_fiche_signale_une_fermeture_automatique()
    {
        var (db, a, ts, e) = await Oubli(9);
        await ShiftOps.FermerAutoSiNecessaireAsync(db, a, ts, e);
        ts.Status = TimesheetStatus.Sent;
        await db.UpdateTimesheetAsync(ts);
        Services.AddSingleton<IDataService>(db);

        var cut = Render<AdminTaos.Pages.Manager.MTimesheetDetail>(p => p.Add(x => x.Id, ts.Id));

        Assert.Contains("Arrêté automatiquement", cut.Markup);
    }

    [Fact]
    public async Task Une_fiche_declaree_normalement_ne_porte_pas_cette_mention()
    {
        var db = new InMemoryDataService();
        Services.AddSingleton<IDataService>(db);
        var ts = (await db.GetTimesheetAsync("ts-1"))!;
        ts.StartedAt = DateTime.Today.AddHours(17);
        ts.EndedAt = DateTime.Today.AddHours(23);
        ts.Status = TimesheetStatus.Sent;
        await db.UpdateTimesheetAsync(ts);

        var cut = Render<AdminTaos.Pages.Manager.MTimesheetDetail>(p => p.Add(x => x.Id, "ts-1"));

        Assert.DoesNotContain("Arrêté automatiquement", cut.Markup);
    }
}
