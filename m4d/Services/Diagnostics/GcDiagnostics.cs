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
    /// <returns>A tuple containing the before snapshot, after snapshot, and bytes freed.</returns>
    public static (GcSnapshot Before, GcSnapshot After, long BytesFreed) ForceCollectionWithMetrics()
    {
        var before = CaptureSnapshot();

        // Force full blocking GC with compaction
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

        var after = CaptureSnapshot();
        var bytesFreed = before.TotalMemoryBytes - after.TotalMemoryBytes;

        return (before, after, bytesFreed);
    }

    /// <summary>
    /// Creates a memory dump of the current process.
    /// </summary>
    /// <param name="dumpDirectory">Directory to store the dump file. Uses default if null.</param>
    /// <param name="dumpType">Type of dump to create (Mini, Heap, or Full).</param>
    /// <returns>Result containing the dump file path or error information.</returns>
    public static DumpResult CreateDump(string? dumpDirectory = null, DumpType dumpType = DumpType.Heap)
    {
        try
        {
            var directory = dumpDirectory ?? DefaultDumpDirectory;
            Directory.CreateDirectory(directory);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var fileName = $"m4d_{dumpType}_{timestamp}_{Environment.ProcessId}.dmp";
            var filePath = Path.Combine(directory, fileName);

            // Use dotnet-dump or createdump tool
            var success = TryCreateDumpWithDotnetDump(filePath, dumpType)
                       || TryCreateDumpWithCreatedump(filePath, dumpType);

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

    private static bool TryCreateDumpWithDotnetDump(string filePath, DumpType dumpType)
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
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            process.WaitForExit(TimeSpan.FromMinutes(5));
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCreateDumpWithCreatedump(string filePath, DumpType dumpType)
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
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            process.WaitForExit(TimeSpan.FromMinutes(5));
            return process.ExitCode == 0;
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
}
