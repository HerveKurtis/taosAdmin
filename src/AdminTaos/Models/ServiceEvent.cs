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

    /// <summary>
    /// Copie horodatée de EndInstant(), écrite à l'enregistrement. Existe uniquement pour les
    /// règles Firestore : la date et les horaires sont stockés en texte et les règles ne savent
    /// pas les convertir en instant, alors qu'un champ finissant par « At » devient un vrai
    /// Timestamp comparable à request.time. C'est ce qui rend le verrou 48 h opposable serveur.
    /// </summary>
    public DateTime? EndsAt { get; set; }
    public List<RoleNeed> RoleNeeds { get; set; } = new();
    public bool IsOpenForSignup { get; set; }
    /// <summary>
    /// Statut à un instant donné. Le paramètre existe pour rendre le calcul testable sans
    /// dépendre de l'horloge — la propriété Status ci-dessous est le seul usage en production.
    /// </summary>
    /// <summary>
    /// Instant réel de fin. Une fin numériquement antérieure au début signifie que le service
    /// passe minuit : un gala 20:00 → 02:00 se termine le lendemain. Courant en événementiel.
    /// </summary>
    public DateTime EndInstant()
    {
        var debut = Date.ToDateTime(MeetingTime);
        var fin   = Date.ToDateTime(ExpectedEndTime);
        return fin <= debut ? fin.AddDays(1) : fin;
    }

    public EventStatus StatusAt(DateTime now)
    {
        var debut = Date.ToDateTime(MeetingTime);
        if (now < debut) return EventStatus.Upcoming;
        return now <= EndInstant() ? EventStatus.InProgress : EventStatus.Past;
    }

    /// <summary>
    /// Déduit de la date et des horaires, jamais stocké. Le champ l'était auparavant et
    /// n'était mis à jour par rien : tout event créé depuis l'app restait « À venir ».
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public EventStatus Status => StatusAt(DateTime.Now);
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
