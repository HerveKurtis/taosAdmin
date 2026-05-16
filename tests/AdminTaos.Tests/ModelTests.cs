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
}
