using System.Reflection;
using System.Security.Claims;
using System.Text.Json;

using m4d.Controllers;
using m4d.Services;
using m4d.Services.ServiceHealth;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace m4d.Tests.Controllers;

/// <summary>
/// /api/health/status is public (every page's status banner polls it), but the error text is
/// often a raw exception message, so only diagnostics users see it. /api/health/report is
/// diagnostics-only.
/// </summary>
[TestClass]
public class HealthControllerTests
{
    private const string Failure = "SqlException: Cannot open server 'internal-name'";

    private static HealthController Controller(params string[] roles)
    {
        var health = new ServiceHealthManager(NullLogger<ServiceHealthManager>.Instance);
        health.MarkUnavailable("Database", Failure);
        health.MarkHealthy("SearchService", TimeSpan.FromMilliseconds(42));

        var files = new Mock<IUpdateMessageFileManager>();
        var updateMessage = new UpdateMessageService(
            NullLogger<UpdateMessageService>.Instance, files.Object, DateTime.UtcNow);

        var identity = roles.Length == 0
            ? new ClaimsIdentity()
            : new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "dwgray"), .. roles.Select(r => new Claim(ClaimTypes.Role, r))],
                "Test");

        return new HealthController(health, updateMessage, NullLogger<HealthController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
            },
        };
    }

    private static Dictionary<string, JsonElement> Services(IActionResult result)
    {
        var json = JsonSerializer.Serialize(((ObjectResult)result).Value);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("services").EnumerateArray()
            .ToDictionary(s => s.GetProperty("name").GetString()!, s => s.Clone());
    }

    [TestMethod]
    [DataRow(new string[0])]
    [DataRow(new[] { "canEdit", "dbAdmin" })]
    public void Status_WithoutDiagnosticsRole_HidesErrorTextAndTimings(string[] roles)
    {
        var result = Controller(roles).GetStatus();

        Assert.AreEqual(StatusCodes.Status503ServiceUnavailable, ((ObjectResult)result).StatusCode);
        var services = Services(result);
        Assert.AreEqual("unavailable", services["Database"].GetProperty("status").GetString());
        Assert.AreEqual(JsonValueKind.Null, services["Database"].GetProperty("errorMessage").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, services["Database"].GetProperty("consecutiveFailures").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, services["SearchService"].GetProperty("responseTime").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, services["SearchService"].GetProperty("lastHealthy").ValueKind);
        Assert.AreEqual(JsonValueKind.String, services["SearchService"].GetProperty("lastChecked").ValueKind);
    }

    [TestMethod]
    public void Status_WithDiagnosticsRole_IncludesErrorTextAndTimings()
    {
        var services = Services(Controller(HealthController.DetailRole).GetStatus());

        Assert.AreEqual(Failure, services["Database"].GetProperty("errorMessage").GetString());
        Assert.AreEqual(42, services["SearchService"].GetProperty("responseTime").GetDouble());
        Assert.AreEqual(1, services["Database"].GetProperty("consecutiveFailures").GetInt32());
        Assert.AreEqual(JsonValueKind.String, services["SearchService"].GetProperty("lastHealthy").ValueKind);
    }

    [TestMethod]
    public void OnlyTheReportRequiresTheDiagnosticsRole()
    {
        var gated = typeof(HealthController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<AuthorizeAttribute>().Any())
            .ToList();

        Assert.HasCount(1, gated);
        Assert.AreEqual(nameof(HealthController.GetReport), gated[0].Name);
        Assert.AreEqual(HealthController.DetailRole, gated[0].GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.IsNull(typeof(HealthController).GetCustomAttribute<AuthorizeAttribute>());
    }
}
