using AdminTaos.Pages.Auth;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

public class ComponentTests : BunitContext
{
    void Wire()
    {
        Services.AddSingleton<IDataService, InMemoryDataService>();
        Services.AddSingleton<IAuthClient, FakeAuthClient>();
        Services.AddScoped<AuthState>();
        Services.AddScoped<NavigationGuard>();
    }

    [Fact]
    public void Login_lists_five_seeded_test_accounts()
    {
        Wire();
        var cut = Render<Login>();
        cut.WaitForState(() => cut.FindAll(".card.quick").Count == 5);
        Assert.Contains("Manager", cut.Markup);
        Assert.Contains("En attente", cut.Markup);
        Assert.Contains("Refusé", cut.Markup);
    }

    [Fact]
    public void Stepper_increments_and_clamps_at_min()
    {
        var val = 1;
        var cut = Render<AdminTaos.Components.Stepper>(p => p
            .Add(s => s.Value, 1).Add(s => s.Min, 0)
            .Add(s => s.ValueChanged, (int v) => val = v));
        cut.FindAll("button")[1].Click(); // +
        Assert.Equal(2, val);
        cut.FindAll("button")[0].Click(); // -
        cut.FindAll("button")[0].Click(); // - (clamp at 0)
        Assert.Equal(0, val);
    }

    [Fact]
    public void Chrono_formats_elapsed_since_start()
    {
        var start = System.DateTime.Now.AddMinutes(-90).AddSeconds(-5);
        var cut = Render<AdminTaos.Components.Chrono>(p => p.Add(c => c.Since, start));
        Assert.Matches(@"01:3[01]:\d\d", cut.Find(".chrono").TextContent);
    }
}
