using AdminTaos.Models;
using Xunit;

namespace AdminTaos.Tests;

public class ModelTests
{
    [Fact]
    public void Enums_have_expected_members()
    {
        Assert.Equal(2, System.Enum.GetValues<AccountType>().Length);
        Assert.True(System.Enum.IsDefined(AccountStatus.Pending));
        Assert.True(System.Enum.IsDefined(TimesheetStatus.ToSend));
        Assert.True(System.Enum.IsDefined(AssignmentStatus.PendingApproval));
        Assert.True(System.Enum.IsDefined(EventStatus.InProgress));
    }

    [Fact]
    public void Timesheet_duration_uses_raw_times()
    {
        var t = new Timesheet {
            StartedAt = new System.DateTime(2026,5,18,17,0,0),
            EndedAt   = new System.DateTime(2026,5,18,23,12,0)
        };
        Assert.Equal(System.TimeSpan.FromMinutes(372), t.Duration);
    }

    [Fact]
    public void Timesheet_duration_prefers_manager_adjustments()
    {
        var t = new Timesheet {
            StartedAt = new System.DateTime(2026,5,18,17,0,0),
            EndedAt   = new System.DateTime(2026,5,18,23,0,0),
            ManagerAdjustedStart = new System.DateTime(2026,5,18,17,2,0),
            ManagerAdjustedEnd   = new System.DateTime(2026,5,18,23,14,0)
        };
        Assert.Equal(System.TimeSpan.FromMinutes(372), t.Duration);
    }

    [Fact]
    public void Timesheet_duration_null_when_not_finished()
    {
        Assert.Null(new Timesheet { StartedAt = System.DateTime.Now }.Duration);
    }
}
