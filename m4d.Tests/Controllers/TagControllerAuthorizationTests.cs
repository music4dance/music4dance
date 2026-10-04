using m4d.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using System.Reflection;

namespace m4d.Tests.Controllers;

/// <summary>
/// TagController has no class-level [Authorize]: Index is the public tag page and every other
/// action is part of the dbAdmin tag editor, so each one must say so itself.
/// </summary>
[TestClass]
public class TagControllerAuthorizationTests
{
    [TestMethod]
    public void EveryActionButIndexRequiresDbAdmin()
    {
        var open = typeof(TagController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() == null)
            .Where(m => m.Name != nameof(TagController.Index))
            .Where(m => !m.GetCustomAttributes<AuthorizeAttribute>().Any(a => a.Roles == "dbAdmin"))
            .Select(m => m.Name)
            .Distinct()
            .ToList();

        Assert.AreEqual(0, open.Count, $"Tag actions without dbAdmin: {string.Join(", ", open)}");
    }
}
