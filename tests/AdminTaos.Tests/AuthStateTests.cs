using AdminTaos.Models;
using AdminTaos.Services;
using Blazored.LocalStorage;
using Xunit;

namespace AdminTaos.Tests;

class FakeLocalStorage : ILocalStorageService
{
    readonly Dictionary<string,string> _d = new();
    public ValueTask<T?> GetItemAsync<T>(string k, CancellationToken c = default)
        => new(_d.TryGetValue(k, out var v) && v is T tv ? tv : default);
    public ValueTask SetItemAsync<T>(string k, T v, CancellationToken c = default)
    { _d[k] = v?.ToString() ?? ""; return ValueTask.CompletedTask; }
    public ValueTask RemoveItemAsync(string k, CancellationToken c = default)
    { _d.Remove(k); return ValueTask.CompletedTask; }
    public ValueTask<string?> GetItemAsStringAsync(string k, CancellationToken c=default)=>new(_d.TryGetValue(k, out var v) ? v : null);
    public ValueTask SetItemAsStringAsync(string k,string v,CancellationToken c=default){_d[k]=v;return ValueTask.CompletedTask;}
    public ValueTask<bool> ContainKeyAsync(string k,CancellationToken c=default)=>new(_d.ContainsKey(k));
    public ValueTask ClearAsync(CancellationToken c=default){_d.Clear();return ValueTask.CompletedTask;}
    public ValueTask<int> LengthAsync(CancellationToken c=default)=>new(_d.Count);
    public ValueTask<string?> KeyAsync(int i,CancellationToken c=default)=>new(_d.Keys.ElementAt(i));
    public ValueTask<IEnumerable<string>> KeysAsync(CancellationToken c=default)=>new(_d.Keys.AsEnumerable());
    public ValueTask RemoveItemsAsync(IEnumerable<string> keys, CancellationToken c=default)
    { foreach (var k in keys) _d.Remove(k); return ValueTask.CompletedTask; }
    public event EventHandler<ChangingEventArgs>? Changing;
    public event EventHandler<ChangedEventArgs>? Changed;
}

public class AuthStateTests
{
    [Fact]
    public async Task Login_with_known_email_sets_current_user()
    {
        var auth = new AuthState(new InMemoryDataService(), new FakeLocalStorage());
        var ok = await auth.LoginAsync("manager@taos.be");
        Assert.True(ok);
        Assert.Equal(AccountType.Manager, auth.CurrentUser!.Type);
    }

    [Fact]
    public async Task Login_unknown_email_fails()
    {
        var auth = new AuthState(new InMemoryDataService(), new FakeLocalStorage());
        Assert.False(await auth.LoginAsync("nobody@taos.be"));
        Assert.Null(auth.CurrentUser);
    }

    [Fact]
    public async Task Initialize_restores_session()
    {
        var ls = new FakeLocalStorage();
        var a1 = new AuthState(new InMemoryDataService(), ls);
        await a1.LoginAsync("sarah@taos.be");
        var a2 = new AuthState(new InMemoryDataService(), ls);
        await a2.InitializeAsync();
        Assert.Equal("sarah@taos.be", a2.CurrentUser!.Email);
    }

    [Fact]
    public async Task Logout_clears_session()
    {
        var auth = new AuthState(new InMemoryDataService(), new FakeLocalStorage());
        await auth.LoginAsync("sarah@taos.be");
        await auth.LogoutAsync();
        Assert.Null(auth.CurrentUser);
    }
}
