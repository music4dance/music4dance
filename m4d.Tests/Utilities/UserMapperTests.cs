using Microsoft.AspNetCore.Identity;

using m4d.Utilities;
using m4dModels;

using Moq;

namespace m4d.Tests.Utilities;

[TestClass]
public class UserMapperTests
{
    [TestCleanup]
    public void Cleanup()
    {
        // GetUserNameDictionary populates UserMapper's static cache - reset it so tests
        // that exercise the real build path don't leak state into other tests.
        UserMapper.Clear();
    }

    private static UserManager<ApplicationUser> MockUserManager(params ApplicationUser[] users)
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625

        mockUserManager.Setup(m => m.Users).Returns(users.AsQueryable());
        mockUserManager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync([]);
        mockUserManager.Setup(m => m.GetLoginsAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync([]);

        return mockUserManager.Object;
    }

    [TestMethod]
    public async Task GetUserNameDictionary_DuplicateUserName_SkipsBadRowAndKeepsBuilding()
    {
        // Two users sharing a UserName (case-insensitively) would throw on
        // Dictionary.Add for the second one - that single bad row must not
        // prevent "carol", enumerated after it, from being cached.
        var alice = new ApplicationUser("alice", false) { Id = Guid.NewGuid().ToString() };
        var aliceDuplicate = new ApplicationUser("Alice", false) { Id = Guid.NewGuid().ToString() };
        var carol = new ApplicationUser("carol", false) { Id = Guid.NewGuid().ToString() };

        var userManager = MockUserManager(alice, aliceDuplicate, carol);

        var dict = await UserMapper.GetUserNameDictionary(userManager);

        Assert.IsTrue(dict.ContainsKey("alice"));
        Assert.IsTrue(dict.ContainsKey("carol"));
    }

    private static IReadOnlyDictionary<string, UserInfo> BuildDictionary(params ApplicationUser[] users)
    {
        var dict = new Dictionary<string, UserInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var user in users)
        {
            dict[user.UserName] = new UserInfo { User = user, Roles = [], Logins = [] };
        }
        return dict;
    }

    private static SongHistory BuildHistory(params string[] userValues)
    {
        return new SongHistory
        {
            Id = Guid.NewGuid(),
            Properties = userValues.Select(v => new SongPropertySparse
            {
                Name = Song.UserField,
                Value = v
            }).ToList()
        };
    }

    // AnonymizeAll is exercised by calling AnonymizeHistory with isAuthenticated: false.

    [TestMethod]
    public void AnonymizeHistory_Unauthenticated_PseudoUserPassesThroughUnchanged()
    {
        var pseudo = new ApplicationUser("batch-a", pseudo: true) { Id = Guid.NewGuid().ToString() };
        var dict = BuildDictionary(pseudo);
        var history = BuildHistory("batch-a|P");

        var result = UserMapper.AnonymizeHistory(history, dict, isAuthenticated: false);

        Assert.AreEqual("batch-a|P", result.Properties[0].Value);
    }

    [TestMethod]
    public void AnonymizeHistory_Unauthenticated_PseudoUserCaseDifferencePasses()
    {
        // UserName is stored with capital B but history has lowercase b — should still pass through.
        var pseudo = new ApplicationUser("Batch-A", pseudo: true) { Id = Guid.NewGuid().ToString() };
        var dict = BuildDictionary(pseudo);
        var history = BuildHistory("batch-a|P");

        var result = UserMapper.AnonymizeHistory(history, dict, isAuthenticated: false);

        Assert.AreEqual("batch-a|P", result.Properties[0].Value);
    }

    [TestMethod]
    public void AnonymizeHistory_Unauthenticated_PipeValueForNonPseudoUserIsUnavailable()
    {
        // A pipe-formatted value where the base name IS in the dictionary but is NOT pseudo
        // should not bypass anonymization.
        var realUser = new ApplicationUser("alice", pseudo: false) { Id = Guid.NewGuid().ToString() };
        var dict = BuildDictionary(realUser);
        var history = BuildHistory("alice|P");

        var result = UserMapper.AnonymizeHistory(history, dict, isAuthenticated: false);

        Assert.AreEqual("*UNAVAILABLE*", result.Properties[0].Value);
    }

    [TestMethod]
    public void AnonymizeHistory_Unauthenticated_UnknownPipeValueIsUnavailable()
    {
        // A pipe-formatted value whose base name is not in the dictionary at all.
        var dict = BuildDictionary(new ApplicationUser("other", pseudo: false) { Id = Guid.NewGuid().ToString() });
        var history = BuildHistory("unknown-bot|P");

        var result = UserMapper.AnonymizeHistory(history, dict, isAuthenticated: false);

        Assert.AreEqual("*UNAVAILABLE*", result.Properties[0].Value);
    }

    [TestMethod]
    public void AnonymizeHistory_Unauthenticated_RegularUserAnonymizedToId()
    {
        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser("bob", pseudo: false) { Id = userId };
        var dict = BuildDictionary(user);
        var history = BuildHistory("bob");

        var result = UserMapper.AnonymizeHistory(history, dict, isAuthenticated: false);

        Assert.AreEqual(userId, result.Properties[0].Value);
    }

    [TestMethod]
    public void AnonymizeHistory_Unauthenticated_PublicUserAlsoAnonymizedToId()
    {
        // Even a user with Privacy=255 (fully public) should be anonymized for unauthenticated visitors.
        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser("carol", pseudo: false) { Id = userId, Privacy = 255 };
        var dict = BuildDictionary(user);
        var history = BuildHistory("carol");

        var result = UserMapper.AnonymizeHistory(history, dict, isAuthenticated: false);

        Assert.AreEqual(userId, result.Properties[0].Value);
    }
}
