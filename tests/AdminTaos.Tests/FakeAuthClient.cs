using AdminTaos.Services;

namespace AdminTaos.Tests;

/// <summary>In-memory IAuthClient for tests. Maps email→uid; password is checked against a stored map.</summary>
public class FakeAuthClient : IAuthClient
{
    private readonly Dictionary<string,(string Uid,string Password)> _users = new(StringComparer.OrdinalIgnoreCase);
    private string? _currentUid;

    public event Action<string?>? OnAuthChanged;

    /// <summary>Pre-register a user (uid known up front, e.g. matching a SeedData Account.Id).</summary>
    public void PreRegister(string uid, string email, string password)
        => _users[email] = (uid, password);

    public Task<string?> LoginAsync(string email, string password)
    {
        if (_users.TryGetValue(email, out var u) && u.Password == password)
        {
            _currentUid = u.Uid;
            OnAuthChanged?.Invoke(_currentUid);
            return Task.FromResult<string?>(_currentUid);
        }
        return Task.FromResult<string?>(null);
    }

    public Task<string?> RegisterAsync(string email, string password)
    {
        if (_users.ContainsKey(email)) return Task.FromResult<string?>(null);
        var uid = Guid.NewGuid().ToString();
        _users[email] = (uid, password);
        _currentUid = uid;
        OnAuthChanged?.Invoke(_currentUid);
        return Task.FromResult<string?>(_currentUid);
    }

    public Task LogoutAsync()
    {
        _currentUid = null;
        OnAuthChanged?.Invoke(null);
        return Task.CompletedTask;
    }

    public Task<string?> GetCurrentUidAsync() => Task.FromResult(_currentUid);

    public Task InitializeAsync() => Task.CompletedTask;
}
