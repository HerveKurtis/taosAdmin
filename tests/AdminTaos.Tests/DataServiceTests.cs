using AdminTaos.Models;
using AdminTaos.Services;
using Xunit;

namespace AdminTaos.Tests;

public class DataServiceTests
{
    static InMemoryDataService New() => new();

    [Fact]
    public async Task Seeds_all_collections()
    {
        var db = New();
        Assert.Equal(5, (await db.GetAccountsAsync()).Count);
        Assert.Equal(2, (await db.GetJobRolesAsync()).Count);
        Assert.Equal(4, (await db.GetEventsAsync()).Count);
        Assert.Equal(4, (await db.GetAssignmentsAsync()).Count);
        Assert.Equal(3, (await db.GetTimesheetsAsync()).Count);
    }

    [Fact]
    public async Task GetAccountByEmail_is_case_insensitive()
    {
        var db = New();
        var a = await db.GetAccountByEmailAsync("MANAGER@taos.be");
        Assert.NotNull(a);
        Assert.Equal(AccountType.Manager, a!.Type);
    }

    [Fact]
    public async Task CreateAccount_then_update_persists()
    {
        var db = New();
        var created = await db.CreateAccountAsync(new Account { FullName = "X", Email = "x@taos.be" });
        created.Status = AccountStatus.Active;
        await db.UpdateAccountAsync(created);
        Assert.Equal(AccountStatus.Active, (await db.GetAccountAsync(created.Id))!.Status);
    }

    [Fact]
    public async Task DeleteJobRole_removes_it()
    {
        var db = New();
        await db.DeleteJobRoleAsync(SeedData.RoleHost);
        Assert.Single(await db.GetJobRolesAsync());
    }

    [Fact]
    public async Task Assignments_filtered_by_event_and_account()
    {
        var db = New();
        Assert.NotEmpty(await db.GetAssignmentsForEventAsync("evt-gala"));
        Assert.NotEmpty(await db.GetAssignmentsForAccountAsync(SeedData.EmpActiveServer));
    }

    [Fact]
    public async Task GetTimesheetForAssignment_returns_match()
    {
        var db = New();
        var ts = await db.GetTimesheetForAssignmentAsync("asg-1");
        Assert.Equal("ts-1", ts!.Id);
    }
}
