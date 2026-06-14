namespace AdminTaos.Services;

public interface IStorageClient
{
    /// <summary>Opens a native file picker, lets the user pick an image, compresses & uploads it to
    /// `avatars/{uid}.jpg`, and returns the public download URL. Returns null if the user cancels.
    /// Throws if the file is invalid (non-image or > 2 MB) or the upload fails.</summary>
    Task<string?> PickAndUploadAvatarAsync(string uid);
}
