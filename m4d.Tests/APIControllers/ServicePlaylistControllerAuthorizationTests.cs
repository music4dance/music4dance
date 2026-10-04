using m4d.APIControllers;

using Microsoft.AspNetCore.Authorization;

using System.Reflection;

namespace m4d.Tests.APIControllers;

/// <summary>
/// The Spotify explorer only shows "Add the playlist" to admins; the API must enforce it too.
/// </summary>
[TestClass]
public class ServicePlaylistControllerAuthorizationTests
{
    [TestMethod]
    public void PostRequiresDbAdmin()
    {
        var post = typeof(ServicePlaylistController).GetMethod(nameof(ServicePlaylistController.Post));

        var roles = post!.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Roles).ToList();

        CollectionAssert.Contains(roles, "dbAdmin");
    }
}
