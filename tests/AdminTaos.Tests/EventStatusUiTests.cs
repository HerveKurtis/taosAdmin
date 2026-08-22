using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>Lecture du statut d'un event à l'écran : libellé et couleur.</summary>
public class EventStatusUiTests : BunitContext
{
    /// <summary>Toujours en cours, quelle que soit l'heure à laquelle le test tourne.</summary>
    static ServiceEvent EnCours(string nom = "En cours") => new() {
        Id = "evt-live", Name = nom, Venue = "Villa Empain",
        Date = DateOnly.FromDateTime(DateTime.Today),
        MeetingTime = new TimeOnly(0, 0), ExpectedEndTime = new TimeOnly(23, 59) };

    static ServiceEvent Termine() => new() {
        Id = "evt-done", Name = "Terminé", Venue = "BOZAR",
        Date = DateOnly.FromDateTime(DateTime.Today.AddDays(-2)),
        MeetingTime = new TimeOnly(9, 0), ExpectedEndTime = new TimeOnly(17, 0) };

    static ServiceEvent AVenir() => new() {
        Id = "evt-soon", Name = "À venir", Venue = "Hôtel Plaza",
        Date = DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
        MeetingTime = new TimeOnly(18, 0), ExpectedEndTime = new TimeOnly(23, 0) };

    // ---------- le helper ----------

    [Theory]
    [InlineData(EventStatus.Upcoming,   "À venir")]
    [InlineData(EventStatus.InProgress, "En cours")]
    [InlineData(EventStatus.Past,       "Terminé")]
    public void Le_libelle_est_en_francais(EventStatus s, string attendu)
        => Assert.Equal(attendu, s.StatusFr());

    [Theory]
    [InlineData(EventStatus.InProgress, "pill live")]
    [InlineData(EventStatus.Past,       "pill done")]
    [InlineData(EventStatus.Upcoming,   "pill soon")]
    public void Chaque_statut_a_sa_pastille(EventStatus s, string classe)
        => Assert.Equal(classe, s.PillClass());

    // ---------- liste des events ----------

    [Fact]
    public async Task La_liste_des_events_colore_chaque_statut()
    {
        var db = new InMemoryDataService();
        foreach (var e in (await db.GetEventsAsync()).ToList())
            await db.UpdateEventAsync(e);
        await db.CreateEventAsync(EnCours());
        await db.CreateEventAsync(Termine());
        await db.CreateEventAsync(AVenir());
        Services.AddSingleton<IDataService>(db);

        var cut = Render<AdminTaos.Pages.Manager.MEvents>();

        Assert.NotEmpty(cut.FindAll("span.pill.live"));
        Assert.NotEmpty(cut.FindAll("span.pill.done"));
        Assert.NotEmpty(cut.FindAll("span.pill.soon"));
        Assert.Contains("En cours", cut.Markup);
        Assert.Contains("Terminé", cut.Markup);
    }

    // ---------- tableau de bord admin ----------

    [Fact]
    public async Task Le_tableau_de_bord_admin_montre_le_statut_de_chaque_event()
    {
        var db = new InMemoryDataService();
        await db.CreateEventAsync(EnCours("Gala en cours"));
        Services.AddSingleton<IDataService>(db);

        var cut = Render<AdminTaos.Pages.Manager.MHome>();

        Assert.Contains("Gala en cours", cut.Markup);
        Assert.NotEmpty(cut.FindAll("span.pill.live"));
    }

    [Fact]
    public async Task Le_tableau_de_bord_admin_ne_liste_pas_les_events_termines()
    {
        var db = new InMemoryDataService();
        await db.CreateEventAsync(Termine());
        Services.AddSingleton<IDataService>(db);

        var cut = Render<AdminTaos.Pages.Manager.MHome>();

        Assert.DoesNotContain("evt-done", cut.Markup);
        Assert.Empty(cut.FindAll("span.pill.done"));
    }

    // ---------- tableau de bord collaborateur ----------

    [Fact]
    public async Task Le_tableau_de_bord_collaborateur_montre_le_statut_de_sa_prestation()
    {
        var db = new InMemoryDataService();
        var ev = EnCours("Cocktail en cours");
        await db.CreateEventAsync(ev);
        await db.CreateAssignmentAsync(new Assignment {
            Id = "asg-live", EventId = ev.Id, AccountId = SeedData.EmpActiveServer,
            JobRoleId = SeedData.RoleServer, Status = AssignmentStatus.Confirmed });

        var me = (await db.GetAccountAsync(SeedData.EmpActiveServer))!;
        var fake = new FakeAuthClient();
        fake.PreRegister(me.Id, me.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(me.Email, "pwd");

        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(auth);

        var cut = Render<AdminTaos.Pages.Employee.EHome>();

        Assert.NotEmpty(cut.FindAll("span.pill.live"));
        Assert.Contains("En cours", cut.Markup);
    }

    [Fact]
    public async Task Une_prestation_en_cours_passe_devant_une_autre_du_meme_jour()
    {
        var db = new InMemoryDataService();

        // Marc a déjà le Déjeuner d'affaires aujourd'hui (asg-8, pas encore commencé).
        var live = EnCours("Service en cours");
        await db.CreateEventAsync(live);
        await db.CreateAssignmentAsync(new Assignment {
            Id = "asg-live2", EventId = live.Id, AccountId = SeedData.EmpActiveServer,
            JobRoleId = SeedData.RoleServer, Status = AssignmentStatus.Confirmed });

        var me = (await db.GetAccountAsync(SeedData.EmpActiveServer))!;
        var fake = new FakeAuthClient();
        fake.PreRegister(me.Id, me.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(me.Email, "pwd");

        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(auth);

        var cut = Render<AdminTaos.Pages.Employee.EHome>();

        // Sans le tri par statut, c'est le Déjeuner d'affaires qui s'afficherait.
        Assert.Contains("Service en cours", cut.Markup);
        Assert.NotEmpty(cut.FindAll("span.pill.live"));
    }
}
