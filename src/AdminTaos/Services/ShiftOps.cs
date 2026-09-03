using AdminTaos.Models;

namespace AdminTaos.Services;

/// <summary>
/// Les quatre gestes d'un service : démarrer, mettre en pause, reprendre, terminer. Un seul
/// endroit, que le geste vienne de la personne ou d'un tiers — deux implémentations
/// divergeraient au premier correctif. Chaque geste projette l'état sur l'affectation.
///
/// Le paramètre <c>by</c> vaut null quand la personne agit sur son propre service, et porte
/// l'identifiant de l'auteur sinon : une timesheet n'est plus forcément une déclaration
/// personnelle, et un litige de paie doit pouvoir être tranché.
/// </summary>
public static class ShiftOps
{
    /// <summary>Au-delà, le responsable du jour perd la main : seul l'admin corrige encore.</summary>
    public static readonly TimeSpan FenetreResponsable = TimeSpan.FromHours(48);

    /// <summary>Qui peut piloter les compteurs de l'équipe sur cet event.</summary>
    public static bool CanPilot(Account? viewer, ServiceEvent e) => CanPilotAt(viewer, e, DateTime.Now);

    public static bool CanPilotAt(Account? viewer, ServiceEvent e, DateTime now)
    {
        if (viewer is null) return false;
        if (viewer.Type == AccountType.Manager) return true;
        if (viewer.Id != e.ResponsableAccountId) return false;
        return now <= e.EndInstant() + FenetreResponsable;
    }

    /// <summary>
    /// On ne démarre pas le compteur de quelqu'un qu'on vient de donner absent : ce serait lui
    /// compter des heures après avoir constaté qu'il n'était pas venu.
    /// </summary>
    public static bool CanStart(Assignment a) => a.Presence != PresenceStatus.Absent;

    /// <summary>
    /// Transforme une heure saisie en instant réel, ancré sur le service et non sur l'horloge.
    /// Un service qui passe minuit s'étale sur deux jours : 22:00 appartient au soir de l'event,
    /// 01:00 au lendemain. On bascule au lendemain seulement quand l'heure saisie tombe plus de
    /// douze heures avant le rendez-vous — sinon quelqu'un arrivé une heure en avance verrait
    /// son début reporté d'un jour.
    /// </summary>
    public static DateTime InstantDansLeService(TimeOnly heure, ServiceEvent e)
    {
        var debut = e.Date.ToDateTime(e.MeetingTime);
        var candidat = e.Date.ToDateTime(heure);
        return candidat < debut.AddHours(-12) ? candidat.AddDays(1) : candidat;
    }

    public static async Task<Timesheet> StartAsync(IDataService data, Assignment a, DateTime at, string? by)
    {
        var ts = await data.GetTimesheetForAssignmentAsync(a.Id)
                 ?? await data.CreateTimesheetAsync(new Timesheet { AssignmentId = a.Id });

        ts.StartedAt = at;
        ts.StartedByAccountId = by;
        ts.Status = TimesheetStatus.InProgress;
        await data.UpdateTimesheetAsync(ts);
        await ShiftSync.ApplyAsync(data, a, ts);
        return ts;
    }

    public static async Task PauseAsync(IDataService data, Assignment a, Timesheet ts, string? by)
    {
        if (ts.IsOnBreak || ts.StartedAt is null || ts.EndedAt is not null) return;

        ts.Breaks.Add(new ShiftBreak { StartedAt = DateTime.Now, StartedByAccountId = by });
        await data.UpdateTimesheetAsync(ts);
        await ShiftSync.ApplyAsync(data, a, ts);
    }

    public static async Task ResumeAsync(IDataService data, Assignment a, Timesheet ts, string? by)
    {
        if (ts.OpenBreak is not { } ouverte) return;

        ouverte.EndedAt = DateTime.Now;
        ouverte.EndedByAccountId = by;
        await data.UpdateTimesheetAsync(ts);
        await ShiftSync.ApplyAsync(data, a, ts);
    }

    public static async Task FinishAsync(IDataService data, Assignment a, Timesheet ts, string? by)
    {
        if (ts.StartedAt is null || ts.EndedAt is not null) return;

        var fin = DateTime.Now;
        // Terminer pendant une pause la referme à l'heure de fin : personne ne reste en pause.
        if (ts.OpenBreak is { } ouverte) ouverte.EndedAt = fin;

        ts.EndedAt = fin;
        ts.EndedByAccountId = by;
        ts.Status = TimesheetStatus.ToSend;
        await data.UpdateTimesheetAsync(ts);
        await ShiftSync.ApplyAsync(data, a, ts);
    }
}
