using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>Ce que l'admin voit et peut corriger sur les pauses d'une timesheet.</summary>
public class AdminBreakReviewTests : BunitContext
{
    static readonly DateTime Base = DateTime.Today.AddHours(17);

    async Task<InMemoryDataService> Sent(params (int from, int to)[] breaks)
    {
        var db = new InMemoryDataService();
        var ts = (await db.GetTimesheetAsync("ts-1"))!;
        ts.StartedAt = Base;
        ts.EndedAt = Base.AddHours(6);
        ts.Status = TimesheetStatus.Sent;
        foreach (var (from, to) in breaks)
            ts.Breaks.Add(new ShiftBreak { StartedAt = Base.AddMinutes(from), EndedAt = Base.AddMinutes(to) });
        await db.UpdateTimesheetAsync(ts);
        Services.AddSingleton<IDataService>(db);
        return db;
    }

    IRenderedComponent<AdminTaos.Pages.Manager.MTimesheetDetail> Sheet() =>
        Render<AdminTaos.Pages.Manager.MTimesheetDetail>(p => p.Add(x => x.Id, "ts-1"));

    [Fact]
    public async Task The_pointed_breaks_are_listed()
    {
        await Sent((120, 150), (240, 255));
        var cut = Sheet();

        Assert.Equal(2, cut.FindAll(".ts-break").Count);
        Assert.Contains("19:00", cut.Markup);
        Assert.Contains("19:30", cut.Markup);
        Assert.Contains("45 min", cut.Markup);   // total pointé
    }

    [Fact]
    public async Task With_no_break_the_panel_says_so()
    {
        await Sent();
        var cut = Sheet();

        Assert.Empty(cut.FindAll(".ts-break"));
        Assert.Contains("Aucune pause pointée", cut.Markup);
    }

    [Fact]
    public async Task The_displayed_duration_is_net_of_breaks()
    {
        await Sent((120, 165));   // 45 min
        var cut = Sheet();

        Assert.Contains("5 h 15", cut.Markup);
    }

    [Fact]
    public async Task Correcting_the_total_changes_the_duration_before_saving()
    {
        await Sent((120, 198));   // 78 min pointées
        var cut = Sheet();

        cut.Find("input.ts-break-minutes").Change("33");

        Assert.Contains("5 h 27", cut.Markup);   // 6 h moins 33 min
    }

    [Fact]
    public async Task Validating_stores_the_corrected_total_and_keeps_the_pointing()
    {
        var db = await Sent((120, 198));
        var cut = Sheet();

        cut.Find("input.ts-break-minutes").Change("33");
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Valider").Click();

        var saved = (await db.GetTimesheetAsync("ts-1"))!;
        Assert.Equal(33, saved.ManagerAdjustedBreakMinutes);
        Assert.Equal(TimeSpan.FromMinutes(78), saved.RecordedBreak);
        Assert.Equal(TimeSpan.FromMinutes(33), saved.EffectiveBreak);
        Assert.Equal(TimesheetStatus.Validated, saved.Status);
    }

    [Fact]
    public async Task The_field_is_prefilled_with_the_pointed_total()
    {
        await Sent((120, 150));
        Assert.Equal("30", Sheet().Find("input.ts-break-minutes").GetAttribute("value"));
    }
}
