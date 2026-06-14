using Microsoft.JSInterop;

namespace AdminTaos.Services;

public class FirebaseAuthClient : IAuthClient, IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private DotNetObjectReference<FirebaseAuthClient>? _self;

    public event Action<string?>? OnAuthChanged;

    public FirebaseAuthClient(IJSRuntime js) { _js = js; }

    public async Task InitializeAsync()
    {
        _self = DotNetObjectReference.Create(this);
        await _js.InvokeVoidAsync("taos.watchAuth", _self);
    }

    [JSInvokable]
    public Task OnAuthChangedFromJs(string? uid)
    {
        OnAuthChanged?.Invoke(uid);
        return Task.CompletedTask;
    }

    public async Task<string?> LoginAsync(string email, string password)
        => await _js.InvokeAsync<string?>("taos.login", email, password);

    public async Task<string?> RegisterAsync(string email, string password)
        => await _js.InvokeAsync<string?>("taos.register", email, password);

    public Task LogoutAsync()
        => _js.InvokeVoidAsync("taos.logout").AsTask();

    public Task<string?> GetCurrentUidAsync()
        => _js.InvokeAsync<string?>("taos.currentUid").AsTask();

    public ValueTask DisposeAsync()
    {
        _self?.Dispose();
        return ValueTask.CompletedTask;
    }
}
