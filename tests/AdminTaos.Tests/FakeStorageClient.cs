using AdminTaos.Services;

namespace AdminTaos.Tests;

/// <summary>IStorageClient inerte : les pages de profil l'injectent, aucun test ne téléverse.</summary>
public class FakeStorageClient : IStorageClient
{
    public Task<string?> PickAndUploadAvatarAsync(string uid) => Task.FromResult<string?>(null);
}
