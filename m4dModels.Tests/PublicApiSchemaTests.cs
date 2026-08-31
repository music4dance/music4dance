using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace m4dModels.Tests;

[TestClass]
public class PublicApiSchemaTests
{
    private const string PreviousMigration = "20260326002120_SearchMostRecentPage";
    private const string FoundationMigration = "20260831134022_DanzQApiFoundation";

    private static readonly string[] OpenIddictTables =
    [
        "OpenIddictApplications",
        "OpenIddictAuthorizations",
        "OpenIddictScopes",
        "OpenIddictTokens"
    ];

    [TestMethod]
    public void Model_ContainsOpenIddictStoreTables()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DanceMusicContext>();
        var tables = context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(name => name != null)
            .ToHashSet();

        foreach (var table in OpenIddictTables)
        {
            Assert.IsTrue(tables.Contains(table), $"Missing table mapping: {table}");
        }
    }

    [TestMethod]
    public void Migration_MatchesTheCurrentModel()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DanceMusicContext>();

        CollectionAssert.Contains(
            context.Database.GetMigrations().ToArray(),
            FoundationMigration);
        Assert.IsFalse(
            context.Database.HasPendingModelChanges(),
            "The model snapshot does not match DanceMusicContext.");
    }

    [TestMethod]
    public void Migration_GeneratesReversibleSql()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DanceMusicContext>();
        var migrator = context.GetService<IMigrator>();
        var up = migrator.GenerateScript(PreviousMigration, FoundationMigration);
        var down = migrator.GenerateScript(FoundationMigration, PreviousMigration);

        foreach (var table in OpenIddictTables)
        {
            StringAssert.Contains(up, $"CREATE TABLE [{table}]");
            StringAssert.Contains(down, $"DROP TABLE [{table}]");
        }
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<DanceMusicContext>(options =>
            options.UseSqlServer(
                    "Server=localhost;Database=unused;Integrated Security=True;Encrypt=False")
                .UseOpenIddict());
        services.AddIdentityCore<ApplicationUser>(options =>
                options.Stores.MaxLengthForKeys = 128)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<DanceMusicContext>();
        return services.BuildServiceProvider();
    }
}
