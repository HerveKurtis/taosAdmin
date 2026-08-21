using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

public class ProfileEditTests : BunitContext
{
    async Task<InMemoryDataService> SignIn(Account who)
    {
        var db = new InMemoryDataService();
        await db.CreateAccountAsync(who);
        var fake = new FakeAuthClient();
        fake.PreRegister(who.Id, who.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(who.Email, "pwd");

        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(auth);
        Services.AddSingleton<IStorageClient>(new FakeStorageClient());
        return db;
    }

    static Account Collaborateur() => new() {
        Id = "acc-p", FullName = "Test P.", Email = "p@taos.be",
        Type = AccountType.Employee, Status = AccountStatus.Active,
        JobRoleIds = new() { SeedData.RoleServer } };

    static Account Admin() => new() {
        Id = "acc-adm", FullName = "Admin A.", Email = "adm@taos.be",
        Type = AccountType.Manager, Status = AccountStatus.Active };

    [Fact]
    public async Task Collaborateur_saves_phone_address_and_iban()
    {
        var db = await SignIn(Collaborateur());
        var cut = Render<AdminTaos.Pages.Employee.EProfile>();

        cut.Find("input.pf-phone").Change("0470 12 34 56");
        cut.Find("input.pf-address").Change("Rue des Bouchers 12, 1000 Bruxelles");
        cut.Find("input.pf-iban").Change("be68 5390 0754 7034");
        cut.Find("button.pf-save").Click();

        var saved = (await db.GetAccountAsync("acc-p"))!;
        Assert.Equal("0470 12 34 56", saved.Phone);
        Assert.Equal("Rue des Bouchers 12, 1000 Bruxelles", saved.PostalAddress);
        Assert.Equal("BE68539007547034", saved.Iban);
    }

    [Fact]
    public async Task Blank_fields_are_stored_as_null_not_empty_strings()
    {
        var who = Collaborateur();
        who.Phone = "0470"; who.PostalAddress = "X"; who.Iban = "BE68539007547034";
        var db = await SignIn(who);
        var cut = Render<AdminTaos.Pages.Employee.EProfile>();

        cut.Find("input.pf-phone").Change("   ");
        cut.Find("input.pf-address").Change("");
        cut.Find("input.pf-iban").Change("");
        cut.Find("button.pf-save").Click();

        var saved = (await db.GetAccountAsync("acc-p"))!;
        Assert.Null(saved.Phone);
        Assert.Null(saved.PostalAddress);
        Assert.Null(saved.Iban);
    }

    [Fact]
    public async Task A_malformed_iban_is_refused_and_nothing_is_saved()
    {
        var db = await SignIn(Collaborateur());
        var cut = Render<AdminTaos.Pages.Employee.EProfile>();

        cut.Find("input.pf-phone").Change("0470 12 34 56");
        cut.Find("input.pf-iban").Change("PAS-UN-IBAN");
        cut.Find("button.pf-save").Click();

        Assert.Contains("IBAN invalide", cut.Markup);
        var saved = (await db.GetAccountAsync("acc-p"))!;
        Assert.Null(saved.Iban);
        Assert.Null(saved.Phone);
    }

    [Fact]
    public async Task An_admin_has_the_same_panel()
    {
        var db = await SignIn(Admin());
        var cut = Render<AdminTaos.Pages.Manager.MProfile>();

        cut.Find("input.pf-phone").Change("0499 99 99 99");
        cut.Find("button.pf-save").Click();

        Assert.Equal("0499 99 99 99", (await db.GetAccountAsync("acc-adm"))!.Phone);
    }

    [Fact]
    public async Task Existing_values_are_prefilled()
    {
        var who = Collaborateur();
        who.Phone = "0470 11 22 33";
        who.Iban = "BE68539007547034";
        await SignIn(who);
        var cut = Render<AdminTaos.Pages.Employee.EProfile>();

        Assert.Equal("0470 11 22 33", cut.Find("input.pf-phone").GetAttribute("value"));
        Assert.Equal("BE68539007547034", cut.Find("input.pf-iban").GetAttribute("value"));
    }

    [Fact]
    public async Task Admin_sees_the_contact_details_of_a_collaborateur()
    {
        var db = await SignIn(Admin());
        var target = Collaborateur();
        target.Phone = "0470 12 34 56";
        target.PostalAddress = "Rue des Bouchers 12, 1000 Bruxelles";
        target.Iban = "BE68539007547034";
        await db.CreateAccountAsync(target);

        var cut = Render<AdminTaos.Pages.Manager.MEmployeeDetail>(p => p.Add(x => x.Id, target.Id));

        Assert.Contains("0470 12 34 56", cut.Markup);
        Assert.Contains("Rue des Bouchers 12", cut.Markup);
        Assert.Contains("BE68539007547034", cut.Markup);
    }

    [Fact]
    public async Task Missing_contact_details_show_a_dash_not_an_empty_row()
    {
        var db = await SignIn(Admin());
        var target = Collaborateur();
        await db.CreateAccountAsync(target);

        var cut = Render<AdminTaos.Pages.Manager.MEmployeeDetail>(p => p.Add(x => x.Id, target.Id));
        var row = cut.FindAll(".field").Single(f => f.TextContent.Contains("IBAN"));
        Assert.Contains("\u2014", row.TextContent);
    }
}
