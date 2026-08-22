using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>
/// IsOpenForSignup n'était écrit qu'une fois, à la création d'un event : tout event naissait
/// ouvert aux candidatures spontanées et l'admin ne pouvait plus jamais le fermer.
/// </summary>
public class SignupToggleTests : BunitContext
{
    InMemoryDataService Db()
    {
        var db = new InMemoryDataService();
        Services.AddSingleton<IDataService>(db);
        return db;
    }

    [Fact]
    public void L_edition_d_un_event_expose_un_interrupteur()
    {
        Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>(p => p.Add(x => x.Id, "evt-cocktail"));

        Assert.Single(cut.FindAll("button.toggle"));
        Assert.Contains("Candidatures spontanées", cut.Markup);
    }

    [Fact]
    public void L_interrupteur_reflete_l_etat_de_l_event()
    {
        Db();

        // evt-cocktail est ouvert, evt-gala est fermé.
        var ouvert = Render<AdminTaos.Pages.Manager.MEventEdit>(p => p.Add(x => x.Id, "evt-cocktail"));
        Assert.Contains("on", ouvert.Find("button.toggle").ClassName);

        var ferme = Render<AdminTaos.Pages.Manager.MEventEdit>(p => p.Add(x => x.Id, "evt-gala"));
        Assert.DoesNotContain("on", ferme.Find("button.toggle").ClassName);
    }

    [Fact]
    public async Task Fermer_les_candidatures_persiste()
    {
        var db = Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>(p => p.Add(x => x.Id, "evt-cocktail"));

        cut.Find("button.toggle").Click();
        cut.Find("button.btn.primary.block").Click();

        Assert.False((await db.GetEventAsync("evt-cocktail"))!.IsOpenForSignup);
    }

    [Fact]
    public async Task Rouvrir_les_candidatures_persiste()
    {
        var db = Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>(p => p.Add(x => x.Id, "evt-gala"));

        cut.Find("button.toggle").Click();
        cut.Find("button.btn.primary.block").Click();

        Assert.True((await db.GetEventAsync("evt-gala"))!.IsOpenForSignup);
    }

    [Fact]
    public async Task Un_event_ferme_disparait_des_events_a_rejoindre()
    {
        var db = new InMemoryDataService();
        var cocktail = (await db.GetEventAsync("evt-cocktail"))!;
        cocktail.IsOpenForSignup = false;
        await db.UpdateEventAsync(cocktail);

        var me = (await db.GetAccountAsync(SeedData.EmpActiveHost))!;
        var fake = new FakeAuthClient();
        fake.PreRegister(me.Id, me.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(me.Email, "pwd");

        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(auth);

        var cut = Render<AdminTaos.Pages.Employee.EEvents>();

        Assert.Contains("Aucun event ouvert", cut.Markup);
    }

    [Fact]
    public async Task Un_event_ferme_ne_propose_plus_de_se_porter_candidat()
    {
        var db = new InMemoryDataService();
        var cocktail = (await db.GetEventAsync("evt-cocktail"))!;
        cocktail.IsOpenForSignup = false;
        await db.UpdateEventAsync(cocktail);

        var me = (await db.GetAccountAsync(SeedData.EmpActiveHost))!;
        var fake = new FakeAuthClient();
        fake.PreRegister(me.Id, me.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(me.Email, "pwd");

        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(auth);

        var cut = Render<AdminTaos.Pages.Employee.EEventDetail>(p => p.Add(x => x.Id, "evt-cocktail"));

        Assert.DoesNotContain("Demander à participer", cut.Markup);
    }
}
