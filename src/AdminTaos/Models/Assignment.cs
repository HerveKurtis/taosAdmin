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
}
