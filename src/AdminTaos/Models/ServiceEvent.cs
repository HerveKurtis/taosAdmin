namespace AdminTaos.Models;

public class ServiceEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Venue { get; set; } = "";
    public string Address { get; set; } = "";
    public DateOnly Date { get; set; }
    public TimeOnly MeetingTime { get; set; }
    public TimeOnly ExpectedEndTime { get; set; }
    public string DressCode { get; set; } = "";
    public string Instructions { get; set; } = "";
    public string OnSiteContact { get; set; } = "";
    /// <summary>Compte désigné responsable pour cet event. Remplace OnSiteContact, conservé en repli.</summary>
    public string? ResponsableAccountId { get; set; }
    public List<RoleNeed> RoleNeeds { get; set; } = new();
    public bool IsOpenForSignup { get; set; }
    /// <summary>
    /// Statut à un instant donné. Le paramètre existe pour rendre le calcul testable sans
    /// dépendre de l'horloge — la propriété Status ci-dessous est le seul usage en production.
    /// </summary>
    public EventStatus StatusAt(DateTime now)
    {
        var debut = Date.ToDateTime(MeetingTime);
        var fin   = Date.ToDateTime(ExpectedEndTime);

        // Une fin numériquement antérieure au début signifie que le service passe minuit :
        // un gala 20:00 → 02:00 se termine le lendemain. Le cas est courant en événementiel.
        if (fin <= debut) fin = fin.AddDays(1);

        if (now < debut) return EventStatus.Upcoming;
        return now <= fin ? EventStatus.InProgress : EventStatus.Past;
    }

    /// <summary>
    /// Déduit de la date et des horaires, jamais stocké. Le champ l'était auparavant et
    /// n'était mis à jour par rien : tout event créé depuis l'app restait « À venir ».
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public EventStatus Status => StatusAt(DateTime.Now);
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
