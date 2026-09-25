using m4d.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using System.Reflection;

namespace m4d.Tests.Controllers;

/// <summary>
/// PlayListController has no class-level [Authorize], so every action must carry its own
/// [Authorize] (admin actions) or an explicit [AllowAnonymous] (token-gated UpdateBatch).
/// Restore/RestoreAll once shipped with neither, letting anonymous requests kick off restore jobs.
/// </summary>
[TestClass]
public class PlayListControllerTests
{
    [TestMethod]
    public void EveryActionDeclaresAuthorization()
    {
        var unguarded = typeof(PlayListController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() == null)
            .Where(m => m.GetCustomAttribute<AuthorizeAttribute>() == null
                        && m.GetCustomAttribute<AllowAnonymousAttribute>() == null)
            .Select(m => m.Name)
            .ToList();

        Assert.AreEqual(0, unguarded.Count, $"Actions without authorization: {string.Join(", ", unguarded)}");
    }

    [TestMethod]
    [DataRow(nameof(PlayListController.Restore))]
    [DataRow(nameof(PlayListController.RestoreAll))]
    public void RestoreActionsRequireDbAdmin(string action)
    {
        var method = typeof(PlayListController).GetMethod(action);
        var authorize = method?.GetCustomAttribute<AuthorizeAttribute>();

        Assert.IsNotNull(authorize);
        Assert.AreEqual("dbAdmin", authorize.Roles);
    }
}
