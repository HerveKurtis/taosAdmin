using AdminTaos.Models;
using AdminTaos.Services;
using Xunit;

namespace AdminTaos.Tests;

public class AuthStateTests
{
    private static (AuthState auth, FakeAuthClient fakeAuth, InMemoryDataService data) Wire()
    {
        var fakeAuth = new FakeAuthClient();
        var data     = new InMemoryDataService();
        // Pre-register Firebase-Auth-side users for two seeded accounts; passwords arbitrary.
        fakeAuth.PreRegister(SeedData.MgrId,         "manager@taos.be", "pwd-mgr");
        fakeAuth.PreRegister(SeedData.EmpActiveHost, "sarah@taos.be",   "pwd-sarah");
        var state = new AuthState(fakeAuth, data);
        return (state, fakeAuth, data);
    }

    [Fact]
    public async Task Login_with_correct_credentials_sets_current_user()
    {
        var (auth, _, _) = Wire();
        var ok = await auth.LoginAsync("manager@taos.be", "pwd-mgr");
        Assert.True(ok);
        Assert.Equal(AccountType.Manager, auth.CurrentUser!.Type);
    }

    [Fact]
    public async Task Login_with_wrong_password_fails_and_user_stays_null()
    {
        var (auth, _, _) = Wire();
        Assert.False(await auth.LoginAsync("manager@taos.be", "bad-pwd"));
        Assert.Null(auth.CurrentUser);
    }

    [Fact]
    public async Task Initialize_restores_current_user_from_existing_session()
    {
        var fakeAuth = new FakeAuthClient();
        var data     = new InMemoryDataService();
        fakeAuth.PreRegister(SeedData.EmpActiveHost, "sarah@taos.be", "pwd-sarah");
        await fakeAuth.LoginAsync("sarah@taos.be", "pwd-sarah");   // session established outside AuthState

        var state = new AuthState(fakeAuth, data);
        await state.InitializeAsync();

        Assert.Equal("sarah@taos.be", state.CurrentUser!.Email);
    }

    [Fact]
    public async Task Logout_clears_current_user()
    {
        var (auth, _, _) = Wire();
        await auth.LoginAsync("manager@taos.be", "pwd-mgr");
        await auth.LogoutAsync();
        Assert.Null(auth.CurrentUser);
    }
}
