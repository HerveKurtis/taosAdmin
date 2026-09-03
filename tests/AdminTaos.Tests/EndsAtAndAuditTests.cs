using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>
/// endsAt est un horodatage écrit pour les règles Firestore, qui ne savent pas lire une date
/// et une heure stockées en texte. Sans lui, le verrou 48 h ne vivrait que dans l'interface.
/// </summary>
public class EndsAtAndAuditTests : BunitContext
{
    InMemoryDataService Db()
    {
        var db = new InMemoryDataService();
        Services.AddSingleton<IDataService>(db);
        return db;
    }

    async Task Admin(InMemoryDataService db)
    {
        var me = (await db.GetAccountAsync(SeedData.MgrId))!;
        var fake = new FakeAuthClient();
        fake.PreRegister(me.Id, me.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(me.Email, "pwd");
        Services.AddSingleton(auth);
    }

    [Fact]
    public async Task Enregistrer_un_event_ecrit_son_instant_de_fin()
    {
        var db = Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>();

        cut.FindAll(".field input")[0].Change("Nouvelle prestation");
        cut.Find("button.btn.primary.block").Click();

        var e = (await db.GetEventsAsync()).Single(x => x.Name == "Nouvelle prestation");
        Assert.NotNull(e.EndsAt);
        Assert.Equal(e.EndInstant(), e.EndsAt);
    }

    [Fact]
    public async Task Modifier_un_event_met_a_jour_son_instant_de_fin()
    {
        var db = Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>(p => p.Add(x => x.Id, "evt-gala"));

        cut.Find("button.btn.primary.block").Click();

        var e = (await db.GetEventAsync("evt-gala"))!;
        Assert.Equal(e.EndInstant(), e.EndsAt);
    }

    [Fact]
    public async Task L_admin_qui_ouvre_une_fiche_complete_l_instant_de_fin_manquant()
    {
        var db = Db();
        await Admin(db);
        Assert.Null((await db.GetEventAsync("evt-gala"))!.EndsAt);

        Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-gala"));

        var e = (await db.GetEventAsync("evt-gala"))!;
        Assert.Equal(e.EndInstant(), e.EndsAt);
    }

    [Fact]
    public void L_instant_de_fin_couvre_le_passage_de_minuit()
    {
        var e = new ServiceEvent {
            Date = new DateOnly(2026, 9, 5),
            MeetingTime = new TimeOnly(20, 0), ExpectedEndTime = new TimeOnly(2, 0) };
        Assert.Equal(new DateTime(2026, 9, 6, 2, 0, 0), e.EndInstant());
    }

    // ---------- traçabilité affichée à l'admin ----------

    [Fact]
    public async Task La_fiche_timesheet_nomme_qui_a_demarre_et_qui_a_arrete()
    {
        var db = Db();
        var ts = (await db.GetTimesheetAsync("ts-1"))!;
        ts.StartedAt = DateTime.Today.AddHours(17);
        ts.EndedAt = DateTime.Today.AddHours(23);
        ts.Status = TimesheetStatus.Sent;
        ts.StartedByAccountId = SeedData.EmpActiveHost;   // Sarah, responsable du jour
        ts.EndedByAccountId = SeedData.MgrId;             // l'admin
        await db.UpdateTimesheetAsync(ts);

        var cut = Render<AdminTaos.Pages.Manager.MTimesheetDetail>(p => p.Add(x => x.Id, "ts-1"));

        Assert.Contains("Démarré par", cut.Markup);
        Assert.Contains("Sarah K.", cut.Markup);
        Assert.Contains("Arrêté par", cut.Markup);
        Assert.Contains("Hervé T.", cut.Markup);
    }

    [Fact]
    public async Task Une_timesheet_saisie_par_la_personne_elle_meme_n_affiche_aucun_auteur()
    {
        var db = Db();
        var ts = (await db.GetTimesheetAsync("ts-1"))!;
        ts.StartedAt = DateTime.Today.AddHours(17);
        ts.EndedAt = DateTime.Today.AddHours(23);
        ts.Status = TimesheetStatus.Sent;
        await db.UpdateTimesheetAsync(ts);

        var cut = Render<AdminTaos.Pages.Manager.MTimesheetDetail>(p => p.Add(x => x.Id, "ts-1"));

        Assert.DoesNotContain("Démarré par", cut.Markup);
        Assert.DoesNotContain("Arrêté par", cut.Markup);
    }

    [Fact]
    public async Task Une_pause_posée_par_un_tiers_est_signalee()
    {
        var db = Db();
        var ts = (await db.GetTimesheetAsync("ts-1"))!;
        ts.StartedAt = DateTime.Today.AddHours(17);
        ts.EndedAt = DateTime.Today.AddHours(23);
        ts.Status = TimesheetStatus.Sent;
        ts.Breaks.Add(new ShiftBreak {
            StartedAt = DateTime.Today.AddHours(19),
            EndedAt = DateTime.Today.AddHours(19).AddMinutes(30),
            StartedByAccountId = SeedData.EmpActiveHost });
        await db.UpdateTimesheetAsync(ts);

        var cut = Render<AdminTaos.Pages.Manager.MTimesheetDetail>(p => p.Add(x => x.Id, "ts-1"));

        Assert.Contains("par Sarah K.", cut.Markup);
    }
}
