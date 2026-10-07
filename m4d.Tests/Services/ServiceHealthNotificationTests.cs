using m4d.Services.ServiceHealth;

using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4d.Tests.Services;

// The admin emails: failure, recovery (closing an incident a failure email opened), the startup
// status report, and the cooldown that keeps a flapping service from flooding the inbox.
[TestClass]
public class ServiceHealthNotificationTests
{
    private const string ServiceName = "SearchService";

    private sealed class RecordingEmailSender : IEmailSender
    {
        private readonly List<string> _subjects = [];

        public IReadOnlyList<string> Subjects
        {
            get
            {
                lock (_subjects)
                {
                    return _subjects.ToList();
                }
            }
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            lock (_subjects)
            {
                _subjects.Add(subject);
            }
            return Task.CompletedTask;
        }
    }

    private static (ServiceHealthManager health, RecordingEmailSender sender) Create(
        bool enabled = true, bool startupStatus = true)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceHealth:AdminNotifications:Enabled"] = enabled ? "true" : "false",
                ["ServiceHealth:AdminNotifications:StartupStatus"] = startupStatus ? "true" : "false",
                ["ServiceHealth:AdminNotifications:Recipients:0"] = "admin@example.com",
                ["WEBSITE_SITE_NAME"] = "m4d-test"
            })
            .Build();
        var sender = new RecordingEmailSender();
        var health = new ServiceHealthManager(NullLogger<ServiceHealthManager>.Instance);
        health.SetNotifier(new ServiceHealthNotifier(
            configuration, () => sender, NullLogger<ServiceHealthNotifier>.Instance));
        return (health, sender);
    }

    private static async Task<IReadOnlyList<string>> WaitForEmails(RecordingEmailSender sender, int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (sender.Subjects.Count < count && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
        // Give any unexpected extra email a moment to show up
        await Task.Delay(50);
        return sender.Subjects;
    }

    [TestMethod]
    public async Task FailureThenRecovery_SendsBothEmails()
    {
        var (health, sender) = Create();
        health.MarkHealthy(ServiceName);

        health.MarkUnavailable(ServiceName, "429 throttled");
        _ = await WaitForEmails(sender, 1);
        health.MarkHealthy(ServiceName);
        var subjects = await WaitForEmails(sender, 2);

        Assert.HasCount(2, subjects);
        StringAssert.StartsWith(subjects[0], "[music4dance.net] Service Failure: SearchService");
        StringAssert.StartsWith(subjects[1], "[music4dance.net] Service Recovered: SearchService");
        StringAssert.EndsWith(subjects[1], "(m4d-test)");
    }

    [TestMethod]
    public async Task HealthyCalls_WithoutFailure_SendNothing()
    {
        var (health, sender) = Create();

        health.MarkHealthy(ServiceName);
        health.MarkHealthy(ServiceName);

        Assert.IsEmpty(await WaitForEmails(sender, 1));
    }

    [TestMethod]
    public async Task Flapping_WithinCooldown_SendsOnlyFirstIncident()
    {
        var (health, sender) = Create();
        health.NotificationCooldown = TimeSpan.FromMinutes(30);

        health.MarkUnavailable(ServiceName, "429 throttled");
        _ = await WaitForEmails(sender, 1);
        health.MarkHealthy(ServiceName);
        _ = await WaitForEmails(sender, 2);

        // Second flap inside the cooldown: no failure email, so no recovery email either
        health.MarkUnavailable(ServiceName, "429 throttled");
        health.MarkHealthy(ServiceName);

        Assert.HasCount(2, await WaitForEmails(sender, 3));
    }

    [TestMethod]
    public async Task Flapping_AfterCooldown_SendsAgain()
    {
        var (health, sender) = Create();
        health.NotificationCooldown = TimeSpan.Zero;

        health.MarkUnavailable(ServiceName, "429 throttled");
        _ = await WaitForEmails(sender, 1);
        health.MarkHealthy(ServiceName);
        _ = await WaitForEmails(sender, 2);
        health.MarkUnavailable(ServiceName, "429 throttled");

        Assert.HasCount(3, await WaitForEmails(sender, 3));
    }

    [TestMethod]
    public async Task DisabledNotifications_NoRecoveryEmailLater()
    {
        // The App Configuration-down case: the failure can't be emailed, so the incident isn't
        // marked notified and its recovery doesn't send a lone "recovered" email
        var (health, sender) = Create(enabled: false);

        health.MarkUnavailable(ServiceName, "429 throttled");
        await Task.Delay(100);
        Assert.IsFalse(health.GetServiceStatus(ServiceName).NotificationSent);

        health.MarkHealthy(ServiceName);
        Assert.IsEmpty(await WaitForEmails(sender, 1));
    }

    [TestMethod]
    public async Task StartupStatus_ReportsHealthyOrDegraded()
    {
        var (health, sender) = Create();
        health.MarkHealthy("Database");
        await health.SendStartupStatusNotificationAsync();

        health.MarkUnavailable("Database", "login failed");
        _ = await WaitForEmails(sender, 2);
        await health.SendStartupStatusNotificationAsync();

        var subjects = await WaitForEmails(sender, 3);
        StringAssert.StartsWith(subjects[0], "[music4dance.net] Status: Started healthy");
        StringAssert.StartsWith(subjects[2], "[music4dance.net] Status: Started degraded (1 service(s)");
    }

    [TestMethod]
    public async Task StartupStatus_NotSentUnlessConfigured()
    {
        var (health, sender) = Create(startupStatus: false);
        health.MarkHealthy("Database");

        await health.SendStartupStatusNotificationAsync();

        Assert.IsEmpty(await WaitForEmails(sender, 1));
    }
}
