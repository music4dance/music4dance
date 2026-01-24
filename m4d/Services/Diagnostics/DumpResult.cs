namespace m4d.Services.Diagnostics;

/// <summary>
/// Specifies the type of memory dump to create.
/// </summary>
public enum DumpType
{
    /// <summary>
    /// Minimal dump with just thread stacks and limited memory.
    /// Smallest file size, useful for crash analysis.
    /// </summary>
    Mini,

    /// <summary>
    /// Heap dump including all managed memory.
    /// Medium file size, useful for memory analysis.
    /// </summary>
    Heap,

    /// <summary>
    /// Full process dump including all memory.
    /// Largest file size, complete process state.
    /// </summary>
    Full
}

/// <summary>
/// Result of a memory dump operation.
/// </summary>
public record DumpResult(
    bool Success,
    string? FilePath,
    string? FileName,
    long FileSizeBytes,
    DumpType DumpType,
    string? ErrorMessage
)
{
    /// <summary>
    /// Gets the file size in megabytes.
    /// </summary>
    public double FileSizeMB => FileSizeBytes / (1024.0 * 1024.0);
}

/// <summary>
/// Information about an existing dump file.
/// </summary>
public record DumpFileInfo(
    string FileName,
    string FilePath,
    long FileSizeBytes,
    DateTime CreatedAt
)
{
    /// <summary>
    /// Gets the file size in megabytes.
    /// </summary>
    public double FileSizeMB => FileSizeBytes / (1024.0 * 1024.0);
}
