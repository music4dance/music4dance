using m4d.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using System.Reflection;

namespace m4d.Tests.Controllers;

/// <summary>
/// The shared-id review merges and edits songs in bulk, so all of it is for dbAdmin only, and
/// every action that changes anything is a POST that validates the antiforgery token.
/// </summary>
[TestClass]
public class SharedIdsControllerAuthorizationTests
{
    private static IEnumerable<MethodInfo> Actions =>
        typeof(SharedIdsController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() == null);

    [TestMethod]
    public void TheControllerRequiresDbAdmin()
    {
        var authorize = typeof(SharedIdsController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.AreEqual("dbAdmin", authorize?.Roles);
    }

    [TestMethod]
    public void NoActionAllowsAnonymousOrWidensTheRole()
    {
        var open = Actions
            .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() != null ||
                m.GetCustomAttributes<AuthorizeAttribute>().Any())
            .Select(m => m.Name)
            .ToList();

        Assert.IsEmpty(open, $"Actions with their own authorization: {string.Join(", ", open)}");
    }

    [TestMethod]
    [DataRow(nameof(SharedIdsController.Apply))]
    [DataRow(nameof(SharedIdsController.Seed))]
    [DataRow(nameof(SharedIdsController.MergeMatching))]
    [DataRow(nameof(SharedIdsController.PreviewMergeMatching))]
    public void ChangesArePostWithAntiforgery(string name)
    {
        var action = Actions.Single(m => m.Name == name);

        Assert.IsNotNull(action.GetCustomAttribute<HttpPostAttribute>(), $"{name} must be [HttpPost]");
        Assert.IsNotNull(action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>(),
            $"{name} must validate the antiforgery token");
    }

    [TestMethod]
    public void OnlyTheReadsAcceptGet()
    {
        var gets = Actions
            .Where(m => m.GetCustomAttribute<HttpPostAttribute>() == null)
            .Select(m => m.Name)
            .Order()
            .ToList();

        CollectionAssert.AreEqual(new[] { nameof(SharedIdsController.Index), nameof(SharedIdsController.Preview) }, gets);
    }
}
