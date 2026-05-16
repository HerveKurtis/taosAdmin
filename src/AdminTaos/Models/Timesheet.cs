namespace AdminTaos.Models;

public class Timesheet
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AssignmentId { get; set; } = "";
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public TimesheetStatus Status { get; set; } = TimesheetStatus.NotStarted;
    public DateTime? ManagerAdjustedStart { get; set; }
    public DateTime? ManagerAdjustedEnd { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ValidatedAt { get; set; }

    public DateTime? EffectiveStart => ManagerAdjustedStart ?? StartedAt;
    public DateTime? EffectiveEnd => ManagerAdjustedEnd ?? EndedAt;

    public TimeSpan? Duration =>
        EffectiveStart is { } s && EffectiveEnd is { } e ? e - s : null;
}
