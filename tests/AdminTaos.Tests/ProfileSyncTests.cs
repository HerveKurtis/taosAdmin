using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>
/// /profiles porte ce qu'un collègue a besoin de savoir — nom, téléphone, photo — et rien
/// d'autre. Les données de paie restent dans /accounts, fermé à tout le monde sauf soi et
/// les admins. Ces tests verrouillent cette séparation et la synchronisation entre les deux.
/// </summary>
public class ProfileSyncTests : BunitContext
{
    [Fact]
    public void A_profile_never_carries_payroll_data()
    {
        var names = typeof(Profile).GetProperties().Select(p => p.Name).ToList();
        Assert.Contains("FullName", names);
        Assert.Contains("Phone", names);
        Assert.Contains("PhotoUrl", names);
        Assert.DoesNotContain("Iban", names);
        Assert.DoesNotContain("PostalAddress", names);
        Assert.DoesNotContain("Email", names);
    }

    [Fact]
    public void A_profile_is_built_from_an_account()
    {
        var a = new Account { Id = "u1", FullName = "Marc D.", Phone = "0470", PhotoUrl = "p.jpg",
                              Iban = "BE68539007547034", Email = "m@taos.be" };
        var p = Profile.From(a);
        Assert.Equal("u1", p.Id);
        Assert.Equal("Marc D.", p.FullName);
        Assert.Equal("0470", p.Phone);
        Assert.Equal("p.jpg", p.PhotoUrl);
    }

    [Fact]
    public async Task Registering_publishes_a_profile()
    {
        var db = new InMemoryDataService();
        var auth = new AuthState(new FakeAuthClient(), db);

        await auth.RegisterAsync("Nouvelle Recrue", "recrue@taos.be", "pwd", new());

        var me = auth.CurrentUser!;
        var p = (await db.GetProfilesAsync()).Single(x => x.Id == me.Id);
        Assert.Equal("Nouvelle Recrue", p.FullName);
    }

    [Fact]
    public async Task Logging_in_refreshes_the_profile_of_a_legacy_account()
    {
        var db = new InMemoryDataService();
        await db.DeleteProfileAsync(SeedData.EmpActiveServer);   // compte antérieur à /profiles

        var fake = new FakeAuthClient();
        fake.PreRegister(SeedData.EmpActiveServer, "marc@taos.be", "pwd");
        var auth = new AuthState(fake, db);

        await auth.LoginAsync("marc@taos.be", "pwd");

        Assert.Contains(await db.GetProfilesAsync(), p => p.Id == SeedData.EmpActiveServer);
    }

    [Fact]
    public async Task Saving_the_profile_panel_publishes_the_new_phone()
    {
        var db = new InMemoryDataService();
        var me = (await db.GetAccountAsync(SeedData.EmpActiveServer))!;
        var fake = new FakeAuthClient();
        fake.PreRegister(me.Id, me.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(me.Email, "pwd");

        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(auth);
        Services.AddSingleton<IStorageClient>(new FakeStorageClient());

        var cut = Render<AdminTaos.Pages.Employee.EProfile>();
        cut.Find("input.pf-phone").Change("0470 12 34 56");
        cut.Find("input.pf-iban").Change("BE68539007547034");
        cut.Find("button.pf-save").Click();

        var p = (await db.GetProfilesAsync()).Single(x => x.Id == me.Id);
        Assert.Equal("0470 12 34 56", p.Phone);
    }

    [Fact]
    public async Task The_team_page_backfills_profiles_for_older_accounts()
    {
        var db = new InMemoryDataService();
        await db.DeleteProfileAsync(SeedData.EmpActiveHost);
        Services.AddSingleton<IDataService>(db);

        Render<AdminTaos.Pages.Manager.MTeam>();

        Assert.Contains(await db.GetProfilesAsync(), p => p.Id == SeedData.EmpActiveHost);
    }
}
