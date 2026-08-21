using AdminTaos.Models;

namespace AdminTaos.Services;

/// <summary>
/// Développement et captures d'écran uniquement. Accepte n'importe quel mot de passe pour un
/// compte du jeu de démonstration. N'est jamais enregistré en environnement Production.
/// </summary>
public class DemoAuthClient : IAuthClient
{
    private readonly IDataService _data;
    private string? _uid;

    public event Action<string?>? OnAuthChanged;

    public DemoAuthClient(IDataService data) => _data = data;

    public async Task<string?> LoginAsync(string email, string password)
    {
        _uid = (await _data.GetAccountByEmailAsync(email))?.Id;
        OnAuthChanged?.Invoke(_uid);
        return _uid;
    }

    public async Task<string?> RegisterAsync(string email, string password)
    {
        if (await _data.GetAccountByEmailAsync(email) is not null) return null;
        _uid = Guid.NewGuid().ToString();
        OnAuthChanged?.Invoke(_uid);
        return _uid;
    }

    public Task LogoutAsync() { _uid = null; OnAuthChanged?.Invoke(null); return Task.CompletedTask; }
    public Task<string?> GetCurrentUidAsync() => Task.FromResult(_uid);
    public Task InitializeAsync() { OnAuthChanged?.Invoke(_uid); return Task.CompletedTask; }
}
