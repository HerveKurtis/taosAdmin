using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>
/// L'action mise en avant sur la fiche d'un event suit son état : on prépare avant, on pilote
/// pendant. Les deux restent atteignables — il arrive de compléter une équipe en plein service.
/// </summary>
public class EventActionsTests : BunitContext
{
    async Task<InMemoryDataService> Db()
    {
        var db = new InMemoryDataService();
        await db.CreateEventAsync(new ServiceEvent {
            Id = "evt-live2", Name = "En cours", Date = DateOnly.FromDateTime(DateTime.Today),
            MeetingTime = new TimeOnly(0, 0), ExpectedEndTime = new TimeOnly(23, 59) });
        await db.CreateEventAsync(new ServiceEvent {
            Id = "evt-soon2", Name = "À venir", Date = DateOnly.FromDateTime(DateTime.Today.AddDays(4)),
            MeetingTime = new TimeOnly(18, 0), ExpectedEndTime = new TimeOnly(23, 0) });
        Services.AddSingleton<IDataService>(db);
        return db;
    }

    static string ClasseDu(IRenderedComponent<AdminTaos.Pages.Manager.MEventDetail> cut, string texte)
        => cut.FindAll("a.btn").Single(a => a.TextContent.Contains(texte)).ClassName ?? "";

    [Fact]
    public async Task Avant_l_event_c_est_l_assignation_qui_est_mise_en_avant()
    {
        await Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-soon2"));

        Assert.Contains("primary", ClasseDu(cut, "Assigner du personnel"));
        Assert.Contains("ghost", ClasseDu(cut, "Équipe du jour"));
    }

    [Fact]
    public async Task Pendant_l_event_c_est_l_equipe_du_jour_qui_est_mise_en_avant()
    {
        await Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-live2"));

        Assert.Contains("primary", ClasseDu(cut, "Équipe du jour"));
        Assert.Contains("ghost", ClasseDu(cut, "Assigner du personnel"));
    }

    [Fact]
    public async Task Les_deux_actions_restent_atteignables_dans_les_deux_cas()
    {
        await Db();

        var enCours = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-live2"));
        Assert.Contains("m/events/evt-live2/assign", enCours.Markup);
        Assert.Contains("m/events/evt-live2/equipe", enCours.Markup);

        var aVenir = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-soon2"));
        Assert.Contains("m/events/evt-soon2/assign", aVenir.Markup);
        Assert.Contains("m/events/evt-soon2/equipe", aVenir.Markup);
    }

    [Fact]
    public async Task Un_event_en_cours_annonce_l_equipe_du_jour()
    {
        await Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-live2"));

        Assert.Contains("Service en cours", cut.Markup);
    }

    [Fact]
    public async Task Un_event_termine_ne_met_rien_en_avant()
    {
        await Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-past"));

        Assert.DoesNotContain("primary", ClasseDu(cut, "Assigner du personnel"));
        Assert.DoesNotContain("primary", ClasseDu(cut, "Équipe du jour"));
    }
}
