using AdminTaos.Models;

namespace AdminTaos.Services;

public class NavigationGuard
{
    private static readonly string[] AuthPages = { "", "splash", "login", "register" };

    /// <summary>Returns a relative path to redirect to, or null if the path is allowed.</summary>
    public string? Resolve(string path, Account? user)
    {
        path = path.Trim('/').ToLowerInvariant();
        var isAuthPage = AuthPages.Contains(path);

        if (user is null)
            return isAuthPage ? null : "login";

        if (user.Status == AccountStatus.Rejected)
            return path == "login" ? null : "login";

        if (user.Status == AccountStatus.Pending)
            return path == "pending" ? null : "pending";

        // Active user
        var area = user.Type == AccountType.Manager ? "m" : "e";
        if (isAuthPage || path == "pending") return area;
        var root = path.Split('/')[0];
        if (root != area) return area;
        return null;
    }
}
