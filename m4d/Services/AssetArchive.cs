using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace m4d.Services;

/// <summary>
/// Keeps the hashed Vite bundles (/vclient/assets) of recent builds on persistent storage, so a
/// page that still points at a previous build's bundles can load them after a deploy. Run From
/// Package mounts each deploy's zip read-only as wwwroot, so a build's bundles vanish from wwwroot
/// the moment the next one starts, but Cloudflare (or a browser, or a tab left open) can still
/// hold HTML that references them.
///
/// Each build is copied into its own folder under <see cref="Root"/>, named from a hash of its
/// file names (the names already carry content hashes, so redeploying the same client reuses the
/// folder). The newest <see cref="AssetArchiveOptions.KeepBuilds"/> builds are always kept, and an
/// older one is kept until <see cref="AssetArchiveOptions.MinRetention"/> has passed since the
/// build that replaced it was archived, which has to outlast the longest time anything caches
/// HTML. See architecture/infrastructure/hosting-and-identity.md (Bundles from earlier builds).
/// </summary>
public partial class AssetArchive(
    string root, string currentAssets, AssetArchiveOptions options, TimeProvider clock,
    ILogger<AssetArchive> logger)
{
    public const string FolderName = "asset-archive";
    internal const string MarkerFile = ".archived";
    private const string TempPrefix = ".tmp-";
    private static readonly TimeSpan StaleTemp = TimeSpan.FromHours(1);

    public string Root { get; } = root;

    /// <summary>
    /// Copies the current build's bundles into the archive (or marks the existing copy as the
    /// newest) and prunes builds that are no longer needed. Safe to run from two instances at
    /// once: each copies into its own temporary folder and the first rename wins.
    /// </summary>
    public void ArchiveCurrent()
    {
        if (!Directory.Exists(currentAssets))
        {
            logger.LogInformation("No bundles at {Assets}; nothing to archive", currentAssets);
            return;
        }

        var files = Directory.GetFiles(currentAssets)
            .Select(Path.GetFileName)
            .Where(IsSafeName)
            .Order(StringComparer.Ordinal)
            .ToList();
        if (files.Count == 0)
        {
            logger.LogInformation("No bundles at {Assets}; nothing to archive", currentAssets);
            return;
        }

        _ = Directory.CreateDirectory(Root);
        var buildId = BuildId(files);
        var target = Path.Combine(Root, buildId);

        if (!Directory.Exists(target))
        {
            var temp = Path.Combine(Root, $"{TempPrefix}{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(temp);
            files.ForEach(f => File.Copy(Path.Combine(currentAssets, f), Path.Combine(temp, f)));
            try
            {
                Directory.Move(temp, target);
                logger.LogInformation("Archived {Count} bundles as build {BuildId}", files.Count, buildId);
            }
            catch (IOException) when (Directory.Exists(target))
            {
                // Another instance archived the same build first
                Directory.Delete(temp, recursive: true);
            }
        }

        // Written on every start, so a redeploy of an older build makes it the newest again
        File.WriteAllText(Path.Combine(target, MarkerFile), clock.GetUtcNow().ToString("O"));

        Prune();
    }

    /// <summary>
    /// The archived copy of <paramref name="fileName"/>, newest build first, or null.
    /// </summary>
    public string Find(string fileName)
    {
        if (!IsSafeName(fileName) || !Directory.Exists(Root))
        {
            return null;
        }

        return Builds()
            .Select(b => Path.Combine(b.Path, fileName))
            .FirstOrDefault(File.Exists);
    }

    internal void Prune()
    {
        var now = clock.GetUtcNow();
        var builds = Builds();

        var expired = builds
            .Select((build, index) => (build, index))
            .Where(x => x.index >= Math.Max(1, options.KeepBuilds)
                && now - builds[x.index - 1].ArchivedAt >= options.MinRetention)
            .Select(x => x.build.Path);

        var staleTemps = Directory.GetDirectories(Root, $"{TempPrefix}*")
            .Where(d => now - Directory.GetCreationTimeUtc(d) >= StaleTemp);

        foreach (var path in expired.Concat(staleTemps).ToList())
        {
            try
            {
                Directory.Delete(path, recursive: true);
                logger.LogInformation("Removed archived bundles {Path}", path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Another instance got there first, or a file is in use; the next start retries
                logger.LogWarning(ex, "Couldn't remove archived bundles {Path}", path);
            }
        }
    }

    // Completed builds, newest first. A folder without a readable marker is skipped: it's mid-copy
    // by another instance, or damaged, and either way isn't safe to serve from or order by.
    private List<(string Path, DateTimeOffset ArchivedAt)> Builds()
    {
        return [.. Directory.GetDirectories(Root)
            .Where(d => !Path.GetFileName(d).StartsWith('.'))
            .Select(d => (Path: d, ArchivedAt: ReadMarker(d)))
            .Where(b => b.ArchivedAt.HasValue)
            .Select(b => (b.Path, ArchivedAt: b.ArchivedAt.Value))
            .OrderByDescending(b => b.ArchivedAt)];
    }

    private static DateTimeOffset? ReadMarker(string buildPath)
    {
        try
        {
            var text = File.ReadAllText(Path.Combine(buildPath, MarkerFile));
            return DateTimeOffset.TryParse(text, null, System.Globalization.DateTimeStyles.RoundtripKind, out var at)
                ? at
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal static string BuildId(IEnumerable<string> sortedNames)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', sortedNames)));
        return Convert.ToHexStringLower(hash)[..16];
    }

    // Vite's output names: letters, digits, '.', '_' and '-', in a single path segment
    internal static bool IsSafeName(string name) =>
        !string.IsNullOrEmpty(name) && name.Length <= 200 && !name.StartsWith('.') && SafeName().IsMatch(name);

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex SafeName();
}

public record AssetArchiveOptions
{
    public bool Enabled { get; init; } = true;
    public int KeepBuilds { get; init; } = 3;
    public TimeSpan MinRetention { get; init; } = TimeSpan.FromHours(48);
}
