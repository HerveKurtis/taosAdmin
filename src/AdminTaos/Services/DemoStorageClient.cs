namespace AdminTaos.Services;

/// <summary>Développement uniquement : aucun téléversement, aucune connexion à Firebase Storage.</summary>
public class DemoStorageClient : IStorageClient
{
    public Task<string?> PickAndUploadAvatarAsync(string uid) => Task.FromResult<string?>(null);
}
