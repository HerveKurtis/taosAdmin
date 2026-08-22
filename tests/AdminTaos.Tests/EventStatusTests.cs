using AdminTaos.Models;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>
/// Le statut se déduit de la date et des horaires. Il était stocké et n'était jamais mis à
/// jour : tout event créé depuis l'app restait « À venir » indéfiniment.
/// </summary>
public class EventStatusTests
{
    static readonly DateOnly Jour = new(2026, 8, 22);

    static ServiceEvent Soiree(int hDebut, int hFin) => new() {
        Name = "Test", Date = Jour,
        MeetingTime = new TimeOnly(hDebut, 0), ExpectedEndTime = new TimeOnly(hFin, 0) };

    static DateTime Le(int jour, int heure, int minute = 0) => new(2026, 8, jour, heure, minute, 0);

    // ---------- service dans la journée : 17h00 → 23h00 ----------

    [Fact] public void Avant_le_rendez_vous_l_event_est_a_venir()
        => Assert.Equal(EventStatus.Upcoming, Soiree(17, 23).StatusAt(Le(22, 16, 59)));

    [Fact] public void A_l_heure_du_rendez_vous_l_event_demarre()
        => Assert.Equal(EventStatus.InProgress, Soiree(17, 23).StatusAt(Le(22, 17)));

    [Fact] public void Pendant_le_service_l_event_est_en_cours()
        => Assert.Equal(EventStatus.InProgress, Soiree(17, 23).StatusAt(Le(22, 20, 30)));

    [Fact] public void A_l_heure_de_fin_l_event_est_encore_en_cours()
        => Assert.Equal(EventStatus.InProgress, Soiree(17, 23).StatusAt(Le(22, 23)));

    [Fact] public void Apres_la_fin_l_event_est_passe()
        => Assert.Equal(EventStatus.Past, Soiree(17, 23).StatusAt(Le(22, 23, 1)));

    [Fact] public void La_veille_l_event_est_a_venir()
        => Assert.Equal(EventStatus.Upcoming, Soiree(17, 23).StatusAt(Le(21, 23, 59)));

    [Fact] public void Le_lendemain_l_event_est_passe()
        => Assert.Equal(EventStatus.Past, Soiree(17, 23).StatusAt(Le(23, 0, 1)));

    // ---------- service qui passe minuit : 17h00 → 02h00 ----------

    [Fact] public void Un_service_qui_passe_minuit_est_en_cours_avant_minuit()
        => Assert.Equal(EventStatus.InProgress, Soiree(17, 2).StatusAt(Le(22, 23, 30)));

    [Fact] public void Un_service_qui_passe_minuit_est_encore_en_cours_apres_minuit()
        => Assert.Equal(EventStatus.InProgress, Soiree(17, 2).StatusAt(Le(23, 1, 30)));

    [Fact] public void Un_service_qui_passe_minuit_se_termine_a_l_heure_du_lendemain()
        => Assert.Equal(EventStatus.Past, Soiree(17, 2).StatusAt(Le(23, 2, 1)));

    [Fact] public void Un_service_qui_passe_minuit_reste_a_venir_avant_son_debut()
        => Assert.Equal(EventStatus.Upcoming, Soiree(17, 2).StatusAt(Le(22, 16)));

    [Fact] public void Un_brunch_du_matin_se_termine_bien_le_jour_meme()
        => Assert.Equal(EventStatus.Past, Soiree(9, 15).StatusAt(Le(22, 15, 1)));

    // ---------- la propriété suit l'horloge ----------

    [Fact]
    public void La_propriete_Status_reflete_l_instant_present()
    {
        var hier = new ServiceEvent {
            Date = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)),
            MeetingTime = new TimeOnly(9, 0), ExpectedEndTime = new TimeOnly(17, 0) };
        Assert.Equal(EventStatus.Past, hier.Status);

        var demain = new ServiceEvent {
            Date = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            MeetingTime = new TimeOnly(9, 0), ExpectedEndTime = new TimeOnly(17, 0) };
        Assert.Equal(EventStatus.Upcoming, demain.Status);
    }

    // ---------- cas limites ----------

    [Fact]
    public void Un_service_qui_se_termine_pile_a_minuit_court_jusqu_au_lendemain_zero_heure()
    {
        var e = Soiree(20, 0);   // 20:00 → 00:00
        Assert.Equal(EventStatus.InProgress, e.StatusAt(Le(22, 23, 59)));
        Assert.Equal(EventStatus.InProgress, e.StatusAt(Le(23, 0, 0)));
        Assert.Equal(EventStatus.Past,       e.StatusAt(Le(23, 0, 1)));
    }

    [Fact]
    public void Un_service_de_nuit_commence_apres_minuit()
    {
        var e = Soiree(0, 6);    // 00:00 → 06:00, service de nuit
        Assert.Equal(EventStatus.InProgress, e.StatusAt(Le(22, 0, 0)));
        Assert.Equal(EventStatus.InProgress, e.StatusAt(Le(22, 5, 59)));
        Assert.Equal(EventStatus.Past,       e.StatusAt(Le(22, 6, 1)));
    }

    [Fact]
    public void Des_horaires_identiques_donnent_un_service_de_vingt_quatre_heures()
    {
        var e = Soiree(17, 17);
        Assert.Equal(EventStatus.InProgress, e.StatusAt(Le(22, 17)));
        Assert.Equal(EventStatus.InProgress, e.StatusAt(Le(23, 16, 59)));
        Assert.Equal(EventStatus.Past,       e.StatusAt(Le(23, 17, 1)));
    }

    [Fact]
    public void Une_seconde_avant_le_rendez_vous_l_event_n_a_pas_commence()
    {
        var e = Soiree(17, 23);
        Assert.Equal(EventStatus.Upcoming, e.StatusAt(Le(22, 16, 59)));
        Assert.Equal(EventStatus.InProgress, e.StatusAt(Le(22, 17, 0)));
    }

    [Fact]
    public void Un_event_lointain_dans_le_passe_reste_passe()
    {
        var vieux = new ServiceEvent {
            Date = new DateOnly(2020, 1, 15),
            MeetingTime = new TimeOnly(18, 0), ExpectedEndTime = new TimeOnly(23, 0) };
        Assert.Equal(EventStatus.Past, vieux.Status);
    }

    [Fact]
    public void Le_statut_n_est_plus_stocke_dans_le_document()
    {
        // Sérialisé, il polluerait le document et laisserait croire que la valeur fait foi.
        var json = System.Text.Json.JsonSerializer.Serialize(
            Soiree(17, 23), new System.Text.Json.JsonSerializerOptions(
                System.Text.Json.JsonSerializerDefaults.Web));
        Assert.DoesNotContain("status", json, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Un_document_portant_un_ancien_statut_perime_l_ignore()
    {
        // Les events déjà en base contiennent status:"Upcoming" — il ne doit plus être lu.
        var json = """
            {"id":"e1","name":"Ancien","date":"2020-01-15",
             "meetingTime":"18:00:00","expectedEndTime":"23:00:00","status":"Upcoming"}
            """;
        var e = System.Text.Json.JsonSerializer.Deserialize<ServiceEvent>(
            json, new System.Text.Json.JsonSerializerOptions(
                System.Text.Json.JsonSerializerDefaults.Web))!;
        Assert.Equal(EventStatus.Past, e.Status);
    }
}
