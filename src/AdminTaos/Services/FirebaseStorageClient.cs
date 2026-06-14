using Microsoft.JSInterop;

namespace AdminTaos.Services;

public class FirebaseStorageClient : IStorageClient
{
    private readonly IJSRuntime _js;
    public FirebaseStorageClient(IJSRuntime js) { _js = js; }

    public Task<string?> PickAndUploadAvatarAsync(string uid)
        => _js.InvokeAsync<string?>("taos.pickAndUploadAvatar", uid).AsTask();
}
