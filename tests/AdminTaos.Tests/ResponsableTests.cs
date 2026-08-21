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
}
