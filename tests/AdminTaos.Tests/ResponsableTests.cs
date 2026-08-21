using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

public class ResponsableTests : BunitContext
{
    InMemoryDataService Db()
    {
        var db = new InMemoryDataService();
        Services.AddSingleton<IDataService>(db);
        return db;
    }

    [Fact]
    public void New_models_carry_safe_defaults()
    {
        Assert.Null(new ServiceEvent().ResponsableAccountId);
        Assert.Equal(PresenceStatus.Expected, new Assignment().Presence);
        Assert.Equal(3, System.Enum.GetValues<PresenceStatus>().Length);
    }

    [Fact]
    public async Task MEventDetail_names_the_responsable_with_their_phone()
    {
        var db = Db();
        var who = (await db.GetAccountAsync(SeedData.EmpActiveServer))!;
        who.Phone = "0470 12 34 56";
        await db.UpdateAccountAsync(who);

        var e = (await db.GetEventAsync("evt-gala"))!;
        e.ResponsableAccountId = who.Id;
        await db.UpdateEventAsync(e);

        var cut = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-gala"));

        Assert.Contains("Responsable du jour", cut.Markup);
        Assert.Contains("Marc D.", cut.Markup);
        Assert.Contains("0470 12 34 56", cut.Markup);
    }

    [Fact]
    public void MEventDetail_falls_back_to_the_legacy_contact_when_no_responsable_is_set()
    {
        Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-gala"));

        Assert.Contains("Julie", cut.Markup);
        Assert.DoesNotContain("Responsable du jour", cut.Markup);
    }

    [Fact]
    public void The_search_filters_candidates_by_name_and_email()
    {
        Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>(p => p.Add(x => x.Id, "evt-gala"));

        cut.Find("input.resp-search").Input("marc");
        Assert.Single(cut.FindAll("button.resp-option"));
        Assert.Contains("Marc D.", cut.Find("button.resp-option").TextContent);

        cut.Find("input.resp-search").Input("sarah@taos.be");
        Assert.Single(cut.FindAll("button.resp-option"));
        Assert.Contains("Sarah K.", cut.Find("button.resp-option").TextContent);
    }

    [Fact]
    public void Suspended_and_pending_accounts_are_not_selectable()
    {
        Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>(p => p.Add(x => x.Id, "evt-gala"));

        cut.Find("input.resp-search").Input("Tom");
        Assert.Empty(cut.FindAll("button.resp-option"));

        cut.Find("input.resp-search").Input("Léa");
        Assert.Empty(cut.FindAll("button.resp-option"));
    }

    [Fact]
    public void An_admin_is_selectable_as_responsable()
    {
        Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>(p => p.Add(x => x.Id, "evt-gala"));

        cut.Find("input.resp-search").Input("Hervé");
        Assert.Single(cut.FindAll("button.resp-option"));
    }

    [Fact]
    public async Task Picking_a_candidate_then_saving_persists_the_responsable()
    {
        var db = Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>(p => p.Add(x => x.Id, "evt-gala"));

        cut.Find("input.resp-search").Input("marc");
        cut.Find($"button.resp-option[data-account-id='{SeedData.EmpActiveServer}']").Click();
        cut.Find("button.btn.primary.block").Click();

        Assert.Equal(SeedData.EmpActiveServer, (await db.GetEventAsync("evt-gala"))!.ResponsableAccountId);
    }

    [Fact]
    public async Task Clearing_the_responsable_puts_the_field_back_to_null()
    {
        var db = Db();
        var e = (await db.GetEventAsync("evt-gala"))!;
        e.ResponsableAccountId = SeedData.EmpActiveServer;
        await db.UpdateEventAsync(e);

        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>(p => p.Add(x => x.Id, "evt-gala"));
        Assert.Contains("Marc D.", cut.Markup);

        cut.Find("button.resp-clear").Click();
        cut.Find("button.btn.primary.block").Click();

        Assert.Null((await db.GetEventAsync("evt-gala"))!.ResponsableAccountId);
    }

    [Fact]
    public void The_on_site_contact_text_field_is_gone()
    {
        Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>(p => p.Add(x => x.Id, "evt-gala"));
        Assert.DoesNotContain("Contact sur place", cut.Markup);
    }

    [Fact]
    public void MEventDetail_shows_the_presence_tally()
    {
        Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-gala"));

        // Le gala compte une assignation confirmée (Marc), personne n'est encore pointé.
        Assert.Contains("0 présent · 0 absent · 1 attendu", cut.Find(".presence-tally").TextContent);
    }

    [Fact]
    public async Task MEventDetail_tally_follows_the_pointing()
    {
        var db = Db();
        var marc = (await db.GetAssignmentsForEventAsync("evt-gala"))
            .Single(a => a.AccountId == SeedData.EmpActiveServer);
        marc.Presence = PresenceStatus.Present;
        await db.UpdateAssignmentAsync(marc);

        var cut = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-gala"));

        Assert.Contains("1 présent · 0 absent · 0 attendu", cut.Find(".presence-tally").TextContent);
    }

    [Fact]
    public async Task MEventDetail_hides_the_tally_when_nobody_is_assigned()
    {
        var db = Db();
        var e = await db.CreateEventAsync(new ServiceEvent { Name = "Vide" });

        var cut = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, e.Id));

        Assert.Empty(cut.FindAll(".presence-tally"));
    }
}
