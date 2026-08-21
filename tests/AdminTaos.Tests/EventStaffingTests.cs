using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>
/// Covers the staffing chain: define an event's role needs → assign staff onto those roles.
/// Regression guard for the bug where MEventEdit never rendered a role-needs editor, so every
/// event was created with RoleNeeds empty and MAssignStaff rendered a blank page.
/// </summary>
public class EventStaffingTests : BunitContext
{
    InMemoryDataService Db()
    {
        var db = new InMemoryDataService();
        Services.AddSingleton<IDataService>(db);
        return db;
    }

    // ---------- MEventEdit: role needs must be editable ----------

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
    public async Task MEventEdit_persists_the_role_needs_entered_by_the_admin()
    {
        var db = Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>();

        cut.FindAll(".field input")[0].Change("Soirée test");

        var row = $".role-need[data-role-id='{SeedData.RoleServer}']";
        var plus = cut.Find($"{row} .stepper button:last-child");
        plus.Click(); plus.Click(); plus.Click();          // CountNeeded = 3
        cut.Find($"{row} input.rate").Change("16.5");      // HourlyRate = 16.5

        cut.Find("button.btn.primary.block").Click();

        var saved = (await db.GetEventsAsync()).Single(e => e.Name == "Soirée test");
        var need = Assert.Single(saved.RoleNeeds);
        Assert.Equal(SeedData.RoleServer, need.JobRoleId);
        Assert.Equal(3, need.CountNeeded);
        Assert.Equal(16.5m, need.HourlyRate);
    }

    [Fact]
    public void MEventEdit_accepts_a_comma_decimal_separator_for_the_rate()
    {
        Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>();
        var row = $".role-need[data-role-id='{SeedData.RoleServer}']";
        cut.Find($"{row} .stepper button:last-child").Click();
        cut.Find($"{row} input.rate").Change("16,5");
        Assert.Contains("16.5", cut.Find($"{row} input.rate").GetAttribute("value"));
    }

    [Fact]
    public async Task MEventEdit_keeps_existing_role_needs_when_editing()
    {
        var db = Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>(p => p.Add(x => x.Id, "evt-gala"));

        Assert.Contains("4", cut.Find($".role-need[data-role-id='{SeedData.RoleServer}'] .stepper b").TextContent);

        cut.Find("button.btn.primary.block").Click();
        var saved = (await db.GetEventAsync("evt-gala"))!;
        Assert.Equal(2, saved.RoleNeeds.Count);
    }

    // ---------- MAssignStaff: never a blank page ----------

    [Fact]
    public async Task MAssignStaff_explains_itself_when_the_event_has_no_role_needs()
    {
        var db = Db();
        var e = await db.CreateEventAsync(new ServiceEvent { Name = "Sans effectif" });

        var cut = Render<AdminTaos.Pages.Manager.MAssignStaff>(p => p.Add(x => x.Id, e.Id));

        Assert.Contains("Aucun rôle défini", cut.Markup);
        Assert.Contains($"m/events/{e.Id}/edit", cut.Markup);
    }

    [Fact]
    public async Task MAssignStaff_assigns_an_available_collaborateur()
    {
        var db = Db();
        var cut = Render<AdminTaos.Pages.Manager.MAssignStaff>(p => p.Add(x => x.Id, "evt-gala"));

        // Marc is already on RoleServer for the gala; Sarah (RoleHost) is the one still assignable.
        var btn = cut.FindAll("button.btn.primary").Single();
        btn.Click();

        var asgs = await db.GetAssignmentsForEventAsync("evt-gala");
        var created = Assert.Single(asgs, a => a.AccountId == SeedData.EmpActiveHost);
        Assert.Equal(SeedData.RoleHost, created.JobRoleId);
        Assert.Equal(AssignmentStatus.Confirmed, created.Status);
        Assert.Contains("Déjà assigné", cut.Markup);
    }

    [Fact]
    public async Task MAssignStaff_points_to_the_team_page_when_nobody_holds_the_role()
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
    public async Task MAssignStaff_surfaces_a_write_failure_instead_of_failing_silently()
    {
        var db = new ThrowingDataService();
        Services.AddSingleton<IDataService>(db);

        var cut = Render<AdminTaos.Pages.Manager.MAssignStaff>(p => p.Add(x => x.Id, "evt-gala"));
        cut.FindAll("button.btn.primary").First().Click();

        Assert.Contains("Échec de l'assignation", cut.Markup);
        await Task.CompletedTask;
    }

    /// <summary>Reads like the real thing, refuses every assignment write — stands in for a Firestore rules denial.</summary>
    sealed class ThrowingDataService : InMemoryDataService, IDataService
    {
        Task<Assignment> IDataService.CreateAssignmentAsync(Assignment a)
            => throw new InvalidOperationException("Missing or insufficient permissions.");
    }
}
