using AdminTaos.Models;
using AdminTaos.Services;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>
/// Les quatre gestes d'un service — démarrer, mettre en pause, reprendre, terminer — passent
/// tous par ShiftOps, que le geste vienne de la personne ou d'un tiers. Une seule machine à
/// états : deux implémentations divergeraient au premier correctif.
/// </summary>
public class ShiftOpsTests
{
    const string Moi = SeedData.EmpActiveServer;
    const string Chef = SeedData.EmpActiveHost;

    static async Task<(InMemoryDataService Db, Assignment A)> Sur()
    {
        var db = new InMemoryDataService();
        var a = await db.CreateAssignmentAsync(new Assignment {
            Id = "a-ops", EventId = "evt-gala", AccountId = Moi,
            JobRoleId = SeedData.RoleServer, Status = AssignmentStatus.Confirmed });
        return (db, a);
    }

    // ---------- démarrer ----------

    [Fact]
    public async Task Demarrer_cree_la_timesheet_et_projette_l_etat()
    {
        var (db, a) = await Sur();
        var t = new DateTime(2026, 8, 23, 17, 0, 0);

        var ts = await ShiftOps.StartAsync(db, a, t, by: null);

        Assert.Equal(t, ts.StartedAt);
        Assert.Equal(TimesheetStatus.InProgress, ts.Status);
        Assert.Equal(ShiftState.InService, a.Shift);
        Assert.Equal(PresenceStatus.Present, a.Presence);
        Assert.Equal(ts.Id, (await db.GetTimesheetForAssignmentAsync(a.Id))!.Id);
    }

    [Fact]
    public async Task Demarrer_a_une_heure_passee_est_permis()
    {
        var (db, a) = await Sur();
        var oubli = DateTime.Now.AddHours(-2.5);

        var ts = await ShiftOps.StartAsync(db, a, oubli, by: Chef);

        Assert.Equal(oubli, ts.StartedAt);
    }

    [Fact]
    public async Task Demarrer_pour_quelqu_un_garde_trace_de_l_auteur()
    {
        var (db, a) = await Sur();

        var ts = await ShiftOps.StartAsync(db, a, DateTime.Now, by: Chef);

        Assert.Equal(Chef, ts.StartedByAccountId);
    }

    [Fact]
    public async Task Demarrer_soi_meme_ne_laisse_aucun_auteur_tiers()
    {
        var (db, a) = await Sur();

        var ts = await ShiftOps.StartAsync(db, a, DateTime.Now, by: null);

        Assert.Null(ts.StartedByAccountId);
    }

    [Fact]
    public async Task Demarrer_deux_fois_ne_recree_pas_de_timesheet()
    {
        var (db, a) = await Sur();
        var premier = await ShiftOps.StartAsync(db, a, DateTime.Now.AddHours(-1), by: null);
        var second = await ShiftOps.StartAsync(db, a, DateTime.Now, by: null);

        Assert.Equal(premier.Id, second.Id);
        Assert.Single((await db.GetTimesheetsAsync()).Where(t => t.AssignmentId == a.Id));
    }

    // ---------- pause, reprise, fin ----------

    [Fact]
    public async Task Mettre_en_pause_ouvre_une_pause_et_projette()
    {
        var (db, a) = await Sur();
        var ts = await ShiftOps.StartAsync(db, a, DateTime.Now.AddHours(-2), by: null);

        await ShiftOps.PauseAsync(db, a, ts, by: Chef);

        Assert.True(ts.IsOnBreak);
        Assert.Equal(Chef, ts.OpenBreak!.StartedByAccountId);
        Assert.Equal(ShiftState.OnBreak, a.Shift);
    }

    [Fact]
    public async Task Reprendre_ferme_la_pause_et_note_qui_a_repris()
    {
        var (db, a) = await Sur();
        var ts = await ShiftOps.StartAsync(db, a, DateTime.Now.AddHours(-2), by: null);
        await ShiftOps.PauseAsync(db, a, ts, by: null);

        await ShiftOps.ResumeAsync(db, a, ts, by: Chef);

        Assert.False(ts.IsOnBreak);
        Assert.Equal(Chef, ts.Breaks[0].EndedByAccountId);
        Assert.Equal(ShiftState.InService, a.Shift);
    }

    [Fact]
    public async Task Terminer_cloture_le_service_et_la_pause_ouverte()
    {
        var (db, a) = await Sur();
        var ts = await ShiftOps.StartAsync(db, a, DateTime.Now.AddHours(-3), by: null);
        await ShiftOps.PauseAsync(db, a, ts, by: null);

        await ShiftOps.FinishAsync(db, a, ts, by: Chef);

        Assert.NotNull(ts.EndedAt);
        Assert.Equal(ts.EndedAt, ts.Breaks[0].EndedAt);
        Assert.Equal(TimesheetStatus.ToSend, ts.Status);
        Assert.Equal(Chef, ts.EndedByAccountId);
        Assert.Equal(ShiftState.Finished, a.Shift);
    }

    [Fact]
    public async Task Mettre_en_pause_deux_fois_n_ouvre_pas_deux_pauses()
    {
        var (db, a) = await Sur();
        var ts = await ShiftOps.StartAsync(db, a, DateTime.Now.AddHours(-1), by: null);

        await ShiftOps.PauseAsync(db, a, ts, by: null);
        await ShiftOps.PauseAsync(db, a, ts, by: null);

        Assert.Single(ts.Breaks);
    }

    // ---------- qui a le droit de piloter ----------

    static readonly DateTime Reference = new(2026, 8, 23, 22, 0, 0);

    /// <summary>Un event de quatre heures se terminant à l'instant de référence.</summary>
    static ServiceEvent EventFini(string? responsable) => new() {
        Id = "e",
        Date = DateOnly.FromDateTime(Reference.AddHours(-4)),
        MeetingTime = TimeOnly.FromDateTime(Reference.AddHours(-4)),
        ExpectedEndTime = TimeOnly.FromDateTime(Reference),
        ResponsableAccountId = responsable };

    static Account Admin() => new() { Id = "adm", Type = AccountType.Manager, Status = AccountStatus.Active };
    static Account Collab(string id) => new() { Id = id, Type = AccountType.Employee, Status = AccountStatus.Active };

    [Fact]
    public void L_admin_pilote_toujours_meme_bien_apres_l_event()
    {
        var e = new ServiceEvent {
            Date = new DateOnly(2020, 1, 1),
            MeetingTime = new TimeOnly(18, 0), ExpectedEndTime = new TimeOnly(23, 0) };
        Assert.True(ShiftOps.CanPilot(Admin(), e));
        Assert.True(ShiftOps.CanPilotAt(Admin(), EventFini(Chef), Reference.AddYears(1)));
    }

    [Fact]
    public void Le_responsable_pilote_pendant_et_jusqu_a_quarante_huit_heures_apres()
    {
        var chef = Collab(Chef);
        var e = EventFini(Chef);
        Assert.True(ShiftOps.CanPilotAt(chef, e, Reference.AddHours(-2)));   // pendant
        Assert.True(ShiftOps.CanPilotAt(chef, e, Reference));                // à la fin pile
        Assert.True(ShiftOps.CanPilotAt(chef, e, Reference.AddHours(47)));   // 47 h après
    }

    [Fact]
    public void Le_responsable_perd_la_main_passe_quarante_huit_heures()
    {
        var e = EventFini(Chef);
        Assert.True(ShiftOps.CanPilotAt(Collab(Chef), e, Reference.AddHours(48)));        // la limite passe
        Assert.False(ShiftOps.CanPilotAt(Collab(Chef), e, Reference.AddHours(48.5)));     // au-delà, non
        Assert.False(ShiftOps.CanPilotAt(Collab(Chef), e, Reference.AddHours(72)));
    }

    [Fact]
    public void Un_collaborateur_quelconque_ne_pilote_personne()
    {
        Assert.False(ShiftOps.CanPilotAt(Collab("autre"), EventFini(Chef), Reference.AddHours(-1)));
    }

    [Fact]
    public void Sans_session_on_ne_pilote_rien()
        => Assert.False(ShiftOps.CanPilotAt(null, EventFini(Chef), Reference.AddHours(-1)));

    // ---------- on ne démarre pas quelqu'un donné absent ----------

    [Theory]
    [InlineData(PresenceStatus.Expected, true)]
    [InlineData(PresenceStatus.Present, true)]
    [InlineData(PresenceStatus.Absent, false)]
    public void Un_absent_ne_peut_pas_etre_demarre(PresenceStatus p, bool attendu)
        => Assert.Equal(attendu, ShiftOps.CanStart(new Assignment { Presence = p }));

    // ---------- l'instant de fin, pour la règle serveur ----------

    [Fact]
    public void L_instant_de_fin_tient_compte_du_passage_de_minuit()
    {
        var e = new ServiceEvent {
            Date = new DateOnly(2026, 8, 23),
            MeetingTime = new TimeOnly(20, 0), ExpectedEndTime = new TimeOnly(2, 0) };
        Assert.Equal(new DateTime(2026, 8, 24, 2, 0, 0), e.EndInstant());
    }

    [Fact]
    public void L_instant_de_fin_reste_le_jour_meme_pour_un_service_de_journee()
    {
        var e = new ServiceEvent {
            Date = new DateOnly(2026, 8, 23),
            MeetingTime = new TimeOnly(9, 0), ExpectedEndTime = new TimeOnly(17, 0) };
        Assert.Equal(new DateTime(2026, 8, 23, 17, 0, 0), e.EndInstant());
    }

    // ---------- l'heure saisie, quand le service passe minuit ----------

    /// <summary>Service du 4 septembre, de <paramref name="debut"/> à <paramref name="fin"/>.</summary>
    static ServiceEvent Service(string debut, string fin) => new() {
        Date = new DateOnly(2026, 9, 4),
        MeetingTime = TimeOnly.Parse(debut), ExpectedEndTime = TimeOnly.Parse(fin) };

    [Theory]
    // service de journée : l'heure saisie tombe le jour de l'event
    [InlineData("11:00", "16:00", "11:05", "2026-09-04 11:05")]
    // arrivé un peu en avance : toujours le jour de l'event, pas la veille
    [InlineData("20:00", "02:00", "19:00", "2026-09-04 19:00")]
    // service qui passe minuit, heure du soir : le jour de l'event
    [InlineData("20:00", "02:00", "22:00", "2026-09-04 22:00")]
    // service qui passe minuit, heure du petit matin : le lendemain
    [InlineData("20:00", "02:00", "01:00", "2026-09-05 01:00")]
    // pile à l'heure du rendez-vous
    [InlineData("20:00", "02:00", "20:00", "2026-09-04 20:00")]
    public void L_heure_saisie_s_ancre_sur_le_service_et_non_sur_l_horloge(
        string debut, string fin, string saisie, string attendu)
    {
        var instant = ShiftOps.InstantDansLeService(TimeOnly.Parse(saisie), Service(debut, fin));
        Assert.Equal(DateTime.Parse(attendu), instant);
    }
}
