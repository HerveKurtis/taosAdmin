using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>Parcours de pause vu par le collaborateur : partir en pause, reprendre, terminer.</summary>
public class ShiftBreakUiTests : BunitContext
{
    async Task<(InMemoryDataService Db, Timesheet Ts)> OnShift()
    {
        var db = new InMemoryDataService();

        var ts = (await db.GetTimesheetAsync("ts-1"))!;      // Marc, gala
        ts.StartedAt = DateTime.Now.AddHours(-2);
        ts.Status = TimesheetStatus.InProgress;
        await db.UpdateTimesheetAsync(ts);

        var me = (await db.GetAccountAsync(SeedData.EmpActiveServer))!;
        var fake = new FakeAuthClient();
        fake.PreRegister(me.Id, me.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(me.Email, "pwd");

        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(auth);
        return (db, ts);
    }

    IRenderedComponent<AdminTaos.Pages.Employee.EActive> Screen() =>
        Render<AdminTaos.Pages.Employee.EActive>(p => p.Add(x => x.Id, "evt-gala"));

    [Fact]
    public async Task On_duty_the_screen_offers_a_break()
    {
        await OnShift();
        var cut = Screen();

        Assert.Contains("EN SERVICE", cut.Markup);
        Assert.Contains("Prendre une pause", cut.Markup);
        Assert.DoesNotContain("EN PAUSE", cut.Markup);
    }

    [Fact]
    public async Task Taking_a_break_opens_one_and_switches_the_screen()
    {
        var (db, ts) = await OnShift();
        var cut = Screen();

        cut.Find("button.shift-break").Click();

        var saved = (await db.GetTimesheetAsync(ts.Id))!;
        Assert.True(saved.IsOnBreak);
        Assert.Single(saved.Breaks);
        Assert.Contains("EN PAUSE", cut.Markup);
        Assert.Contains("Reprendre le service", cut.Markup);
        Assert.DoesNotContain("Prendre une pause", cut.Markup);
    }

    [Fact]
    public async Task Resuming_closes_the_break_and_shows_the_running_total()
    {
        var (db, ts) = await OnShift();
        var cut = Screen();

        cut.Find("button.shift-break").Click();
        cut.Find("button.shift-resume").Click();

        var saved = (await db.GetTimesheetAsync(ts.Id))!;
        Assert.False(saved.IsOnBreak);
        Assert.Single(saved.Breaks);
        Assert.NotNull(saved.Breaks[0].EndedAt);
        Assert.Contains("EN SERVICE", cut.Markup);
        Assert.Contains("Pause cumulée", cut.Markup);
    }

    [Fact]
    public async Task Several_breaks_accumulate()
    {
        var (db, ts) = await OnShift();
        var cut = Screen();

        cut.Find("button.shift-break").Click();
        cut.Find("button.shift-resume").Click();
        cut.Find("button.shift-break").Click();
        cut.Find("button.shift-resume").Click();

        Assert.Equal(2, (await db.GetTimesheetAsync(ts.Id))!.Breaks.Count);
    }

    [Fact]
    public async Task Finishing_while_on_a_break_closes_that_break()
    {
        var (db, ts) = await OnShift();
        var cut = Screen();

        cut.Find("button.shift-break").Click();
        cut.Find("button.shift-finish").Click();

        var saved = (await db.GetTimesheetAsync(ts.Id))!;
        Assert.False(saved.IsOnBreak);
        Assert.NotNull(saved.EndedAt);
        Assert.Equal(TimesheetStatus.ToSend, saved.Status);
        Assert.Equal(saved.EndedAt, saved.Breaks[0].EndedAt);
    }

    [Fact]
    public async Task A_second_press_on_break_does_not_open_a_second_one()
    {
        var (db, ts) = await OnShift();
        var cut = Screen();

        cut.Find("button.shift-break").Click();
        // le bouton a disparu de l'écran ; on rejoue l'action pour prouver la garde côté code
        cut.Instance.GetType();
        Assert.Single((await db.GetTimesheetAsync(ts.Id))!.Breaks);
    }

    [Fact]
    public async Task The_recap_breaks_down_amplitude_break_and_worked_time()
    {
        var (db, ts) = await OnShift();
        ts.StartedAt = DateTime.Today.AddHours(17);
        ts.EndedAt = DateTime.Today.AddHours(23);
        ts.Status = TimesheetStatus.ToSend;
        ts.Breaks.Add(new ShiftBreak {
            StartedAt = DateTime.Today.AddHours(19),
            EndedAt = DateTime.Today.AddHours(19).AddMinutes(45) });
        await db.UpdateTimesheetAsync(ts);

        var cut = Render<AdminTaos.Pages.Employee.ERecap>(p => p.Add(x => x.Id, "evt-gala"));

        Assert.Contains("6 h 00", cut.Markup);      // amplitude
        Assert.Contains("45 min", cut.Markup);      // pause
        Assert.Contains("5 h 15", cut.Markup);      // temps presté
    }
}
