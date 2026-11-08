using CsvHelper;

namespace m4d.Utilities;

/// <summary>
/// Reads the tab-separated UsageLog export consumed by the admin usage loaders
/// (see architecture/runbooks/analyze-usage-logs.md for the file format).
/// </summary>
/// <remarks>
/// Columns are mapped by hand rather than with <c>GetRecords&lt;UsageLog&gt;()</c>: CsvHelper's
/// record mapping is source-generated and needs a <c>[CsvRecord]</c> partial type, which would
/// pull CsvHelper into m4dModels.
/// </remarks>
public static class UsageLogTsv
{
    private static readonly CsvReaderOptions Options = new()
    {
        Separator = '\t',
        Mode = CsvMode.NoEscape
    };

    /// <summary>
    /// Lazily reads UsageLog records from <paramref name="reader"/>. The first row is a header
    /// whose column names match the UsageLog properties; fields are neither quoted nor escaped.
    /// The reader is left open.
    /// </summary>
    public static IEnumerable<UsageLog> Read(TextReader reader)
    {
        using var csv = CsvReader.Create(reader, Options, leaveOpen: true);
        Columns columns = null;

        while (csv.Read())
        {
            columns ??= new Columns(csv);
            yield return new UsageLog
            {
                Id = csv.Parse<long>(columns.Id),
                UsageId = csv.GetString(columns.UsageId),
                UserName = csv.GetString(columns.UserName),
                Date = csv.Parse<DateTimeOffset>(columns.Date),
                Page = csv.GetString(columns.Page),
                Query = csv.GetString(columns.Query),
                Filter = csv.GetString(columns.Filter),
                Referrer = csv.GetString(columns.Referrer),
                UserAgent = csv.GetString(columns.UserAgent)
            };
        }
    }

    // Resolved once per file; GetFieldIndex throws when the header lacks a column.
    private sealed class Columns(CsvReader csv)
    {
        public readonly int Id = csv.GetFieldIndex(nameof(UsageLog.Id));
        public readonly int UsageId = csv.GetFieldIndex(nameof(UsageLog.UsageId));
        public readonly int UserName = csv.GetFieldIndex(nameof(UsageLog.UserName));
        public readonly int Date = csv.GetFieldIndex(nameof(UsageLog.Date));
        public readonly int Page = csv.GetFieldIndex(nameof(UsageLog.Page));
        public readonly int Query = csv.GetFieldIndex(nameof(UsageLog.Query));
        public readonly int Filter = csv.GetFieldIndex(nameof(UsageLog.Filter));
        public readonly int Referrer = csv.GetFieldIndex(nameof(UsageLog.Referrer));
        public readonly int UserAgent = csv.GetFieldIndex(nameof(UsageLog.UserAgent));
    }
}
