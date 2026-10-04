using m4d.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using System.Reflection;

namespace m4d.Tests.Controllers;

/// <summary>
/// AdminController is [Authorize] at the class level, so a single [AllowAnonymous] on an action
/// silently opens that action to everyone. Every action should also name the role it needs, since
/// the class-level attribute alone admits any signed-in user.
/// </summary>
[TestClass]
public class AdminControllerAuthorizationTests
{
    private static IEnumerable<MethodInfo> Actions =>
        typeof(AdminController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() == null);

    [TestMethod]
    public void NoActionAllowsAnonymous()
    {
        var anonymous = Actions
            .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() != null)
            .Select(m => m.Name)
            .ToList();

        Assert.AreEqual(0, anonymous.Count, $"Anonymous admin actions: {string.Join(", ", anonymous)}");
    }

    [TestMethod]
    public void EveryActionRequiresARole()
    {
        var unroled = Actions
            .Where(m => m.GetCustomAttributes<AuthorizeAttribute>().All(a => string.IsNullOrEmpty(a.Roles)))
            .Select(m => m.Name)
            .Distinct()
            .ToList();

        Assert.AreEqual(0, unroled.Count, $"Admin actions without a role: {string.Join(", ", unroled)}");
    }

    /// <summary>
    /// Actions that flip site-wide state must not be reachable by a GET, or a link or image on
    /// another site could trigger them in a signed-in admin's browser.
    /// </summary>
    [TestMethod]
    [DataRow(nameof(AdminController.ToggleTestKeys))]
    [DataRow(nameof(AdminController.SetLogLevel))]
    [DataRow(nameof(AdminController.SetSearchIdx))]
    public void GlobalStateActionsArePostWithAntiforgery(string name)
    {
        var action = Actions.Single(m => m.Name == name);

        Assert.IsNotNull(action.GetCustomAttribute<HttpPostAttribute>(), $"{name} must be [HttpPost]");
        Assert.IsNull(action.GetCustomAttribute<HttpGetAttribute>(), $"{name} must not accept GET");
        Assert.IsNotNull(action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>(),
            $"{name} must validate the antiforgery token");
    }

    [TestMethod]
    public void ActivityLogRequiresARole()
    {
        var authorize = typeof(ActivityLogController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.IsFalse(string.IsNullOrEmpty(authorize?.Roles), "ActivityLogController must require a role");
    }
}
