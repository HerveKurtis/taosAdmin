using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

public class EventTeamTests : BunitContext
{
    /// <summary>Connecte le compte donné et désigne Marc responsable du gala, sauf indication contraire.</summary>
    async Task<InMemoryDataService> Setup(string signedInAs, string? responsable = SeedData.EmpActiveServer)
    {
        var db = new InMemoryDataService();

        var e = (await db.GetEventAsync("evt-gala"))!;
        e.ResponsableAccountId = responsable;
        await db.UpdateEventAsync(e);

        await db.CreateAssignmentAsync(new Assignment {
            Id = "asg-gala-host", EventId = "evt-gala", AccountId = SeedData.EmpActiveHost,
            JobRoleId = SeedData.RoleHost, Status = AssignmentStatus.Confirmed });

        var me = (await db.GetAccountAsync(signedInAs))!;
        var fake = new FakeAuthClient();
        fake.PreRegister(me.Id, me.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(me.Email, "pwd");

        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(auth);
        return db;
    }

    IRenderedComponent<AdminTaos.Pages.Shared.EventTeam> Page() =>
        Render<AdminTaos.Pages.Shared.EventTeam>(p => p.Add(x => x.Id, "evt-gala"));

    [Fact]
    public async Task The_designated_responsable_sees_the_team()
    {
        await Setup(SeedData.EmpActiveServer);
        var cut = Page();

        Assert.Empty(cut.FindAll(".team-denied"));
        Assert.Equal(2, cut.FindAll(".team-member").Count);
        Assert.Contains("Marc D.", cut.Markup);
        Assert.Contains("Sarah K.", cut.Markup);
    }

    [Fact]
    public async Task An_admin_may_supervise_the_page()
    {
        await Setup(SeedData.MgrId);
        Assert.Empty(Page().FindAll(".team-denied"));
    }

    [Fact]
    public async Task A_collaborateur_who_is_not_the_responsable_is_refused()
    {
        await Setup(SeedData.EmpActiveHost);
        var cut = Page();

        Assert.Single(cut.FindAll(".team-denied"));
        Assert.Empty(cut.FindAll(".team-member"));
    }

    [Fact]
    public async Task The_event_briefing_is_shown()
    {
        await Setup(SeedData.EmpActiveServer);
        var cut = Page();

        Assert.Contains("Hôtel Plaza", cut.Markup);
        Assert.Contains("Arriver 15 min avant", cut.Markup);
    }

    [Fact]
    public async Task A_phone_number_is_a_tel_link_and_its_absence_is_stated()
    {
        var db = await Setup(SeedData.EmpActiveServer);
        var sarah = (await db.GetAccountAsync(SeedData.EmpActiveHost))!;
        sarah.Phone = "0470 99 88 77";
        await db.UpdateAccountAsync(sarah);

        var cut = Page();

        var link = cut.Find($".team-member[data-account-id='{SeedData.EmpActiveHost}'] a.team-phone");
        Assert.Equal("tel:0470998877", link.GetAttribute("href"));

        var marc = cut.Find($".team-member[data-account-id='{SeedData.EmpActiveServer}']");
        Assert.Empty(marc.QuerySelectorAll("a.team-phone"));
        Assert.Contains("Téléphone non renseigné", marc.TextContent);
    }

    [Fact]
    public async Task Only_confirmed_assignments_appear()
    {
        var db = await Setup(SeedData.EmpActiveServer);
        await db.CreateAssignmentAsync(new Assignment {
            Id = "asg-gala-pending", EventId = "evt-gala", AccountId = SeedData.EmpPending,
            JobRoleId = SeedData.RoleServer, Status = AssignmentStatus.PendingApproval });

        Assert.Equal(2, Page().FindAll(".team-member").Count);
    }
}
