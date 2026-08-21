using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>Retirer quelqu'un d'un event après une erreur d'assignation.</summary>
public class UnassignTests : BunitContext
{
    InMemoryDataService Db()
    {
        var db = new InMemoryDataService();
        Services.AddSingleton<IDataService>(db);
        return db;
    }

    IRenderedComponent<AdminTaos.Pages.Manager.MEventDetail> Sheet(string id = "evt-gala") =>
        Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, id));

    [Fact]
    public async Task The_data_service_can_delete_an_assignment()
    {
        var db = new InMemoryDataService();
        await db.DeleteAssignmentAsync("asg-1");
        Assert.DoesNotContain(await db.GetAssignmentsAsync(), a => a.Id == "asg-1");
    }

    [Fact]
    public async Task Removing_someone_asks_for_confirmation_first()
    {
        var db = Db();
        var cut = Sheet();

        cut.Find("button.asg-remove").Click();

        Assert.Contains("Retirer Marc D.", cut.Markup);
        Assert.NotEmpty(cut.FindAll("button.asg-remove-confirm"));
        // rien n'est supprimé tant que la confirmation n'est pas donnée
        Assert.Contains(await db.GetAssignmentsForEventAsync("evt-gala"), a => a.Id == "asg-1");
    }

    [Fact]
    public async Task Confirming_removes_the_assignment()
    {
        var db = Db();
        var cut = Sheet();

        cut.Find("button.asg-remove").Click();
        cut.Find("button.asg-remove-confirm").Click();

        Assert.DoesNotContain(await db.GetAssignmentsForEventAsync("evt-gala"), a => a.Id == "asg-1");
        Assert.Contains("Personne pour l'instant", cut.Markup);
    }

    [Fact]
    public async Task Cancelling_leaves_the_assignment_alone()
    {
        var db = Db();
        var cut = Sheet();

        cut.Find("button.asg-remove").Click();
        cut.Find("button.asg-remove-cancel").Click();

        Assert.Contains(await db.GetAssignmentsForEventAsync("evt-gala"), a => a.Id == "asg-1");
        Assert.Empty(cut.FindAll("button.asg-remove-confirm"));
    }

    [Fact]
    public async Task Someone_who_already_started_their_shift_cannot_be_removed()
    {
        var db = Db();
        var ts = (await db.GetTimesheetAsync("ts-1"))!;   // rattachée à asg-1
        ts.StartedAt = DateTime.Now.AddHours(-1);
        ts.Status = TimesheetStatus.InProgress;
        await db.UpdateTimesheetAsync(ts);

        var cut = Sheet();
        cut.Find("button.asg-remove").Click();
        cut.Find("button.asg-remove-confirm").Click();

        Assert.Contains("a déjà démarré son service", cut.Markup);
        Assert.Contains(await db.GetAssignmentsForEventAsync("evt-gala"), a => a.Id == "asg-1");
    }

    [Fact]
    public async Task An_untouched_timesheet_does_not_block_the_removal()
    {
        var db = Db();
        // ts-1 existe mais n'a jamais été démarrée (statut NotStarted, StartedAt null)
        var cut = Sheet();

        cut.Find("button.asg-remove").Click();
        cut.Find("button.asg-remove-confirm").Click();

        Assert.DoesNotContain(await db.GetAssignmentsForEventAsync("evt-gala"), a => a.Id == "asg-1");
    }
}
