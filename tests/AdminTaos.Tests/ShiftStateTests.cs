using AdminTaos.Models;
using AdminTaos.Services;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>
/// L'état de service est recopié sur l'affectation : un responsable non-admin n'a pas le droit
/// de lire les timesheets des autres, et une requête refusée échoue en entier.
/// </summary>
public class ShiftStateTests
{
    static readonly DateTime H17 = new(2026, 8, 23, 17, 0, 0);

    [Fact]
    public void Une_affectation_neuve_n_a_pas_commence()
    {
        var a = new Assignment();
        Assert.Equal(ShiftState.NotStarted, a.Shift);
        Assert.Null(a.ShiftSince);
        Assert.Equal(PresenceStatus.Expected, a.Presence);
    }

    [Fact]
    public void Sans_timesheet_l_etat_est_pas_commence()
    {
        Assert.Equal(ShiftState.NotStarted, ShiftSync.StateOf(null));
        Assert.Equal(ShiftState.NotStarted, ShiftSync.StateOf(new Timesheet()));
    }

    [Fact]
    public void Un_service_demarre_est_en_service()
    {
        var ts = new Timesheet { StartedAt = H17 };
        Assert.Equal(ShiftState.InService, ShiftSync.StateOf(ts));
        Assert.Equal(H17, ShiftSync.SinceOf(ts));
    }

    [Fact]
    public void Une_pause_ouverte_donne_en_pause_depuis_le_debut_de_la_pause()
    {
        var ts = new Timesheet { StartedAt = H17 };
        ts.Breaks.Add(new ShiftBreak { StartedAt = H17.AddHours(2) });
        Assert.Equal(ShiftState.OnBreak, ShiftSync.StateOf(ts));
        Assert.Equal(H17.AddHours(2), ShiftSync.SinceOf(ts));
    }

    [Fact]
    public void Une_pause_close_ramene_en_service_depuis_la_reprise()
    {
        var ts = new Timesheet { StartedAt = H17 };
        ts.Breaks.Add(new ShiftBreak { StartedAt = H17.AddHours(2), EndedAt = H17.AddHours(2).AddMinutes(30) });
        Assert.Equal(ShiftState.InService, ShiftSync.StateOf(ts));
        Assert.Equal(H17.AddHours(2).AddMinutes(30), ShiftSync.SinceOf(ts));
    }

    [Fact]
    public void Un_service_termine_est_termine_depuis_l_heure_de_fin()
    {
        var ts = new Timesheet { StartedAt = H17, EndedAt = H17.AddHours(6) };
        Assert.Equal(ShiftState.Finished, ShiftSync.StateOf(ts));
        Assert.Equal(H17.AddHours(6), ShiftSync.SinceOf(ts));
    }

    [Fact]
    public async Task Demarrer_son_service_vaut_pointage_de_presence()
    {
        var db = new InMemoryDataService();
        var a = (await db.GetAssignmentsForEventAsync("evt-gala")).First();
        var ts = new Timesheet { AssignmentId = a.Id, StartedAt = H17 };

        await ShiftSync.ApplyAsync(db, a, ts);

        var saved = (await db.GetAssignmentsForEventAsync("evt-gala")).First(x => x.Id == a.Id);
        Assert.Equal(ShiftState.InService, saved.Shift);
        Assert.Equal(PresenceStatus.Present, saved.Presence);
    }

    [Fact]
    public async Task Un_retardataire_marque_absent_repasse_present_en_demarrant()
    {
        var db = new InMemoryDataService();
        var a = (await db.GetAssignmentsForEventAsync("evt-gala")).First();
        a.Presence = PresenceStatus.Absent;          // le responsable l'avait donné manquant
        await db.UpdateAssignmentAsync(a);

        await ShiftSync.ApplyAsync(db, a, new Timesheet { AssignmentId = a.Id, StartedAt = H17 });

        Assert.Equal(PresenceStatus.Present,
            (await db.GetAssignmentsForEventAsync("evt-gala")).First(x => x.Id == a.Id).Presence);
    }

    [Fact]
    public async Task Une_pause_ne_touche_pas_au_pointage_de_presence()
    {
        var db = new InMemoryDataService();
        var a = (await db.GetAssignmentsForEventAsync("evt-gala")).First();
        a.Presence = PresenceStatus.Absent;
        await db.UpdateAssignmentAsync(a);

        var ts = new Timesheet { AssignmentId = a.Id };   // jamais démarré
        await ShiftSync.ApplyAsync(db, a, ts);

        Assert.Equal(PresenceStatus.Absent,
            (await db.GetAssignmentsForEventAsync("evt-gala")).First(x => x.Id == a.Id).Presence);
    }

    [Fact]
    public async Task Rien_n_est_ecrit_si_l_etat_n_a_pas_change()
    {
        var db = new CountingDataService();
        var a = (await db.GetAssignmentsForEventAsync("evt-gala")).First();
        var ts = new Timesheet { AssignmentId = a.Id, StartedAt = H17 };

        await ShiftSync.ApplyAsync(db, a, ts);
        var apresPremiere = db.Writes;
        await ShiftSync.ApplyAsync(db, a, ts);

        Assert.Equal(1, apresPremiere);
        Assert.Equal(1, db.Writes);   // le second appel n'écrit rien
    }

    sealed class CountingDataService : InMemoryDataService, IDataService
    {
        public int Writes { get; private set; }
        Task IDataService.UpdateAssignmentAsync(Assignment a)
        {
            Writes++;
            return base.UpdateAssignmentAsync(a);
        }
    }
}
