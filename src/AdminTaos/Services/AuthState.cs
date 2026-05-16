using AdminTaos.Models;
using Blazored.LocalStorage;

namespace AdminTaos.Services;

public class AuthState
{
    private const string Key = "taos.session.email";
    private readonly IDataService _data;
    private readonly ILocalStorageService _ls;

    public AuthState(IDataService data, ILocalStorageService ls) { _data = data; _ls = ls; }

    public Account? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null;
    public event Action? OnChange;
    private void Notify() => OnChange?.Invoke();

    public async Task InitializeAsync()
    {
        var email = await _ls.GetItemAsStringAsync(Key);
        if (!string.IsNullOrWhiteSpace(email))
            CurrentUser = await _data.GetAccountByEmailAsync(email);
        Notify();
    }

    public async Task<bool> LoginAsync(string email)
    {
        var acc = await _data.GetAccountByEmailAsync(email.Trim());
        if (acc is null) return false;
        CurrentUser = acc;
        await _ls.SetItemAsStringAsync(Key, acc.Email);
        Notify();
        return true;
    }

    public async Task LogoutAsync()
    {
        CurrentUser = null;
        await _ls.RemoveItemAsync(Key);
        Notify();
    }

    public void Refresh() => Notify();
}
