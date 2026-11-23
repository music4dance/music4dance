using m4d.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using System.Reflection;

namespace m4d.Tests.Controllers;

/// <summary>
/// Unmerge rewrites songs and recreates deleted ones, so it must be an admin-only,
/// anti-forgery-checked POST. The split itself is covered by m4dModels.Tests/UnmergeTests.
/// </summary>
[TestClass]
public class SongControllerUnmergeTests
{
    private static MethodInfo Unmerge => typeof(SongController).GetMethod(nameof(SongController.Unmerge))!;

    [TestMethod]
    public void UnmergeRequiresDbAdmin()
    {
        var roles = Unmerge.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Roles).ToList();

        CollectionAssert.Contains(roles, "dbAdmin");
    }

    [TestMethod]
    public void UnmergeIsAnAntiForgeryCheckedPost()
    {
        Assert.IsNotNull(Unmerge.GetCustomAttribute<HttpPostAttribute>());
        Assert.IsNotNull(Unmerge.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }
}
