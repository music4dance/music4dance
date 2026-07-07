using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;

using ZstdSharp;

namespace m4dModels;

// Compresses the Properties field stored in Azure Search: song property-log strings share
// a lot of vocabulary across records (.Edit=, User=batch-s|P, Purchase:00:SS=, Tag+=...:Dance,
// the Spotify preview URL prefix, etc.) that only a shared dictionary can exploit, since plain
// per-record compression can't see redundancy across records. See
// local/dict-trainer for how Resources/song-properties.v1.dict was trained.
//
// Values written before compression was added start with .Create=/.Edit=/.Merge= (the property
// log's action markers) which can never appear at the start of Base64 output, so that prefix
// doubles as the "is this legacy plain text" check on read.
//
// A handful of rows were, due to a since-fixed bug in Song.AdminEdit(string, ...), saved with a
// stray "SongId={guid}" property glued onto the front of the log (that method used to forget to
// strip the id header off the full serialized-song string admins submit). That prefix is treated
// as legacy plain text too so reads don't crash trying to Base64-decode it, and it's stripped so
// the bogus property doesn't keep reappearing on every future write.
//
// The frame written into the field is [1-byte dictionary version][4-byte original length][zstd
// bytes], Base64-encoded. The version byte lets the dictionary be retrained (e.g. after adding
// enough new property vocabulary that the old dictionary stops paying for itself) without
// breaking previously-written values: every dictionary version this field was ever written with
// must stay embedded as Resources/song-properties.v<N>.dict forever, or those rows become
// undecodable.
public static class SongPropertyCompression
{
    // Manual-testing escape hatch (set from the "FeatureManagement:SongPropertyCompression" config
    // value at startup, see Program.cs). Reads already handle both formats regardless of this flag,
    // so toggling it only changes what new writes look like.
    public static bool Enabled { get; set; } = true;

    private const byte CurrentDictionaryVersion = 1;

    private static readonly string[] LegacyPrefixes =
    [
        Song.CreateCommand + "=",
        Song.EditCommand + "=",
        Song.MergeCommand + "=",
        SongIndex.SongIdField + "="
    ];

    private static readonly IReadOnlyDictionary<byte, byte[]> DictionariesByVersion = LoadEmbeddedDictionaries();

    public static bool IsCompressed(string stored)
    {
        return stored != null && Array.TrueForAll(LegacyPrefixes, p => !stored.StartsWith(p, StringComparison.Ordinal));
    }

    public static string Compress(string properties)
    {
        if (!Enabled)
        {
            return properties;
        }

        var source = Encoding.UTF8.GetBytes(properties);

        using var compressor = new Compressor();
        compressor.LoadDictionary(DictionariesByVersion[CurrentDictionaryVersion]);
        var compressed = compressor.Wrap(source);

        const int headerSize = sizeof(byte) + sizeof(int);
        var framed = new byte[headerSize + compressed.Length];
        framed[0] = CurrentDictionaryVersion;
        BinaryPrimitives.WriteInt32LittleEndian(framed.AsSpan(sizeof(byte)), source.Length);
        compressed.CopyTo(framed.AsSpan(headerSize));

        return Convert.ToBase64String(framed);
    }

    public static string Decompress(string stored)
    {
        if (!IsCompressed(stored))
        {
            var ich = Song.TryParseId(stored, out _);
            return ich > 0 ? stored[ich..] : stored;
        }

        var framed = Convert.FromBase64String(stored);
        var version = framed[0];
        var originalLength = BinaryPrimitives.ReadInt32LittleEndian(framed.AsSpan(sizeof(byte)));

        if (!DictionariesByVersion.TryGetValue(version, out var dictionary))
        {
            throw new InvalidOperationException(
                $"Properties field was compressed with dictionary version {version}, which is not embedded in this build.");
        }

        const int headerSize = sizeof(byte) + sizeof(int);
        using var decompressor = new Decompressor();
        decompressor.LoadDictionary(dictionary);
        var decompressed = decompressor.Unwrap(framed.AsSpan(headerSize), originalLength);

        return Encoding.UTF8.GetString(decompressed);
    }

    private static IReadOnlyDictionary<byte, byte[]> LoadEmbeddedDictionaries()
    {
        var assembly = typeof(SongPropertyCompression).Assembly;
        var result = new Dictionary<byte, byte[]>();

        foreach (var name in assembly.GetManifestResourceNames())
        {
            var match = Regex.Match(name, @"song-properties\.v(\d+)\.dict$");
            if (!match.Success)
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(name);
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);

            result[byte.Parse(match.Groups[1].Value)] = buffer.ToArray();
        }

        return result;
    }
}
