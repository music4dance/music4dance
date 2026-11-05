namespace m4d.Configuration;

/// <summary>
/// Where the app keeps the files it writes at runtime (admin backups, usage exports and the runtime
/// caches) and reads uploaded sitemap overrides from, all under <see cref="AppData"/>.
/// Test and production set <see cref="RootSetting"/> to a folder on the persistent /home volume,
/// outside wwwroot, so the deployed package can be mounted read-only
/// (WEBSITE_RUN_FROM_PACKAGE). Unset, the root is the web root, so local runs keep their files in
/// wwwroot/AppData as before. See architecture/infrastructure/hosting-and-identity.md.
/// </summary>
public sealed record AppDataPaths(string Root)
{
    public const string RootSetting = "M4D_DATA_ROOT";

    public string AppData => Path.Combine(Root, "AppData");

    public static AppDataPaths Resolve(IConfiguration configuration, string webRootPath)
    {
        var root = configuration[RootSetting];
        if (string.IsNullOrWhiteSpace(root))
        {
            root = webRootPath;
        }

        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException(
                $"{RootSetting} is unset and there is no web root to fall back to");
        }

        return new AppDataPaths(Path.GetFullPath(root));
    }

    /// <summary>
    /// Creates the folder, so a root the app can't write to fails at startup rather than on the
    /// first backup.
    /// </summary>
    public AppDataPaths EnsureCreated()
    {
        _ = Directory.CreateDirectory(AppData);
        return this;
    }
}
