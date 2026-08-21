using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>
/// Covers the staffing chain: an event's effectif is a planning target, never a gate.
/// Regression guard for two bugs: MEventEdit never rendered a role-needs editor (so RoleNeeds
/// was always empty), and MAssignStaff derived its whole rendering from RoleNeeds (so it
/// rendered a blank page).
/// </summary>
public class EventStaffingTests : BunitContext
{
    InMemoryDataService Db()
    {
        var db = new InMemoryDataService();
        Services.AddSingleton<IDataService>(db);
        return db;
    }

    // ---------- MEventEdit: the effectif must be editable ----------

    [Fact]
    public void MEventEdit_renders_a_role_need_row_for_every_job_role()
    {
        Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>();
        Assert.Equal(2, cut.FindAll(".role-need").Count);
        Assert.NotNull(cut.Find($".role-need[data-role-id='{SeedData.RoleServer}']"));
        Assert.NotNull(cut.Find($".role-need[data-role-id='{SeedData.RoleHost}']"));
    }

    [Fact]
    public async Task MEventEdit_persists_the_effectif_entered_by_the_admin()
    {
        var db = Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>();

        cut.FindAll(".field input")[0].Change("Soirée test");

        var plus = cut.Find($".role-need[data-role-id='{SeedData.RoleServer}'] .stepper button:last-child");
        plus.Click(); plus.Click(); plus.Click();

        cut.Find("button.btn.primary.block").Click();

        var saved = (await db.GetEventsAsync()).Single(e => e.Name == "Soirée test");
        var need = Assert.Single(saved.RoleNeeds);
        Assert.Equal(SeedData.RoleServer, need.JobRoleId);
        Assert.Equal(3, need.CountNeeded);
    }

    [Fact]
    public void MEventEdit_does_not_ask_for_an_hourly_rate()
    {
        Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>(p => p.Add(x => x.Id, "evt-gala"));
        Assert.Empty(cut.FindAll("input.rate"));
        Assert.DoesNotContain("aux horaire", cut.Markup);
    }

    [Fact]
    public async Task MEventEdit_preserves_rates_it_no_longer_edits()
    {
        var db = Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>(p => p.Add(x => x.Id, "evt-gala"));

        Assert.Contains("4", cut.Find($".role-need[data-role-id='{SeedData.RoleServer}'] .stepper b").TextContent);

        cut.Find("button.btn.primary.block").Click();

        var saved = (await db.GetEventAsync("evt-gala"))!;
        Assert.Equal(2, saved.RoleNeeds.Count);
        Assert.Equal(14m, saved.RoleNeeds.Single(n => n.JobRoleId == SeedData.RoleServer).HourlyRate);
    }

    // ---------- MAssignStaff: never blocked, never blank ----------

    [Fact]
    public async Task MAssignStaff_falls_back_to_every_staffable_role_when_no_effectif_is_defined()
    {
        var db = Db();
        var e = await db.CreateEventAsync(new ServiceEvent { Name = "Sans effectif" });

        var cut = Render<AdminTaos.Pages.Manager.MAssignStaff>(p => p.Add(x => x.Id, e.Id));

        Assert.Contains("Serveur", cut.Markup);
        Assert.Contains("Hôtesse", cut.Markup);
        Assert.Equal(2, cut.FindAll("button.btn.primary").Count);   // Marc + Sarah, both assignable
    }

    [Fact]
    public async Task MAssignStaff_assigns_without_any_effectif_defined()
    {
        var db = Db();
        var e = await db.CreateEventAsync(new ServiceEvent { Name = "Sans effectif" });

        var cut = Render<AdminTaos.Pages.Manager.MAssignStaff>(p => p.Add(x => x.Id, e.Id));
        cut.FindAll("button.btn.primary").First().Click();

        Assert.Single(await db.GetAssignmentsForEventAsync(e.Id));
    }

    [Fact]
    public void MAssignStaff_shows_progress_against_the_target_when_an_effectif_exists()
    {
        Db();
        var cut = Render<AdminTaos.Pages.Manager.MAssignStaff>(p => p.Add(x => x.Id, "evt-gala"));

        // Gala needs 4 Serveurs and 2 Hôtesses; Marc is the only one assigned so far.
        Assert.Contains("1 / 4", cut.Markup);
        Assert.Contains("0 / 2", cut.Markup);
    }

    [Fact]
    public async Task MAssignStaff_assigns_an_available_collaborateur()
    {
        var db = Db();
        var cut = Render<AdminTaos.Pages.Manager.MAssignStaff>(p => p.Add(x => x.Id, "evt-gala"));

        cut.FindAll("button.btn.primary").Single().Click();

        var asgs = await db.GetAssignmentsForEventAsync("evt-gala");
        var created = Assert.Single(asgs, a => a.AccountId == SeedData.EmpActiveHost);
        Assert.Equal(SeedData.RoleHost, created.JobRoleId);
        Assert.Equal(AssignmentStatus.Confirmed, created.Status);
        Assert.Contains("Déjà assigné", cut.Markup);
    }

    [Fact]
    public async Task MAssignStaff_points_to_the_team_page_when_nobody_holds_a_needed_role()
    {
        var db = Db();
        var role = await db.CreateJobRoleAsync(new JobRole { Name = "Barman" });
        var e = await db.CreateEventAsync(new ServiceEvent {
            Name = "Bar", RoleNeeds = new() { new() { JobRoleId = role.Id, CountNeeded = 2 } } });

        var cut = Render<AdminTaos.Pages.Manager.MAssignStaff>(p => p.Add(x => x.Id, e.Id));

        Assert.Contains("Aucun collaborateur", cut.Markup);
        Assert.Contains("m/team", cut.Markup);
    }

    [Fact]
    public async Task MAssignStaff_explains_itself_when_no_collaborateur_holds_any_role()
    {
        var db = Db();
        foreach (var a in (await db.GetAccountsAsync()).Where(a => a.Type == AccountType.Employee))
        {
            a.JobRoleIds = new();
            await db.UpdateAccountAsync(a);
        }
        var e = await db.CreateEventAsync(new ServiceEvent { Name = "Sans effectif" });

        var cut = Render<AdminTaos.Pages.Manager.MAssignStaff>(p => p.Add(x => x.Id, e.Id));

        Assert.Contains("Aucun collaborateur", cut.Markup);
        Assert.Contains("m/team", cut.Markup);
    }

    [Fact]
    public void MAssignStaff_surfaces_a_write_failure_instead_of_failing_silently()
    {
        Services.AddSingleton<IDataService>(new ThrowingDataService());

        var cut = Render<AdminTaos.Pages.Manager.MAssignStaff>(p => p.Add(x => x.Id, "evt-gala"));
        cut.FindAll("button.btn.primary").First().Click();

        Assert.Contains("Échec de l'assignation", cut.Markup);
    }

    /// <summary>Reads like the real thing, refuses every assignment write — stands in for a Firestore rules denial.</summary>
    sealed class ThrowingDataService : InMemoryDataService, IDataService
    {
        Task<Assignment> IDataService.CreateAssignmentAsync(Assignment a)
            => throw new InvalidOperationException("Missing or insufficient permissions.");
    }
}
