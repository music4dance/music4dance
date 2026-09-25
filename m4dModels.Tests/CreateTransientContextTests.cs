using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

/// <summary>
/// Covers <see cref="DanceMusicContext.CreateTransientContext"/>'s ability to recover the
/// configuration of the context it was cloned from. Both branches read that configuration back
/// out of <see cref="DbContextOptions"/>, which is easy to break silently: the connection string
/// comes from the public RelationalOptionsExtension base rather than the provider's own internal
/// options type, and a provider swap or EF upgrade that moved it would leave
/// CreateTransientContext throwing "Cannot create a new dbcontext from a test context" instead
/// of failing at build time.
/// </summary>
[TestClass]
public class CreateTransientContextTests
{
    // Never opened - CreateTransientContext only copies the connection string, it does not connect.
    private const string FakeConnectionString =
        "Server=localhost;Database=TransientContextTests;Trusted_Connection=True";

    [TestMethod]
    public void CreateTransientContext_SqlServer_CarriesConnectionStringForward()
    {
        var options = new DbContextOptionsBuilder<DanceMusicContext>()
            .UseSqlServer(FakeConnectionString)
            .Options;

        using var context = new DanceMusicContext(options);
        using var transient = context.CreateTransientContext();

        Assert.AreNotSame(context, transient);

        // SqlClient canonicalizes keywords and appends its own Application Name, so compare the
        // parsed target rather than the raw string.
        var carried = new SqlConnectionStringBuilder(transient.Database.GetConnectionString());
        var expected = new SqlConnectionStringBuilder(FakeConnectionString);
        Assert.AreEqual(expected.DataSource, carried.DataSource);
        Assert.AreEqual(expected.InitialCatalog, carried.InitialCatalog);
    }

    [TestMethod]
    public void CreateTransientContext_InMemory_SharesTheStoreWithItsParent()
    {
        var storeName = $"transient-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<DanceMusicContext>()
            .UseInMemoryDatabase(storeName)
            .Options;

        using var context = new DanceMusicContext(options);
        _ = context.TagGroups.Add(new TagGroup { Key = "Transient:Test", Count = 7 });
        _ = context.SaveChanges();

        using var transient = context.CreateTransientContext();

        Assert.AreNotSame(context, transient);
        // A different store name would leave this null, which is exactly the playlist bug the
        // InMemoryStoreName plumbing exists to prevent.
        Assert.IsNotNull(transient.TagGroups.Find("Transient:Test"));
    }
}
