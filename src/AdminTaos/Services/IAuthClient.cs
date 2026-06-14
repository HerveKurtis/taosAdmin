namespace AdminTaos.Services;

public interface IAuthClient
{
    /// <summary>Sign in with email+password. Returns the Firebase Auth uid on success, null on failure.</summary>
    Task<string?> LoginAsync(string email, string password);

    /// <summary>Create a new auth user with email+password. Returns the new uid on success, null on failure (e.g. email taken).</summary>
    Task<string?> RegisterAsync(string email, string password);

    /// <summary>Sign the current user out.</summary>
    Task LogoutAsync();

    /// <summary>Get the currently authenticated uid, or null if none.</summary>
    Task<string?> GetCurrentUidAsync();

    /// <summary>Subscribe to auth state changes. Called with uid on sign-in/restore, null on sign-out.</summary>
    event Action<string?>? OnAuthChanged;

    /// <summary>Start listening to underlying auth changes (call once at app start).</summary>
    Task InitializeAsync();
}
