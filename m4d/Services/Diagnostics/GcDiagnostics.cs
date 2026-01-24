using System.Diagnostics;
using System.Runtime.InteropServices;

namespace m4d.Services.Diagnostics;

/// <summary>
/// Provides static methods for capturing garbage collection diagnostics.
/// </summary>
public static class GcDiagnostics
{
    /// <summary>
    /// Default directory for storing memory dumps.
    /// </summary>
    public static string DefaultDumpDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "m4d", "dumps");

    /// <summary>
    /// Captures a snapshot of current GC and memory metrics.
    /// </summary>
    public static GcSnapshot CaptureSnapshot()
    {
        var gcInfo = GC.GetGCMemoryInfo();

        // GenerationInfo indices: 0=Gen0, 1=Gen1, 2=Gen2, 3=LOH, 4=POH
        var lohSize = gcInfo.GenerationInfo.Length > 3 ? gcInfo.GenerationInfo[3].SizeAfterBytes : 0;
        var pohSize = gcInfo.GenerationInfo.Length > 4 ? gcInfo.GenerationInfo[4].SizeAfterBytes : 0;

        return new GcSnapshot(
            TotalMemoryBytes: GC.GetTotalMemory(forceFullCollection: false),
            HeapSizeBytes: gcInfo.HeapSizeBytes,
            FragmentedBytes: gcInfo.FragmentedBytes,
            Gen0Collections: GC.CollectionCount(0),
            Gen1Collections: GC.CollectionCount(1),
            Gen2Collections: GC.CollectionCount(2),
            WorkingSetBytes: Environment.WorkingSet,
            MemoryLoadPercent: gcInfo.TotalAvailableMemoryBytes > 0
                ? (gcInfo.MemoryLoadBytes * 100.0 / gcInfo.TotalAvailableMemoryBytes)
                : 0,
            TotalAvailableMemoryBytes: gcInfo.TotalAvailableMemoryBytes,
            HighMemoryLoadThresholdBytes: gcInfo.HighMemoryLoadThresholdBytes,
            LargeObjectHeapSizeBytes: lohSize,
            PinnedObjectHeapSizeBytes: pohSize,
            CapturedAt: DateTimeOffset.Now
        );
    }

    /// <summary>
    /// Forces a full garbage collection and returns before/after snapshots.
    /// </summary>
    /// <returns>A tuple containing the before snapshot, after snapshot, and memory delta (positive = freed, negative = increased).</returns>
    public static (GcSnapshot Before, GcSnapshot After, long MemoryDelta) ForceCollectionWithMetrics()
    {
        var before = CaptureSnapshot();

        // Force full blocking GC with compaction
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

        var after = CaptureSnapshot();
        var memoryDelta = before.TotalMemoryBytes - after.TotalMemoryBytes;

        return (before, after, memoryDelta);
    }

    /// <summary>
    /// Creates a memory dump of the current process.
    /// </summary>
    /// <param name="dumpDirectory">Directory to store the dump file. Uses default if null.</param>
    /// <param name="dumpType">Type of dump to create (Mini, Heap, or Full).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the dump file path or error information.</returns>
    public static async Task<DumpResult> CreateDumpAsync(string? dumpDirectory = null, DumpType dumpType = DumpType.Heap, CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = dumpDirectory ?? DefaultDumpDirectory;
            Directory.CreateDirectory(directory);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var fileName = $"m4d_{dumpType}_{timestamp}_{Environment.ProcessId}.dmp";
            var filePath = Path.Combine(directory, fileName);

            // Use dotnet-dump or createdump tool
            var success = await TryCreateDumpWithDotnetDumpAsync(filePath, dumpType, cancellationToken)
                       || await TryCreateDumpWithCreatedumpAsync(filePath, dumpType, cancellationToken);

            if (success && File.Exists(filePath))
            {
                var fileInfo = new FileInfo(filePath);
                return new DumpResult(
                    Success: true,
                    FilePath: filePath,
                    FileName: fileName,
                    FileSizeBytes: fileInfo.Length,
                    DumpType: dumpType,
                    ErrorMessage: null);
            }

            return new DumpResult(
                Success: false,
                FilePath: filePath,
                FileName: fileName,
                FileSizeBytes: 0,
                DumpType: dumpType,
                ErrorMessage: "Failed to create dump. Ensure dotnet-dump tool is installed: dotnet tool install -g dotnet-dump");
        }
        catch (OperationCanceledException)
        {
            return new DumpResult(
                Success: false,
                FilePath: null,
                FileName: null,
                FileSizeBytes: 0,
                DumpType: dumpType,
                ErrorMessage: "Dump creation was cancelled.");
        }
        catch (Exception ex)
        {
            return new DumpResult(
                Success: false,
                FilePath: null,
                FileName: null,
                FileSizeBytes: 0,
                DumpType: dumpType,
                ErrorMessage: ex.Message);
        }
    }

    private static async Task<bool> TryCreateDumpWithDotnetDumpAsync(string filePath, DumpType dumpType, CancellationToken cancellationToken)
    {
        try
        {
            var dumpTypeArg = dumpType switch
            {
                DumpType.Mini => "Mini",
                DumpType.Heap => "Heap",
                DumpType.Full => "Full",
                _ => "Heap"
            };

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet-dump",
                Arguments = $"collect -p {Environment.ProcessId} -o \"{filePath}\" --type {dumpTypeArg}",
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TryCreateDumpWithCreatedumpAsync(string filePath, DumpType dumpType, CancellationToken cancellationToken)
    {
        // createdump is available on Linux with .NET runtime
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return false;

        try
        {
            var dumpTypeArg = dumpType switch
            {
                DumpType.Mini => "--mini",
                DumpType.Heap => "--heap",
                DumpType.Full => "--full",
                _ => "--heap"
            };

            // Find createdump in the .NET runtime directory
            var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
            var createdumpPath = Path.Combine(runtimeDir, "createdump");

            if (!File.Exists(createdumpPath))
                return false;

            var startInfo = new ProcessStartInfo
            {
                FileName = createdumpPath,
                Arguments = $"{dumpTypeArg} -f \"{filePath}\" {Environment.ProcessId}",
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the current process ID for use with external diagnostic tools.
    /// </summary>
    public static int GetProcessId() => Environment.ProcessId;

    /// <summary>
    /// Gets a list of recent dump files from the default dump directory.
    /// </summary>
    /// <param name="maxCount">Maximum number of dumps to return.</param>
    /// <returns>List of dump file information, ordered by most recent first.</returns>
    public static IReadOnlyList<DumpFileInfo> GetRecentDumps(int maxCount = 10)
    {
        var directory = DefaultDumpDirectory;
        if (!Directory.Exists(directory))
            return [];

        return Directory.GetFiles(directory, "*.dmp")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .Take(maxCount)
            .Select(f => new DumpFileInfo(
                FileName: f.Name,
                FilePath: f.FullName,
                FileSizeBytes: f.Length,
                CreatedAt: f.CreationTime))
            .ToList();
    }

    /// <summary>
    /// Deletes a specific dump file.
    /// </summary>
    /// <param name="fileName">The file name of the dump to delete.</param>
    /// <returns>True if deleted successfully, false otherwise.</returns>
    public static bool DeleteDump(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var filePath = Path.Combine(DefaultDumpDirectory, Path.GetFileName(fileName));
        
        if (!File.Exists(filePath))
            return false;

        try
        {
            File.Delete(filePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Deletes all dump files from the default dump directory.
    /// </summary>
    /// <returns>The number of files deleted.</returns>
    public static int DeleteAllDumps()
    {
        var directory = DefaultDumpDirectory;
        if (!Directory.Exists(directory))
            return 0;

        var count = 0;
        foreach (var file in Directory.GetFiles(directory, "*.dmp"))
        {
            try
            {
                File.Delete(file);
                count++;
            }
            catch
            {
                // Continue deleting other files even if one fails
            }
        }
        return count;
    }
}
