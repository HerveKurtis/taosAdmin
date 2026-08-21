using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>
/// L'écran de connexion doit montrer qu'il travaille : l'aller-retour Firebase peut durer
/// plusieurs secondes sur un réseau mobile, et un bouton inerte invite à cliquer en boucle.
/// </summary>
public class LoginProgressTests : BunitContext
{
    /// <summary>IAuthClient dont la connexion ne se termine que sur ordre du test.</summary>
    sealed class GatedAuthClient : IAuthClient
    {
        readonly TaskCompletionSource<string?> _gate;
        public int Attempts { get; private set; }
        public event Action<string?>? OnAuthChanged;

        public GatedAuthClient(TaskCompletionSource<string?> gate) => _gate = gate;

        public Task<string?> LoginAsync(string email, string password)
        {
            Attempts++;
            return _gate.Task;
        }

        public Task<string?> RegisterAsync(string e, string p) => Task.FromResult<string?>(null);
        public Task LogoutAsync() { OnAuthChanged?.Invoke(null); return Task.CompletedTask; }
        public Task<string?> GetCurrentUidAsync() => Task.FromResult<string?>(null);
        public Task InitializeAsync() => Task.CompletedTask;
    }

    GatedAuthClient Setup(TaskCompletionSource<string?> gate)
    {
        var db = new InMemoryDataService();
        var client = new GatedAuthClient(gate);
        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(new AuthState(client, db));
        return client;
    }

    static void Fill(IRenderedComponent<AdminTaos.Pages.Auth.Login> cut)
    {
        cut.Find("input[type=email]").Change("marc@taos.be");
        cut.Find("input[type=password]").Change("motdepasse");
    }

    [Fact]
    public void The_button_shows_progress_while_the_login_is_in_flight()
    {
        Setup(new TaskCompletionSource<string?>());
        var cut = Render<AdminTaos.Pages.Auth.Login>();
        Fill(cut);

        cut.Find("button.btn.primary").Click();

        Assert.Contains("Connexion…", cut.Markup);
        Assert.True(cut.Find("button.btn.primary").HasAttribute("disabled"));
        Assert.Single(cut.FindAll(".btn-spinner"));
    }

    [Fact]
    public void The_fields_are_locked_while_the_login_is_in_flight()
    {
        Setup(new TaskCompletionSource<string?>());
        var cut = Render<AdminTaos.Pages.Auth.Login>();
        Fill(cut);

        cut.Find("button.btn.primary").Click();

        Assert.True(cut.Find("input[type=email]").HasAttribute("disabled"));
        Assert.True(cut.Find("input[type=password]").HasAttribute("disabled"));
    }

    [Fact]
    public void A_second_click_does_not_start_a_second_login()
    {
        var client = Setup(new TaskCompletionSource<string?>());
        var cut = Render<AdminTaos.Pages.Auth.Login>();
        Fill(cut);

        cut.Find("button.btn.primary").Click();
        cut.Find("button.btn.primary").Click();

        Assert.Equal(1, client.Attempts);
    }

    [Fact]
    public void The_form_is_usable_again_after_a_failed_login()
    {
        var gate = new TaskCompletionSource<string?>();
        Setup(gate);
        var cut = Render<AdminTaos.Pages.Auth.Login>();
        Fill(cut);

        cut.Find("button.btn.primary").Click();
        gate.SetResult(null);   // Firebase refuse les identifiants

        cut.WaitForAssertion(() => Assert.Contains("Identifiants invalides", cut.Markup));
        Assert.DoesNotContain("Connexion…", cut.Markup);
        Assert.False(cut.Find("button.btn.primary").HasAttribute("disabled"));
        Assert.False(cut.Find("input[type=email]").HasAttribute("disabled"));
    }

    [Fact]
    public void An_empty_form_is_refused_without_ever_showing_progress()
    {
        var client = Setup(new TaskCompletionSource<string?>());
        var cut = Render<AdminTaos.Pages.Auth.Login>();

        cut.Find("button.btn.primary").Click();

        Assert.Contains("Email et mot de passe requis", cut.Markup);
        Assert.DoesNotContain("Connexion…", cut.Markup);
        Assert.Equal(0, client.Attempts);
    }
}
