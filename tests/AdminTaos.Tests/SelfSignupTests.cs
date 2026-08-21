using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>
/// Self-signup on an open event. Regression guard for RequestJoin() calling
/// <c>u.JobRoleIds.First()</c>, which threw for any collaborateur without a rôle métier.
/// </summary>
public class SelfSignupTests : BunitContext
{
    async Task<(InMemoryDataService Db, AuthState Auth)> SignIn(Account who)
    {
        var db = new InMemoryDataService();
        await db.CreateAccountAsync(who);
        var fake = new FakeAuthClient();
        fake.PreRegister(who.Id, who.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(who.Email, "pwd");

        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(auth);
        return (db, auth);
    }

    static Account Collaborateur(params string[] jobRoleIds) => new() {
        Id = "acc-test", FullName = "Test C.", Email = "test@taos.be",
        Type = AccountType.Employee, Status = AccountStatus.Active,
        JobRoleIds = jobRoleIds.ToList() };

    [Fact]
    public async Task Collaborateur_without_a_job_role_cannot_request_and_is_told_why()
    {
        var (db, _) = await SignIn(Collaborateur());

        // evt-cocktail is the seeded open-for-signup event.
        var cut = Render<AdminTaos.Pages.Employee.EEventDetail>(p => p.Add(x => x.Id, "evt-cocktail"));

        Assert.Empty(cut.FindAll("button.btn.primary.block"));
        Assert.Contains("Aucun de tes rôles métier", cut.Markup);
    }

    [Fact]
    public async Task Collaborateur_with_a_matching_job_role_requests_on_that_role()
    {
        var acc = Collaborateur(SeedData.RoleServer);
        var (db, _) = await SignIn(acc);

        var cut = Render<AdminTaos.Pages.Employee.EEventDetail>(p => p.Add(x => x.Id, "evt-cocktail"));
        cut.Find("button.btn.primary.block").Click();

        var asg = Assert.Single(await db.GetAssignmentsForAccountAsync(acc.Id));
        Assert.Equal(SeedData.RoleServer, asg.JobRoleId);
        Assert.Equal(AssignmentStatus.PendingApproval, asg.Status);
        Assert.Equal(AssignmentSource.SelfRequest, asg.Source);
    }

    [Fact]
    public async Task Collaborateur_whose_roles_are_not_needed_on_the_event_cannot_request()
    {
        var acc = Collaborateur(SeedData.RoleHost);   // cocktail only needs RoleServer
        var (db, _) = await SignIn(acc);

        var cut = Render<AdminTaos.Pages.Employee.EEventDetail>(p => p.Add(x => x.Id, "evt-cocktail"));

        Assert.Empty(cut.FindAll("button.btn.primary.block"));
        Assert.Contains("Aucun de tes rôles métier", cut.Markup);
        Assert.Empty(await db.GetAssignmentsForAccountAsync(acc.Id));
    }
}
