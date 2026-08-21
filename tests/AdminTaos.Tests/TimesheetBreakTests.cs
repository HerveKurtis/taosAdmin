using AdminTaos.Models;
using AdminTaos.Services;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>
/// Le temps presté est l'amplitude du service moins les pauses. Duration porte le net :
/// tous les écrans qui l'affichaient déjà passent au net sans être modifiés.
/// </summary>
public class TimesheetBreakTests
{
    static readonly DateTime H17 = new(2026, 8, 21, 17, 0, 0);

    static Timesheet Shift(params (int fromMin, int? toMin)[] breaks)
    {
        var ts = new Timesheet { StartedAt = H17, EndedAt = H17.AddHours(6) };
        foreach (var (from, to) in breaks)
            ts.Breaks.Add(new ShiftBreak {
                StartedAt = H17.AddMinutes(from),
                EndedAt = to is { } t ? H17.AddMinutes(t) : null });
        return ts;
    }

    [Fact]
    public void A_new_timesheet_has_no_break()
    {
        var ts = new Timesheet();
        Assert.Empty(ts.Breaks);
        Assert.False(ts.IsOnBreak);
        Assert.Equal(TimeSpan.Zero, ts.RecordedBreak);
        Assert.Null(ts.ManagerAdjustedBreakMinutes);
    }

    [Fact]
    public void Gross_duration_ignores_breaks()
    {
        Assert.Equal(TimeSpan.FromHours(6), Shift((120, 150)).GrossDuration);
    }

    [Fact]
    public void Worked_time_subtracts_every_closed_break()
    {
        var ts = Shift((120, 150), (240, 255));   // 30 min + 15 min
        Assert.Equal(TimeSpan.FromMinutes(45), ts.RecordedBreak);
        Assert.Equal(TimeSpan.FromMinutes(360 - 45), ts.Duration);
    }

    [Fact]
    public void An_open_break_counts_for_nothing_until_it_is_closed()
    {
        var ts = Shift((120, 150), (240, null));
        Assert.True(ts.IsOnBreak);
        Assert.NotNull(ts.OpenBreak);
        Assert.Equal(TimeSpan.FromMinutes(30), ts.RecordedBreak);
    }

    [Fact]
    public void Worked_time_never_goes_negative()
    {
        var ts = Shift((0, 600));   // pause plus longue que le service
        Assert.Equal(TimeSpan.Zero, ts.Duration);
    }

    [Fact]
    public void The_admin_correction_replaces_the_recorded_total()
    {
        var ts = Shift((120, 198));                  // 78 min pointées
        ts.ManagerAdjustedBreakMinutes = 33;         // 33 min réelles
        Assert.Equal(TimeSpan.FromMinutes(78), ts.RecordedBreak);   // le pointage reste visible
        Assert.Equal(TimeSpan.FromMinutes(33), ts.EffectiveBreak);
        Assert.Equal(TimeSpan.FromMinutes(360 - 33), ts.Duration);
    }

    [Fact]
    public void A_zero_minute_correction_is_honoured_and_is_not_read_as_absent()
    {
        var ts = Shift((120, 150));
        ts.ManagerAdjustedBreakMinutes = 0;
        Assert.Equal(TimeSpan.Zero, ts.EffectiveBreak);
        Assert.Equal(TimeSpan.FromHours(6), ts.Duration);
    }

    [Fact]
    public void An_unfinished_shift_has_no_duration()
    {
        var ts = new Timesheet { StartedAt = H17 };
        Assert.Null(ts.GrossDuration);
        Assert.Null(ts.Duration);
    }

    [Fact]
    public void Manager_adjusted_hours_still_win_over_the_pointed_ones()
    {
        var ts = Shift((120, 150));
        ts.ManagerAdjustedEnd = H17.AddHours(5);     // fin ramenée à 5 h
        Assert.Equal(TimeSpan.FromHours(5), ts.GrossDuration);
        Assert.Equal(TimeSpan.FromMinutes(300 - 30), ts.Duration);
    }

    [Fact]
    public void A_break_reports_its_own_duration()
    {
        var b = new ShiftBreak { StartedAt = H17, EndedAt = H17.AddMinutes(20) };
        Assert.Equal(TimeSpan.FromMinutes(20), b.Duration);
        Assert.Null(new ShiftBreak { StartedAt = H17 }.Duration);
    }

    [Theory]
    [InlineData(30, "30 min")]
    [InlineData(59, "59 min")]
    [InlineData(60, "1 h 00")]
    [InlineData(95, "1 h 35")]
    public void Short_durations_read_in_minutes(int minutes, string expected)
        => Assert.Equal(expected, TimeSpan.FromMinutes(minutes).FmtShort());
}
