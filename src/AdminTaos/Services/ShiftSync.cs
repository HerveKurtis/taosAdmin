using AdminTaos.Models;

namespace AdminTaos.Services;

/// <summary>
/// Projette l'état de la timesheet sur l'affectation, seule collection que la page d'équipe
/// peut lire pour toute une équipe. Point de passage unique : les cinq écrans qui font bouger
/// un service appellent ApplyAsync, personne n'écrit ces champs à la main.
/// </summary>
public static class ShiftSync
{
    public static ShiftState StateOf(Timesheet? ts) =>
        ts?.StartedAt is null      ? ShiftState.NotStarted
        : ts.EndedAt is not null   ? ShiftState.Finished
        : ts.IsOnBreak             ? ShiftState.OnBreak
        :                            ShiftState.InService;

    /// <summary>Instant où l'état courant a débuté.</summary>
    public static DateTime? SinceOf(Timesheet? ts) => StateOf(ts) switch
    {
        ShiftState.NotStarted => null,
        ShiftState.Finished   => ts!.EndedAt,
        ShiftState.OnBreak    => ts!.OpenBreak!.StartedAt,
        _                     => ts!.Breaks.LastOrDefault(b => b.EndedAt is not null)?.EndedAt
                                 ?? ts.StartedAt,
    };

    /// <summary>
    /// Aligne l'affectation sur la timesheet et n'écrit que si quelque chose a changé.
    /// Démarrer son service vaut pointage de présence : c'est la preuve la plus directe
    /// qu'on est sur place, et ça évite au responsable de pointer quelqu'un qui vient de badger.
    /// </summary>
    public static async Task ApplyAsync(IDataService data, Assignment a, Timesheet? ts)
    {
        var etat = StateOf(ts);
        var depuis = SinceOf(ts);
        var presence = etat == ShiftState.NotStarted ? a.Presence : PresenceStatus.Present;

        if (a.Shift == etat && a.ShiftSince == depuis && a.Presence == presence) return;

        a.Shift = etat;
        a.ShiftSince = depuis;
        a.Presence = presence;

        try
        {
            await data.UpdateAssignmentAsync(a);
        }
        catch
        {
            // Silence délibéré, contrairement aux échecs d'assignation ou de pointage qui, eux,
            // s'affichent : ici la timesheet est déjà enregistrée et le service a réellement
            // démarré. Faire remonter l'erreur empêcherait quelqu'un de commencer son service
            // pour un simple défaut d'affichage sur l'écran du responsable, qui se rattrapera
            // au prochain changement d'état.
        }
    }
}
