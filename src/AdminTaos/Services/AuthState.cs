using AdminTaos.Models;

namespace AdminTaos.Services;

public class AuthState
{
    private readonly IAuthClient _auth;
    private readonly IDataService _data;

    public AuthState(IAuthClient auth, IDataService data)
    {
        _auth = auth;
        _data = data;
        _auth.OnAuthChanged += OnUidChanged;
    }

    public Account? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null;
    public event Action? OnChange;
    private void Notify() => OnChange?.Invoke();

    /// <summary>Wires up the underlying auth listener and reads the current session (if any).</summary>
    public async Task InitializeAsync()
    {
        await _auth.InitializeAsync();
        var uid = await _auth.GetCurrentUidAsync();
        if (uid is not null) CurrentUser = await _data.GetAccountAsync(uid);
        Notify();
    }

    /// <summary>Sign in. Returns true on success (account exists in Firestore).</summary>
    public async Task<bool> LoginAsync(string email, string password)
    {
        var uid = await _auth.LoginAsync(email.Trim(), password);
        if (uid is null) return false;
        CurrentUser = await _data.GetAccountAsync(uid);
        Notify();
        return CurrentUser is not null;
    }

    /// <summary>Create a new Auth user AND the accounts/{uid} doc (Employee, Pending). Returns true on success.</summary>
    public async Task<bool> RegisterAsync(string fullName, string email, string password, List<string> jobRoleIds)
    {
        var uid = await _auth.RegisterAsync(email.Trim(), password);
        if (uid is null) return false;
        var acc = new Account {
            Id = uid,
            FullName = fullName,
            Email = email.Trim(),
            Type = AccountType.Employee,
            Status = AccountStatus.Pending,
            JobRoleIds = jobRoleIds,
            CreatedAt = DateTime.UtcNow
        };
        await _data.CreateAccountAsync(acc);
        CurrentUser = acc;
        Notify();
        return true;
    }

    public async Task LogoutAsync()
    {
        await _auth.LogoutAsync();
        CurrentUser = null;
        Notify();
    }

    public void Refresh() => Notify();

    private async void OnUidChanged(string? uid)
    {
        CurrentUser = uid is null ? null : await _data.GetAccountAsync(uid);
        Notify();
    }
}
