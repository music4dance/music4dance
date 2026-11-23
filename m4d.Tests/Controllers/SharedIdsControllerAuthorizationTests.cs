using m4d.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using System.Reflection;

using ApiSharedIdsController = m4d.APIControllers.SharedIdsController;

namespace m4d.Tests.Controllers;

/// <summary>
/// The shared-id review merges and edits songs in bulk, so all of it is for dbAdmin only, and
/// every action that changes anything is a POST that validates the antiforgery token.
/// </summary>
[TestClass]
public class SharedIdsControllerAuthorizationTests
{
    private static IEnumerable<MethodInfo> Actions(Type controller) =>
        controller
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() == null);

    [TestMethod]
    [DataRow(typeof(SharedIdsController))]
    [DataRow(typeof(ApiSharedIdsController))]
    public void TheControllerRequiresDbAdmin(Type controller)
    {
        var authorize = controller.GetCustomAttribute<AuthorizeAttribute>();

        Assert.AreEqual("dbAdmin", authorize?.Roles);
    }

    [TestMethod]
    [DataRow(typeof(SharedIdsController))]
    [DataRow(typeof(ApiSharedIdsController))]
    public void NoActionAllowsAnonymousOrWidensTheRole(Type controller)
    {
        var open = Actions(controller)
            .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() != null ||
                m.GetCustomAttributes<AuthorizeAttribute>().Any())
            .Select(m => m.Name)
            .ToList();

        Assert.IsEmpty(open, $"Actions with their own authorization: {string.Join(", ", open)}");
    }

    [TestMethod]
    [DataRow(nameof(SharedIdsController.Seed))]
    [DataRow(nameof(SharedIdsController.MergeMatching))]
    [DataRow(nameof(SharedIdsController.MergeFeaturing))]
    [DataRow(nameof(SharedIdsController.MergeCredits))]
    [DataRow(nameof(SharedIdsController.MergeLeadArtist))]
    [DataRow(nameof(SharedIdsController.PreviewMergeMatching))]
    public void ChangesArePostWithAntiforgery(string name)
    {
        var action = Actions(typeof(SharedIdsController)).Single(m => m.Name == name);

        Assert.IsNotNull(action.GetCustomAttribute<HttpPostAttribute>(), $"{name} must be [HttpPost]");
        Assert.IsNotNull(action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>(),
            $"{name} must validate the antiforgery token");
    }

    [TestMethod]
    public void OnlyTheReadsAcceptGet()
    {
        var gets = Actions(typeof(SharedIdsController))
            .Where(m => m.GetCustomAttribute<HttpPostAttribute>() == null)
            .Select(m => m.Name)
            .Order()
            .ToList();

        CollectionAssert.AreEqual(new[] { nameof(SharedIdsController.Index), nameof(SharedIdsController.Preview) }, gets);
    }

    [TestMethod]
    public void TheApiValidatesAntiforgeryAndOnlyTakesPosts()
    {
        Assert.IsNotNull(typeof(ApiSharedIdsController).GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());

        var notPost = Actions(typeof(ApiSharedIdsController))
            .Where(m => m.GetCustomAttribute<HttpPostAttribute>() == null)
            .Select(m => m.Name)
            .ToList();

        Assert.IsEmpty(notPost, $"API actions that aren't POST: {string.Join(", ", notPost)}");
    }
}
