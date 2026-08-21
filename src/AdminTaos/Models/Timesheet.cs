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

    /// <summary>Pauses pointées par le collaborateur, dans l'ordre où elles ont eu lieu.</summary>
    public List<ShiftBreak> Breaks { get; set; } = new();

    /// <summary>Total de pause retenu par l'admin, en minutes. Null = on garde le pointage réel.</summary>
    public int? ManagerAdjustedBreakMinutes { get; set; }

    public DateTime? EffectiveStart => ManagerAdjustedStart ?? StartedAt;
    public DateTime? EffectiveEnd => ManagerAdjustedEnd ?? EndedAt;

    public ShiftBreak? OpenBreak => Breaks.FirstOrDefault(b => b.EndedAt is null);
    public bool IsOnBreak => OpenBreak is not null;

    /// <summary>Somme des pauses terminées. Une pause en cours ne compte qu'une fois close.</summary>
    public TimeSpan RecordedBreak =>
        Breaks.Aggregate(TimeSpan.Zero, (total, b) => total + (b.Duration ?? TimeSpan.Zero));

    /// <summary>La correction de l'admin si elle existe, le pointage réel sinon.</summary>
    public TimeSpan EffectiveBreak => ManagerAdjustedBreakMinutes is { } m
        ? TimeSpan.FromMinutes(Math.Max(0, m))
        : RecordedBreak;

    /// <summary>Amplitude du service, pauses comprises.</summary>
    public TimeSpan? GrossDuration =>
        EffectiveStart is { } s && EffectiveEnd is { } e ? e - s : null;

    /// <summary>Temps presté : l'amplitude moins les pauses, jamais négatif.</summary>
    public TimeSpan? Duration => GrossDuration is { } g
        ? (g > EffectiveBreak ? g - EffectiveBreak : TimeSpan.Zero)
        : null;
}
