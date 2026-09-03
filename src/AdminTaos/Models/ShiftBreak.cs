namespace AdminTaos.Models;

/// <summary>Une interruption de service. EndedAt à null signifie que la pause est en cours.</summary>
public class ShiftBreak
{
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    /// <summary>Qui a envoyé en pause, si ce n'est pas la personne elle-même.</summary>
    public string? StartedByAccountId { get; set; }

    /// <summary>Qui a fait reprendre, si ce n'est pas la personne elle-même.</summary>
    public string? EndedByAccountId { get; set; }

    public TimeSpan? Duration =>
        EndedAt is { } e && e > StartedAt ? e - StartedAt : null;
}
