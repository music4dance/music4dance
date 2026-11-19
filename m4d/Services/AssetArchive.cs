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

    // How long Find trusts its file index before listing the archive again. Another instance's
    // archive or prune shows up within this; this instance's own refreshes the index at once.
    internal static readonly TimeSpan IndexLifetime = TimeSpan.FromMinutes(1);

    private readonly Lock _indexLock = new();
    private volatile Index _index;

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
            // The marker goes in before the rename, so a build folder is complete when it appears
            var temp = Path.Combine(Root, $"{TempPrefix}{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(temp);
            files.ForEach(f => File.Copy(Path.Combine(currentAssets, f), Path.Combine(temp, f)));
            File.WriteAllText(Path.Combine(temp, MarkerFile), clock.GetUtcNow().ToString("O"));
            try
            {
                Directory.Move(temp, target);
                logger.LogInformation("Archived {Count} bundles as build {BuildId}", files.Count, buildId);
            }
            catch (IOException) when (Directory.Exists(target))
            {
                // Another instance archived the same build first
                Directory.Delete(temp, recursive: true);
                TouchMarker(target);
            }
        }
        else
        {
            // A redeploy of an older build makes it the newest again
            TouchMarker(target);
        }

        Prune();
        _index = null;
    }

    // Rewrites the marker through a temporary file and a rename, so a reader never sees it half
    // written. Losing a race with another instance doing the same is harmless: either time works.
    private void TouchMarker(string buildPath)
    {
        var temp = Path.Combine(buildPath, $"{MarkerFile}{TempPrefix}{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(temp, clock.GetUtcNow().ToString("O"));
            File.Move(temp, Path.Combine(buildPath, MarkerFile), overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Couldn't update the archive marker in {Path}", buildPath);
            try
            {
                File.Delete(temp);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // Harmless: a dot-file is never served, and it goes when the build folder does
            }
        }
    }

    /// <summary>
    /// The archived copy of <paramref name="fileName"/>, from the newest build that has it, or
    /// null. Answers from an in-memory index of the archive, so a miss (a scanner, a real 404)
    /// costs no storage round trips. The file can still have been pruned since the index was
    /// built; callers handle it being gone.
    /// </summary>
    public string Find(string fileName)
    {
        return IsSafeName(fileName) && CurrentIndex().Files.TryGetValue(fileName, out var path)
            ? path
            : null;
    }

    private Index CurrentIndex()
    {
        var now = clock.GetUtcNow();
        var index = _index;
        if (index != null && now - index.BuiltAt < IndexLifetime)
        {
            return index;
        }

        lock (_indexLock)
        {
            index = _index;
            if (index == null || now - index.BuiltAt >= IndexLifetime)
            {
                index = new Index(now, BuildIndex());
                _index = index;
            }
            return index;
        }
    }

    private Dictionary<string, string> BuildIndex()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!Directory.Exists(Root))
        {
            return files;
        }

        try
        {
            foreach (var build in Builds())
            {
                foreach (var file in Directory.GetFiles(build.Path))
                {
                    var name = Path.GetFileName(file);
                    if (IsSafeName(name))
                    {
                        _ = files.TryAdd(name, file); // newest build first, so the first one wins
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A build pruned mid-listing; what was read so far is still right, and the next
            // refresh lists it again
            logger.LogWarning(ex, "Couldn't list the asset archive at {Root}", Root);
        }

        return files;
    }

    private sealed record Index(DateTimeOffset BuiltAt, Dictionary<string, string> Files);

    internal void Prune()
    {
        var now = clock.GetUtcNow();
        var builds = Builds();

        var expired = builds
            .Select((build, index) => (build, index))
            .Where(x => x.index >= Math.Max(1, options.KeepBuilds)
                && now - builds[x.index - 1].ArchivedAt >= options.MinRetention)
            .Select(x => x.build.Path);

        // Abandoned copies, and build folders that never got a marker (written by an older
        // version of this code, or damaged); a folder younger than this may still be in progress
        var stale = Directory.GetDirectories(Root)
            .Where(d => Path.GetFileName(d).StartsWith(TempPrefix, StringComparison.Ordinal)
                || !Path.GetFileName(d).StartsWith('.') && ReadMarker(d) == null)
            .Where(d => now - Directory.GetCreationTimeUtc(d) >= StaleTemp);

        foreach (var path in expired.Concat(stale).ToList())
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
