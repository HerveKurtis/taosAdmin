using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>
/// Reactivation of a suspended account. Now that /m/team lists suspended members, this button
/// is reachable for the first time — it must not silently downgrade an Admin to a Collaborateur
/// with no rôle métier.
/// </summary>
public class AccountLifecycleTests : BunitContext
{
    async Task<InMemoryDataService> SignedInAdmin()
    {
        var db = new InMemoryDataService();
        var fake = new FakeAuthClient();
        fake.PreRegister(SeedData.MgrId, "manager@taos.be", "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync("manager@taos.be", "pwd");
        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(auth);
        return db;
    }

    IRenderedComponent<AdminTaos.Pages.Manager.MEmployeeDetail> Detail(string id) =>
        Render<AdminTaos.Pages.Manager.MEmployeeDetail>(p => p.Add(x => x.Id, id));

    [Fact]
    public async Task Reactivating_a_suspended_admin_keeps_them_admin()
    {
        var db = await SignedInAdmin();
        var admin = new Account { Id = "acc-susp-admin", FullName = "Ex Admin", Email = "ex@taos.be",
            Type = AccountType.Manager, Status = AccountStatus.Rejected };
        await db.CreateAccountAsync(admin);

        var cut = Detail(admin.Id);
        cut.FindAll("button").Single(b => b.TextContent.Contains("Réactiver")).Click();

        var after = (await db.GetAccountAsync(admin.Id))!;
        Assert.Equal(AccountStatus.Active, after.Status);
        Assert.Equal(AccountType.Manager, after.Type);
    }

    [Fact]
    public async Task Reactivating_a_suspended_collaborateur_restores_their_job_roles()
    {
        var db = await SignedInAdmin();
        var cut = Detail(SeedData.EmpRejected);   // Tom V., suspended, held RoleHost
        cut.FindAll("button").Single(b => b.TextContent.Contains("Réactiver")).Click();

        var after = (await db.GetAccountAsync(SeedData.EmpRejected))!;
        Assert.Equal(AccountStatus.Active, after.Status);
        Assert.Equal(AccountType.Employee, after.Type);
        Assert.Equal(new[] { SeedData.RoleHost }, after.JobRoleIds);
    }

    [Fact]
    public async Task Reactivating_a_collaborateur_without_a_job_role_is_refused()
    {
        var db = await SignedInAdmin();
        var orphan = new Account { Id = "acc-susp-orphan", FullName = "Sans rôle", Email = "sr@taos.be",
            Type = AccountType.Employee, Status = AccountStatus.Rejected };
        await db.CreateAccountAsync(orphan);

        var cut = Detail(orphan.Id);
        cut.FindAll("button").Single(b => b.TextContent.Contains("Réactiver")).Click();

        Assert.Equal(AccountStatus.Rejected, (await db.GetAccountAsync(orphan.Id))!.Status);
        Assert.Contains("au moins un rôle métier", cut.Markup);
    }

    [Fact]
    public async Task A_suspended_collaborateur_can_be_given_a_different_role_on_reactivation()
    {
        var db = await SignedInAdmin();
        var cut = Detail(SeedData.EmpRejected);   // holds RoleHost

        cut.FindAll("button.card").Single(b => b.TextContent.Contains("Serveur")).Click();
        cut.FindAll("button").Single(b => b.TextContent.Contains("Réactiver")).Click();

        var after = (await db.GetAccountAsync(SeedData.EmpRejected))!;
        Assert.Equal(2, after.JobRoleIds.Count);
        Assert.Contains(SeedData.RoleServer, after.JobRoleIds);
    }
}
