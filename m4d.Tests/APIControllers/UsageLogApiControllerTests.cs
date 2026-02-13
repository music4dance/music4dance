using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using m4d.APIControllers;
using m4d.Tests.TestHelpers;
using m4dModels;
using m4dModels.Tests;
using System.Security.Claims;
using System.Security.Principal;
using System.Text.Json;

namespace m4d.Tests.APIControllers;

/// <summary>
/// Integration tests for UsageLogController.
/// Tests the controller logic including request validation and task enqueueing.
/// Background task execution is tested separately (would require full service provider setup).
/// </summary>
[TestClass]
public class UsageLogControllerIntegrationTests
{
    private static IConfiguration CreateTestConfiguration()
    {
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["UsageTracking:Enabled"] = "true",
            ["UsageTracking:AnonymousThreshold"] = "3",
            ["UsageTracking:AnonymousBatchSize"] = "5",
            ["UsageTracking:AuthenticatedBatchSize"] = "1",
            ["UsageTracking:MaxQueueSize"] = "100"
        });
        return configBuilder.Build();
    }

    private static (UsageLogController controller, TestBackgroundTaskQueue taskQueue) CreateController(
        DanceMusicService dms, IConfiguration? config = null)
    {
        config ??= CreateTestConfiguration();
        var taskQueue = new TestBackgroundTaskQueue();

        // Access internal DanceStatsManager via reflection
        // C# compiler generates a backing field like <PropertyName>k__BackingField for auto-properties
        var danceStatsManagerField = typeof(DanceMusicCoreService)
            .GetField("<DanceStatsManager>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (danceStatsManagerField == null)
        {
            throw new InvalidOperationException("Could not access DanceStatsManager field via reflection. " +
                "This test infrastructure needs updating if the property implementation changed.");
        }

        var danceStats = (IDanceStatsManager)danceStatsManagerField.GetValue(dms)!;

        var controller = new UsageLogController(
            dms.Context,
            dms.UserManager,
            dms.SearchService,
            danceStats,
            config,
            NullLogger<UsageLogController>.Instance,
            taskQueue
        );

        return (controller, taskQueue);
    }

    [TestMethod]
    public async Task LogBatch_ValidAnonymousRequest_AcceptsAndEnqueuesTask()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("UsageLog_Anonymous");
        var (controller, taskQueue) = CreateController(dms);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var events = new List<UsageEventDto>
        {
            new() {
                UsageId = Guid.NewGuid().ToString(),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Page = "/test/page",
                Query = "?filter=test",
                Referrer = "https://google.com",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                Filter = "test-filter"
            }
        };

        var eventsJson = JsonSerializer.Serialize(events);

        // Act
        var result = await controller.LogBatch(eventsJson);

        // Assert
        Assert.IsInstanceOfType<AcceptedResult>(result, "Should return 202 Accepted");
        Assert.AreEqual(1, taskQueue.Count, "Should enqueue exactly one background task");
    }

    [TestMethod]
    public async Task LogBatch_AuthenticatedUser_AcceptsAndEnqueuesTask()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("UsageLog_Authenticated");
        var (controller, taskQueue) = CreateController(dms);

        // Set up authenticated user context
        var identity = new GenericIdentity("dwgray", "TestAuthentication");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var events = new List<UsageEventDto>
        {
            new() {
                UsageId = Guid.NewGuid().ToString(),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Page = "/admin",
                UserAgent = "Mozilla/5.0",
            }
        };

        var eventsJson = JsonSerializer.Serialize(events);

        // Act
        var result = await controller.LogBatch(eventsJson);

        // Assert
        Assert.IsInstanceOfType<AcceptedResult>(result, "Should return 202 Accepted for authenticated user");
        Assert.AreEqual(1, taskQueue.Count, "Should enqueue exactly one background task");
    }

    [TestMethod]
    public async Task LogBatch_MultipleEvents_AcceptsAndEnqueuesTask()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("UsageLog_Multiple");
        var (controller, taskQueue) = CreateController(dms);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var events = new List<UsageEventDto>
        {
            new() {
                UsageId = Guid.NewGuid().ToString(),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Page = "/page1",
                UserAgent = "Mozilla/5.0"
            },
            new() {
                UsageId = Guid.NewGuid().ToString(),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 1000,
                Page = "/page2",
                UserAgent = "Mozilla/5.0"
            },
            new() {
                UsageId = Guid.NewGuid().ToString(),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 2000,
                Page = "/page3",
                UserAgent = "Mozilla/5.0"
            }
        };

        var eventsJson = JsonSerializer.Serialize(events);

        // Act
        var result = await controller.LogBatch(eventsJson);

        // Assert
        Assert.IsInstanceOfType<AcceptedResult>(result);
        Assert.AreEqual(1, taskQueue.Count, "Should enqueue one task for batch of 3 events");
    }

    [TestMethod]
    public async Task LogBatch_EmptyEvents_ReturnsBadRequest()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("UsageLog_Empty");
        var (controller, taskQueue) = CreateController(dms);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var events = new List<UsageEventDto>();
        var eventsJson = JsonSerializer.Serialize(events);

        // Act
        var result = await controller.LogBatch(eventsJson);

        // Assert
        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
        var badRequest = (BadRequestObjectResult)result;
        Assert.AreEqual("No events provided", badRequest.Value);
        Assert.AreEqual(0, taskQueue.Count, "Should not enqueue any tasks");
    }

    [TestMethod]
    public async Task LogBatch_NullRequest_ReturnsBadRequest()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("UsageLog_Null");
        var (controller, taskQueue) = CreateController(dms);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act
        var result = await controller.LogBatch(null!);

        // Assert
        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
        Assert.AreEqual(0, taskQueue.Count, "Should not enqueue any tasks");
    }

    [TestMethod]
    public async Task LogBatch_TooManyEvents_ReturnsBadRequest()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("UsageLog_TooMany");
        var (controller, taskQueue) = CreateController(dms);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Create 101 events (over the limit of 100)
        var events = Enumerable.Range(0, 101).Select(i => new UsageEventDto
        {
            UsageId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Page = $"/test{i}",
            UserAgent = "Mozilla/5.0"
        }).ToList();

        var eventsJson = JsonSerializer.Serialize(events);

        // Act
        var result = await controller.LogBatch(eventsJson);

        // Assert
        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
        var badRequest = (BadRequestObjectResult)result;
        Assert.AreEqual("Batch size exceeds limit (100 events)", badRequest.Value);
        Assert.AreEqual(0, taskQueue.Count, "Should not enqueue any tasks for oversized batch");
    }

    [TestMethod]
    public void UsageEventDto_RequiredFields_HaveRequiredAttribute()
    {
        // Arrange & Act - Check that required fields have [Required] attribute
        var usageIdProperty = typeof(UsageEventDto).GetProperty(nameof(UsageEventDto.UsageId));
        var timestampProperty = typeof(UsageEventDto).GetProperty(nameof(UsageEventDto.Timestamp));
        var pageProperty = typeof(UsageEventDto).GetProperty(nameof(UsageEventDto.Page));
        var userAgentProperty = typeof(UsageEventDto).GetProperty(nameof(UsageEventDto.UserAgent));

        // Assert
        Assert.IsNotNull(usageIdProperty, "UsageId property should exist");
        Assert.IsTrue(usageIdProperty.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false).Any(),
            "UsageId should have [Required] attribute");

        Assert.IsNotNull(timestampProperty, "Timestamp property should exist");
        Assert.IsTrue(timestampProperty.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false).Any(),
            "Timestamp should have [Required] attribute");

        Assert.IsNotNull(pageProperty, "Page property should exist");
        Assert.IsTrue(pageProperty.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false).Any(),
            "Page should have [Required] attribute");

        Assert.IsNotNull(userAgentProperty, "UserAgent property should exist");
        Assert.IsTrue(userAgentProperty.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false).Any(),
            "UserAgent should have [Required] attribute");
    }

    [TestMethod]
    public void UsageEventDto_OptionalFields_DoNotHaveRequiredAttribute()
    {
        // Arrange & Act - Check that optional fields don't have [Required] attribute
        var queryProperty = typeof(UsageEventDto).GetProperty(nameof(UsageEventDto.Query));
        var referrerProperty = typeof(UsageEventDto).GetProperty(nameof(UsageEventDto.Referrer));
        var filterProperty = typeof(UsageEventDto).GetProperty(nameof(UsageEventDto.Filter));
        var userNameProperty = typeof(UsageEventDto).GetProperty(nameof(UsageEventDto.UserName));

        // Assert
        Assert.IsNotNull(queryProperty, "Query property should exist");
        Assert.IsFalse(queryProperty.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false).Any(),
            "Query should NOT have [Required] attribute");

        Assert.IsNotNull(referrerProperty, "Referrer property should exist");
        Assert.IsFalse(referrerProperty.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false).Any(),
            "Referrer should NOT have [Required] attribute");

        Assert.IsNotNull(filterProperty, "Filter property should exist");
        Assert.IsFalse(filterProperty.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false).Any(),
            "Filter should NOT have [Required] attribute");

        Assert.IsNotNull(userNameProperty, "UserName property should exist");
        Assert.IsFalse(userNameProperty.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false).Any(),
            "UserName should NOT have [Required] attribute");
    }
}
