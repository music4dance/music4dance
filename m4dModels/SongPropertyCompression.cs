using System.IO.Compression;
using System.Text;

namespace m4dModels;

// Compresses the Properties field stored in Azure Search, but only for the rare record large
// enough to risk exceeding the field's size limit (the largest real record observed is 58,590
// bytes — one song reprocessed hundreds of times by the automated import pipeline). Typical
// records are left as plain text: Azure Search's own index storage already block-compresses
// stored fields across documents, which exploits cross-record redundancy (shared field names,
// shared tag vocab, the common batch-import literals) far better than per-record compression can,
// so compressing every record was a net storage *regression* — see
// architecture/song-internal-format.md §11 for the measured numbers that motivated gating this on
// record size instead.
//
// Values that were left as plain text (either because compression is disabled, or because they're
// under the size threshold) start with .Create=/.Edit=/.Merge= (the property log's action markers)
// which can never appear at the start of Base64 output, so that prefix doubles as the
// "is this compressed" check on read.
//
// A handful of rows were, due to a since-fixed bug in Song.AdminEdit(string, ...), saved with a
// stray "SongId={guid}" property glued onto the front of the log (that method used to forget to
// strip the id header off the full serialized-song string admins submit). That prefix is treated
// as plain text too so reads don't crash trying to Base64-decode it, and it's stripped so the
// bogus property doesn't keep reappearing on every future write.
public static class SongPropertyCompression
{
    // Manual-testing escape hatch (set from the "FeatureManagement:SongPropertyCompression" config
    // value at startup, see Program.cs). Reads already handle both formats regardless of this flag,
    // so toggling it only changes what new writes look like.
    public static bool Enabled { get; set; } = true;

    // Below this, Azure Search's own stored-field compression already handles it better than a
    // per-record pass can; only records near the field's size limit are worth compressing here.
    // Picked from a corpus-wide line-length distribution (scripts/line-length-stats.ps1).
    private const int CompressionThreshold = 10_000;

    private static readonly string[] LegacyPrefixes =
    [
        Song.CreateCommand + "=",
        Song.EditCommand + "=",
        Song.MergeCommand + "=",
        SongIndex.SongIdField + "="
    ];

    public static bool IsCompressed(string stored)
    {
        return stored != null && Array.TrueForAll(LegacyPrefixes, p => !stored.StartsWith(p, StringComparison.Ordinal));
    }

    public static string Compress(string properties)
    {
        if (!Enabled || properties.Length <= CompressionThreshold)
        {
            return properties;
        }

        var source = Encoding.UTF8.GetBytes(properties);

        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.Optimal))
        {
            brotli.Write(source);
        }

        return Convert.ToBase64String(output.ToArray());
    }

    public static string Decompress(string stored)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return stored;
        }

        if (!IsCompressed(stored))
        {
            var ich = Song.TryParseId(stored, out _);
            return ich > 0 ? stored[ich..] : stored;
        }

        var compressed = Convert.FromBase64String(stored);

        using var input = new MemoryStream(compressed);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        brotli.CopyTo(output);

        return Encoding.UTF8.GetString(output.ToArray());
    }
}
