// Trains (or inspects) the Zstd dictionary used by m4dModels/SongPropertyCompression.cs to
// compress the Azure Search "Properties" field. See the compression-decision memory / SongIndex.cs
// for background. This project is intentionally not part of music4dance.sln - it's a maintenance
// tool you run by hand, not app code.
//
// Prerequisite: local/song-properties-samples.txt, one raw (uncompressed) property-log string per
// line. It's gitignored (real user data - titles, artists, usernames) so it must be regenerated
// locally from a fresh Azure Search / SQL export before (re)training; it is not checked in.
//
// Usage:
//   dotnet run -- train [--out path] [--samples N|full] [--capacity N]
//                       [--method legacy|fastcover|curated|hybrid] [--k N] [--d N] [--steps N] [--shrink]
//                       [--max-filler-line-bytes N]
//   dotnet run -- analyze <dict-path>     -- quantify how "generic vs. overfit" a dictionary's content is
//   dotnet run -- dump <dict-path>        -- print the readable strings a trained dictionary contains
//
// `train` with no flags trains the shipped recipe: `hybrid` method (see BuildCuratedContent /
// BuildFillerContent below) on the full sample corpus, 110 KiB, written to the next dictionary
// version path (refuses to overwrite an existing version file).
//
// Why `hybrid` and not plain `legacy`/`fastcover` training: COVER and FastCover only ever copy
// literal contiguous byte spans out of real samples - there's no way for them to learn a "field
// name only" template, and no combination of corpus size, capacity, segment length (k/d), or
// shrinkDict changed that (all tested and landing in the same ~2.2-2.33x compression band, see
// the compression-decision memory for the numbers). Worse, several genuinely valid field names
// (Comment+=, Choreographer+=, StepSheetUrl+=, PatternName+=, .Delete=, .Undo=, etc.) are rare
// enough in the corpus that COVER/FastCover never picked them at all in any variant tested - songs
// using those fields got zero dictionary benefit for that part of their content. `hybrid` uses
// ZDICT_finalizeDictionary to prepend a hand-picked field-name/qualifier vocabulary (guaranteeing
// every known field gets at least a partial-match prefix) and fills the remaining budget with real
// sample text (for the legitimate cross-record redundancy - shared genre-tag lists, shared album
// titles - that a names-only dictionary would lose). Costs ~3% ratio versus pure auto-training
// (2.25x vs 2.30-2.33x) in exchange for that coverage guarantee. Pure `curated` (no filler) was
// also tried and dropped the ratio to 1.69x - real repeated content matters more than field-name
// coverage alone.
//
// Filler candidates are deduped and length-capped (--max-filler-line-bytes, default 2048) before
// selection. Some songs have been reprocessed by the batch pipeline hundreds of times and their
// property-log line is dominated by repeated ".Edit=\tUser=batch-s|P..." history entries - real
// corpus lines run up to 58 KB, over half the dictionary capacity, in a single line. A trained
// dictionary only needs a byte-string once to be usable as a back-match reference for every future
// compression, so letting one such outlier line into the filler wastes a large fraction of the
// budget on a string that already appears elsewhere for zero additional ratio benefit. The cap
// excludes roughly the top 10% of lines by length (p90 is ~2 KB) while keeping the realistic bulk
// of the corpus.
//
// To ship a retrained dictionary:
//   1. Pick final parameters with `train --out local/candidate.dict ...` + `analyze`/`dump` (iterate
//      freely here - these are scratch files, not the shipped resource).
//   2. Bump DictionaryVersion below and re-run `train` with no --out (writes the real, version-guarded
//      Resources/song-properties.vN.dict). Never reuse or overwrite a prior version's file - old
//      compressed rows need that exact dictionary to decode.
//   3. Bump CurrentDictionaryVersion in m4dModels/SongPropertyCompression.cs to match.
//   4. Once every row in the index has been re-saved under the new version (e.g. via a full
//      search-and-reload pass over all songs), the old .dict file is no longer read and can be
//      deleted - keep at most the current version plus the immediately preceding one until you've
//      confirmed the reload pass reached every row.

using System.Diagnostics;
using System.Text;

using ZstdSharp;
using ZstdSharp.Unsafe;

const int DictionaryVersion = 1; // bump when retraining; never reuse or overwrite a prior version's file

// Property names pulled from architecture/song-internal-format.md - the vocabulary a "generic,
// well-generalizing" dictionary should mostly be built from, per field-name/qualifier rather than
// per literal value.
string[] GenericTokens =
[
    ".Create=", ".Edit=", ".Merge=", ".Delete=", ".Undo=", ".NoMerge=", ".FailedLookup=",
    "User=", "Time=", "Title=", "Artist=", "Tempo=", "Length=", "Sample=",
    "Danceability=", "Energy=", "Valence=", "DanceRating=",
    "Tag+=", "Tag-=", "DeleteTag=", "Comment+=", "Comment-=",
    "Choreographer+=", "Choreographer-=", "StepSheetUrl+=", "StepSheetUrl-=", "PatternName+=",
    "Album:", "Publisher:", "Track:", "Purchase:", "Purchase-:", "PromoteAlbum=", "OrderAlbums=",
    "Like=", "OwnerHash=", ":Music", ":Dance", ":Other", ":Tempo", ":Style", "|P"
];

switch (args.ElementAtOrDefault(0))
{
    case "dump":
        DumpDictionaryStrings(args.ElementAtOrDefault(1) ?? DefaultDictPath(DictionaryVersion));
        break;
    case "analyze":
        AnalyzeDictionary(args.ElementAtOrDefault(1) ?? DefaultDictPath(DictionaryVersion));
        break;
    case "train":
        Train(args.Skip(1).ToArray());
        break;
    case null:
        Train([]);
        break;
    default:
        Console.WriteLine($"Unknown command '{args[0]}'. Use train, analyze, or dump.");
        break;
}

return;

void Train(string[] trainArgs)
{
    var samplesArg = GetOption(trainArgs, "--samples") ?? "full";
    var capacity = int.Parse(GetOption(trainArgs, "--capacity") ?? "112640");
    var method = GetOption(trainArgs, "--method") ?? "hybrid";
    var k = int.Parse(GetOption(trainArgs, "--k") ?? "200");
    var d = int.Parse(GetOption(trainArgs, "--d") ?? "8");
    var steps = int.Parse(GetOption(trainArgs, "--steps") ?? "40");
    var shrink = trainArgs.Contains("--shrink");
    var maxFillerLineBytes = int.Parse(GetOption(trainArgs, "--max-filler-line-bytes") ?? "2048");
    var outPath = GetOption(trainArgs, "--out");
    const int RngSeed = 20260705;

    var samplesPath = Path.Combine("..", "..", "local", "song-properties-samples.txt");
    var allLines = File.ReadAllLines(samplesPath);
    Console.WriteLine($"Loaded {allLines.Length} property strings from {samplesPath}");

    var rng = new Random(RngSeed);
    var shuffled = allLines.OrderBy(_ => rng.Next()).ToArray();
    var holdoutLines = shuffled.Take(500).ToArray();
    var remaining = shuffled.Skip(500);
    var trainingLines = (samplesArg == "full" ? remaining : remaining.Take(int.Parse(samplesArg))).ToArray();

    var trainingSamples = trainingLines.Select(l => Encoding.UTF8.GetBytes(l)).ToList();
    var totalTrainingBytes = trainingSamples.Sum(s => (long)s.Length);
    Console.WriteLine($"Training on {trainingSamples.Count} samples, {totalTrainingBytes:N0} bytes total ({(double)totalTrainingBytes / capacity:F1}x dict capacity), method={method}");

    var sw = Stopwatch.StartNew();
    byte[] dict;
    if (method == "fastcover")
    {
        Console.WriteLine($"FastCover params: k={k} d={d} steps={steps} shrinkDict={shrink}");
        var fcParams = new ZDICT_fastCover_params_t
        {
            k = (uint)k,
            d = (uint)d,
            steps = (uint)steps,
            shrinkDict = shrink ? 1u : 0u
        };
        dict = DictBuilder.TrainFromBufferFastCover(trainingSamples, fcParams, capacity).ToArray();
    }
    else if (method == "curated")
    {
        var customContent = BuildCuratedContent();
        Console.WriteLine($"Curated content: {customContent.Length:N0} bytes of hand-picked field-name/qualifier tokens");
        dict = FinalizeDictionary(customContent, trainingSamples, capacity);
    }
    else if (method == "hybrid")
    {
        var curated = BuildCuratedContent();
        var fillerCandidates = trainingLines
            .Zip(trainingSamples, (line, bytes) => (line, bytes))
            .Where(t => t.bytes.Length <= maxFillerLineBytes)
            .Select(t => t.line)
            .Distinct()
            .ToArray();
        Console.WriteLine($"Filler candidates: {fillerCandidates.Length:N0} of {trainingLines.Length:N0} lines " +
                           $"(<= {maxFillerLineBytes:N0} bytes, deduped)");
        var filler = BuildFillerContent(fillerCandidates, capacity - curated.Length);
        var customContent = curated.Concat(filler).ToArray();
        Console.WriteLine($"Curated content: {curated.Length:N0} bytes + filler: {filler.Length:N0} bytes of real sample text");
        dict = FinalizeDictionary(customContent, trainingSamples, capacity);
    }
    else
    {
        dict = DictBuilder.TrainFromBuffer(trainingSamples, capacity);
    }
    sw.Stop();
    Console.WriteLine($"Trained dictionary: {dict.Length} bytes in {sw.Elapsed}");

    outPath ??= DefaultDictPath(DictionaryVersion);
    if (outPath == DefaultDictPath(DictionaryVersion) && File.Exists(outPath))
    {
        throw new InvalidOperationException(
            $"{outPath} already exists. Bump DictionaryVersion (or pass --out) instead of overwriting a " +
            "dictionary version that may already have compressed data written against it.");
    }
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
    File.WriteAllBytes(outPath, dict);
    Console.WriteLine($"Wrote dictionary to {Path.GetFullPath(outPath)}");

    EvaluateRoundTripAndRatio(dict, holdoutLines);
    Console.WriteLine();
    AnalyzeDictionary(outPath);
}

// Builds the dictionary "content" section ourselves - a plain field-name/qualifier vocabulary,
// with no per-record values attached - instead of letting COVER/FastCover pick literal substrings
// out of real samples (which always drags along whatever value happened to follow that field in
// whichever sample the segment was copied from). Expands the indexed families (Album/Track/
// Publisher/Purchase, 2-digit index per architecture/song-internal-format.md) across a plausible
// index range and the known Purchase qualifiers.
byte[] BuildCuratedContent()
{
    var tokens = new List<string>(GenericTokens);
    var qualifiers = new[] { "AS", "AD", "IS", "IA", "SS", "SA", "XS", "XA" };

    for (var i = 0; i <= 9; i++)
    {
        var idx = i.ToString("D2");
        tokens.Add($"Album:{idx}=");
        tokens.Add($"Track:{idx}=");
        tokens.Add($"Publisher:{idx}=");
        foreach (var q in qualifiers)
        {
            tokens.Add($"Purchase:{idx}:{q}=");
            tokens.Add($"Purchase-:{idx}:{q}=");
        }
    }

    return Encoding.UTF8.GetBytes(string.Join("\t", tokens) + "\t");
}

// Fills remaining dictionary budget with real, diverse sample lines verbatim (most-recently-useful
// last, since zstd's match-finder favors content nearer the end of the window) - a cheap
// approximation of what COVER/FastCover would pick, without needing to extract their internal
// content section (there's no public API to separate a trained dictionary's content from its
// header/entropy tables).
byte[] BuildFillerContent(string[] candidateLines, int budget)
{
    var buffer = new List<byte>(Math.Max(budget, 0));
    foreach (var line in candidateLines)
    {
        var bytes = Encoding.UTF8.GetBytes(line);
        if (buffer.Count + bytes.Length > budget)
        {
            continue;
        }
        buffer.AddRange(bytes);
        buffer.Add((byte)'\t');
    }
    return buffer.ToArray();
}

// ZDICT_finalizeDictionary attaches real entropy/Huffman tables (computed from the actual sample
// corpus) around dictionary "content" bytes we supply ourselves, instead of having the trainer pick
// the content. Only exposed as a raw unsafe P/Invoke-style export in ZstdSharp.Port 0.8.8 - no
// managed wrapper - so this pins everything by hand. zstd's error convention encodes failures as a
// size_t near SIZE_MAX; since a real result can never exceed the buffer capacity we passed in, any
// result bigger than that capacity is treated as an error.
unsafe byte[] FinalizeDictionary(byte[] customContent, IReadOnlyList<byte[]> samples, int dictCapacity)
{
    var dictBuffer = new byte[dictCapacity];
    var samplesBuffer = new byte[samples.Sum(s => (long)s.Length)];
    var offset = 0;
    foreach (var s in samples)
    {
        Buffer.BlockCopy(s, 0, samplesBuffer, offset, s.Length);
        offset += s.Length;
    }
    var samplesSizes = samples.Select(s => (nuint)s.Length).ToArray();

    fixed (byte* dictPtr = dictBuffer)
    fixed (byte* customPtr = customContent)
    fixed (byte* samplesPtr = samplesBuffer)
    fixed (nuint* sizesPtr = samplesSizes)
    {
        var result = Methods.ZDICT_finalizeDictionary(
            dictPtr, (nuint)dictBuffer.Length,
            customPtr, (nuint)customContent.Length,
            samplesPtr, sizesPtr, (uint)samples.Count,
            new ZDICT_params_t());

        if ((ulong)result > (ulong)dictBuffer.Length)
        {
            throw new InvalidOperationException(
                $"ZDICT_finalizeDictionary failed: {Methods.ZDICT_getErrorName(result)}");
        }

        return dictBuffer[..(int)result];
    }
}

void EvaluateRoundTripAndRatio(byte[] dict, string[] holdoutLines)
{
    using var compressor = new Compressor();
    compressor.LoadDictionary(dict);
    using var decompressor = new Decompressor();
    decompressor.LoadDictionary(dict);

    long origTotal = 0, compTotal = 0, base64Total = 0;
    foreach (var line in holdoutLines)
    {
        var src = Encoding.UTF8.GetBytes(line);
        var compressed = compressor.Wrap(src).ToArray();
        var decompressed = decompressor.Unwrap(compressed, src.Length).ToArray();
        if (!decompressed.SequenceEqual(src))
        {
            Console.WriteLine("ROUND-TRIP MISMATCH!");
            return;
        }
        origTotal += src.Length;
        compTotal += compressed.Length;
        base64Total += (compressed.Length + 2) / 3 * 4;
    }

    Console.WriteLine($"Holdout ({holdoutLines.Length} samples): original {origTotal:N0} bytes -> compressed {compTotal:N0} bytes -> base64 {base64Total:N0} bytes");
    Console.WriteLine($"Effective ratio (orig / base64): {(double)origTotal / base64Total:F2}x");
}

void AnalyzeDictionary(string path)
{
    var bytes = File.ReadAllBytes(path);
    // Field-level granularity (breaks on tab, like the original heuristic): the "long run = likely
    // overfit" signal is about individual field *values* being suspiciously long/specific, which only
    // holds if a run is one field, not an entire concatenated record chain.
    var runs = ExtractPrintableRuns(bytes, minRunLength: 6, includeTabs: false);
    var runLengths = runs.Select(r => r.Length).OrderBy(l => l).ToArray();

    Console.WriteLine($"=== Analysis: {path} ({bytes.Length:N0} bytes) ===");
    Console.WriteLine($"Printable runs (>=6 chars): {runs.Count}, total {runs.Sum(r => r.Length):N0} chars");
    if (runLengths.Length > 0)
    {
        Console.WriteLine($"Run length: min={runLengths[0]} median={runLengths[runLengths.Length / 2]} " +
                           $"mean={runLengths.Average():F0} max={runLengths[^1]}");
    }

    var longRunThreshold = 60;
    var longRuns = runs.Where(r => r.Length >= longRunThreshold).ToList();
    Console.WriteLine($"Long runs (>={longRunThreshold} chars, likely value-specific/overfit content): {longRuns.Count}");

    Console.WriteLine("\nGeneric property-name token hits (count of literal occurrences in the raw bytes):");
    var text = Encoding.Latin1.GetString(bytes); // 1 byte <-> 1 char, preserves offsets for counting
    foreach (var token in GenericTokens)
    {
        var count = CountOccurrences(text, token);
        if (count > 0)
        {
            Console.WriteLine($"  {token,-18} x{count}");
        }
    }
}

int CountOccurrences(string haystack, string needle)
{
    var count = 0;
    var i = 0;
    while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
    {
        count++;
        i += needle.Length;
    }
    return count;
}

// Tab is the field separator within a record and the join byte BuildFillerContent uses between
// concatenated records. `includeTabs: true` (dump) treats it as printable so a whole record (or run
// of records) reads as one block - useful for seeing what real content actually surrounds a value.
// `includeTabs: false` (analyze) breaks on it, so each run is one field - needed for the "long run =
// likely overfit" signal, which is only meaningful at field granularity: nearly every record chain
// exceeds 60 chars once tabs no longer split it, which would make that count meaningless.
List<string> ExtractPrintableRuns(byte[] bytes, int minRunLength, bool includeTabs)
{
    var runs = new List<string>();
    var current = new StringBuilder();

    void FlushRun()
    {
        if (current.Length >= minRunLength)
        {
            runs.Add(current.ToString());
        }
        current.Clear();
    }

    foreach (var b in bytes)
    {
        if (b is >= 0x20 and <= 0x7E || (includeTabs && b == 0x09))
        {
            current.Append((char)b);
        }
        else
        {
            FlushRun();
        }
    }
    FlushRun();

    return runs;
}

// Zstd dictionaries aren't just a blob of sample text - a header (magic number, dict ID, entropy/
// Huffman tables) precedes the raw "content" section that COVER copied verbatim from training
// samples. There's no public API to separate them, so this just runs a `strings`-style scan over
// the whole file: printable-ASCII runs long enough to be meaningful are almost always from the
// content section, since the entropy tables are dense binary data that rarely produces long
// printable runs by chance.
void DumpDictionaryStrings(string path)
{
    var bytes = File.ReadAllBytes(path);
    Console.WriteLine($"{path}: {bytes.Length:N0} bytes\n");

    var runs = ExtractPrintableRuns(bytes, minRunLength: 6, includeTabs: true);
    foreach (var run in runs.OrderByDescending(r => r.Length))
    {
        Console.WriteLine(run);
    }

    Console.WriteLine($"\n{runs.Count} printable runs >= 6 chars, {runs.Sum(r => r.Length):N0} chars total");
}

string DefaultDictPath(int version) =>
    Path.Combine("..", "..", "m4dModels", "Resources", $"song-properties.v{version}.dict");

string GetOption(string[] a, string name)
{
    var idx = Array.IndexOf(a, name);
    return idx >= 0 && idx + 1 < a.Length ? a[idx + 1] : null;
}
