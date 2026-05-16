using AdminTaos.Pages.Auth;
using AdminTaos.Services;
using Blazored.LocalStorage;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

public class ComponentTests : BunitContext
{
    void Wire()
    {
        Services.AddSingleton<IDataService, InMemoryDataService>();
        Services.AddSingleton<ILocalStorageService>(new FakeLocalStorage());
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
}
