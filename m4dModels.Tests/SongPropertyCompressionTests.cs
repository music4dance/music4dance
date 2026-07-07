using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

[TestClass]
public class SongPropertyCompressionTests
{
    private const string SampleProperties =
        ".Create=\tUser=dwgray\tTime=07/04/2026 09:03:55\tTitle=Test Song\tArtist=Test Artist\t" +
        "Tag+=Halloween:Other|Holiday:Other\t.Edit=\tUser=batch-s|P\tTime=07/04/2026 09:03:56\tTempo=138.0";

    [TestMethod]
    public void Compress_RoundTrips()
    {
        var compressed = SongPropertyCompression.Compress(SampleProperties);
        var decompressed = SongPropertyCompression.Decompress(compressed);

        Assert.AreEqual(SampleProperties, decompressed);
    }

    [TestMethod]
    public void Compress_ProducesSmallerBase64ThanOriginal()
    {
        var compressed = SongPropertyCompression.Compress(SampleProperties);

        Assert.IsTrue(compressed.Length < SampleProperties.Length);
    }

    [TestMethod]
    [DataRow(Song.CreateCommand)]
    [DataRow(Song.EditCommand)]
    [DataRow(Song.MergeCommand)]
    public void IsCompressed_FalseForLegacyPlainTextPrefixes(string command)
    {
        var legacyValue = $"{command}=\tUser=dwgray\tTime=07/04/2026 09:03:55";

        Assert.IsFalse(SongPropertyCompression.IsCompressed(legacyValue));
    }

    [TestMethod]
    public void IsCompressed_TrueForCompressedValue()
    {
        var compressed = SongPropertyCompression.Compress(SampleProperties);

        Assert.IsTrue(SongPropertyCompression.IsCompressed(compressed));
    }

    [TestMethod]
    [DataRow(Song.CreateCommand)]
    [DataRow(Song.EditCommand)]
    [DataRow(Song.MergeCommand)]
    public void Decompress_PassesThroughLegacyPlainText(string command)
    {
        var legacyValue = $"{command}=\tUser=dwgray\tTime=07/04/2026 09:03:55\tTitle=Test Song";

        Assert.AreEqual(legacyValue, SongPropertyCompression.Decompress(legacyValue));
    }

    [TestMethod]
    public void IsCompressed_FalseForStraySongIdPrefix()
    {
        var legacyValue = "SongId={08f6d679-537b-42ba-8317-4e8c6b61bd19}\t.Create=\tUser=dwgray\tTitle=Test Song";

        Assert.IsFalse(SongPropertyCompression.IsCompressed(legacyValue));
    }

    [TestMethod]
    public void Decompress_StripsStraySongIdPrefix()
    {
        var legacyValue = "SongId={08f6d679-537b-42ba-8317-4e8c6b61bd19}\t.Create=\tUser=dwgray\tTitle=Test Song";

        Assert.AreEqual(".Create=\tUser=dwgray\tTitle=Test Song", SongPropertyCompression.Decompress(legacyValue));
    }

    [TestMethod]
    public void Decompress_ThrowsForUnknownDictionaryVersion()
    {
        var framed = Convert.FromBase64String(SongPropertyCompression.Compress(SampleProperties));
        framed[0] = 255; // no dictionary this build ships is ever expected to reach this version
        var tampered = Convert.ToBase64String(framed);

        _ = Assert.ThrowsExactly<InvalidOperationException>(() => SongPropertyCompression.Decompress(tampered));
    }

    [TestMethod]
    public void Compress_PassesThroughPlainTextWhenDisabled()
    {
        SongPropertyCompression.Enabled = false;
        try
        {
            var result = SongPropertyCompression.Compress(SampleProperties);

            Assert.AreEqual(SampleProperties, result);
            Assert.IsFalse(SongPropertyCompression.IsCompressed(result));
        }
        finally
        {
            SongPropertyCompression.Enabled = true;
        }
    }

    [TestMethod]
    public void Decompress_StillReadsCompressedValuesWhenDisabled()
    {
        var compressed = SongPropertyCompression.Compress(SampleProperties);

        SongPropertyCompression.Enabled = false;
        try
        {
            Assert.AreEqual(SampleProperties, SongPropertyCompression.Decompress(compressed));
        }
        finally
        {
            SongPropertyCompression.Enabled = true;
        }
    }
}
