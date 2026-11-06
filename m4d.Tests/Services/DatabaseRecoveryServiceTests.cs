#nullable disable

using m4d.Services;
using m4d.Services.ServiceHealth;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4d.Tests.Services;

// A deploy that lands while the on-demand test database is waking up fails its startup migration.
// Recovery used to reconnect and mark the database healthy on the old schema, so every page that
// read a table the release added threw until the next restart. These cover the retry.
[TestClass]
public class DatabaseRecoveryServiceTests
{
    private const string ConnectionString = "Server=tcp:example.invalid;Database=m4d";

    private sealed class FakeRecoveryService(ServiceHealthManager health)
        : DatabaseRecoveryService(
            // An empty provider: post-recovery FixupStats can't resolve its services, which the
            // service already logs and tolerates.
            new ServiceCollection().BuildServiceProvider(),
            health,
            new ConfigurationBuilder().Build(),
            NullLogger<DatabaseRecoveryService>.Instance)
    {
        public bool CanConnect { get; set; } = true;
        public Exception MigrationError { get; set; }
        public int MigrateCalls { get; private set; }

        internal override Task<bool> ProbeAsync(string connectionString) => Task.FromResult(CanConnect);

        internal override Task MigrateAsync(string connectionString)
        {
            MigrateCalls++;
            return MigrationError == null ? Task.CompletedTask : Task.FromException(MigrationError);
        }
    }

    private static (ServiceHealthManager, FakeRecoveryService) Create()
    {
        var health = new ServiceHealthManager(NullLogger<ServiceHealthManager>.Instance);
        health.MarkUnavailable("Database", "Migration failed: transient");
        return (health, new FakeRecoveryService(health));
    }

    private static ServiceStatus DatabaseStatus(ServiceHealthManager health) =>
        health.GetServiceStatus("Database").Status;

    [TestMethod]
    public async Task Recovery_MigrationsDeferred_MigratesThenMarksHealthy()
    {
        var (health, service) = Create();
        service.DeferMigrations();

        await service.AttemptRecoveryAsync(ConnectionString);

        Assert.AreEqual(1, service.MigrateCalls);
        Assert.IsFalse(service.MigrationsDeferred);
        Assert.AreEqual(ServiceStatus.Healthy, DatabaseStatus(health));
    }

    [TestMethod]
    public async Task Recovery_MigrationFails_StaysUnavailableAndRetriesNextTime()
    {
        var (health, service) = Create();
        service.DeferMigrations();
        service.MigrationError = new InvalidOperationException("still waking up");

        await service.AttemptRecoveryAsync(ConnectionString);

        Assert.AreEqual(ServiceStatus.Unavailable, DatabaseStatus(health));
        Assert.IsTrue(service.MigrationsDeferred);

        service.MigrationError = null;
        await service.AttemptRecoveryAsync(ConnectionString);

        Assert.AreEqual(2, service.MigrateCalls);
        Assert.IsFalse(service.MigrationsDeferred);
        Assert.AreEqual(ServiceStatus.Healthy, DatabaseStatus(health));
    }

    [TestMethod]
    public async Task Recovery_MigrationsNotDeferred_DoesNotMigrate()
    {
        // Startup ran the migrations, or skipped them on purpose (PROD_DB / TEST_DB).
        var (health, service) = Create();

        await service.AttemptRecoveryAsync(ConnectionString);

        Assert.AreEqual(0, service.MigrateCalls);
        Assert.AreEqual(ServiceStatus.Healthy, DatabaseStatus(health));
    }

    [TestMethod]
    public async Task Recovery_CannotConnect_LeavesMigrationsDeferred()
    {
        var (health, service) = Create();
        service.DeferMigrations();
        service.CanConnect = false;

        await service.AttemptRecoveryAsync(ConnectionString);

        Assert.AreEqual(0, service.MigrateCalls);
        Assert.IsTrue(service.MigrationsDeferred);
        Assert.AreEqual(ServiceStatus.Unavailable, DatabaseStatus(health));
    }
}
