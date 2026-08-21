using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>
/// /m/team must show every account, whatever its type or status. Regression guard for the
/// filter that hid Admins and suspended accounts: MEmployeeDetail is the only place holding
/// "Rétrograder en Collaborateur" and "Réactiver", and it was reachable by URL only.
/// </summary>
public class TeamListTests : BunitContext
{
    InMemoryDataService Db()
    {
        var db = new InMemoryDataService();
        Services.AddSingleton<IDataService>(db);
        return db;
    }

    IRenderedComponent<AdminTaos.Pages.Manager.MTeam> Team() =>
        Render<AdminTaos.Pages.Manager.MTeam>();

    static string Rows(IRenderedComponent<AdminTaos.Pages.Manager.MTeam> cut) =>
        string.Join("\n", cut.FindAll("tbody tr").Select(r => r.TextContent));

    [Fact]
    public void Active_tab_lists_admins_alongside_collaborateurs()
    {
        Db();
        var cut = Team();
        var rows = Rows(cut);
        Assert.Contains("Hervé T.", rows);   // seeded Admin
        Assert.Contains("Marc D.", rows);    // seeded Collaborateur
    }

    [Fact]
    public void Admin_row_is_labelled_as_admin()
    {
        Db();
        var row = Team().FindAll("tbody tr").Single(r => r.TextContent.Contains("Hervé T."));
        Assert.Contains("Admin", row.TextContent);
        Assert.DoesNotContain("Collaborateur", row.TextContent);
    }

    [Fact]
    public void Collaborateur_row_shows_its_job_roles()
    {
        Db();
        var row = Team().FindAll("tbody tr").Single(r => r.TextContent.Contains("Marc D."));
        Assert.Contains("Collaborateur", row.TextContent);
        Assert.Contains("Serveur", row.TextContent);
    }

    [Fact]
    public void Suspended_tab_lists_rejected_accounts()
    {
        Db();
        var cut = Team();
        Assert.DoesNotContain("Tom V.", Rows(cut));   // hidden on the Actifs tab

        cut.FindAll(".row2 button").Single(b => b.TextContent.Contains("Suspendus")).Click();
        Assert.Contains("Tom V.", Rows(cut));
    }

    [Fact]
    public void Pending_tab_still_lists_only_pending_accounts()
    {
        Db();
        var cut = Team();
        cut.FindAll(".row2 button").Single(b => b.TextContent.Contains("attente")).Click();
        var rows = Rows(cut);
        Assert.Contains("Léa B.", rows);
        Assert.DoesNotContain("Marc D.", rows);
        Assert.DoesNotContain("Hervé T.", rows);
    }

    [Fact]
    public void Every_row_links_to_the_account_detail_page()
    {
        Db();
        var nav = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        var cut = Team();
        cut.FindAll("tbody tr").Single(r => r.TextContent.Contains("Hervé T.")).Click();
        Assert.EndsWith($"m/team/{SeedData.MgrId}", nav.Uri);
    }
}
