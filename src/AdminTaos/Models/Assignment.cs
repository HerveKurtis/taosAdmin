namespace AdminTaos.Models;

public class Assignment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string EventId { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string JobRoleId { get; set; } = "";
    public AssignmentSource Source { get; set; } = AssignmentSource.AssignedByManager;
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Confirmed;
    /// <summary>Pointage du responsable du jour. Indépendant du chronomètre de la timesheet.</summary>
    public PresenceStatus Presence { get; set; } = PresenceStatus.Expected;

    /// <summary>
    /// État de service recopié depuis la timesheet. Il vit ici parce qu'un responsable du jour
    /// non-admin n'a pas le droit de lire les timesheets des autres : la page d'équipe le
    /// trouve sur une collection qu'elle charge déjà, sans exposer la moindre heure.
    /// </summary>
    public ShiftState Shift { get; set; } = ShiftState.NotStarted;

    /// <summary>Instant où l'état courant a débuté : début de service, de pause, ou fin.</summary>
    public DateTime? ShiftSince { get; set; }
}
