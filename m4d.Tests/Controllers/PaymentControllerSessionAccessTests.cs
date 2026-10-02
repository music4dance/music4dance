using m4d.Controllers;

using static m4d.Controllers.PaymentController;

namespace m4d.Tests.Controllers;

/// <summary>
/// A checkout session may only be completed (and credited) by the user who started it.
/// </summary>
[TestClass]
public class PaymentControllerSessionAccessTests
{
    private const string Owner = "owner-id";
    private const string Other = "other-id";

    [TestMethod]
    public void OwnerIsAllowed() =>
        Assert.AreEqual(SessionAccess.Allowed, CheckSessionAccess(Owner, Owner));

    [TestMethod]
    public void OtherUserIsDenied() =>
        Assert.AreEqual(SessionAccess.Denied, CheckSessionAccess(Owner, Other));

    [TestMethod]
    public void UserIdComparisonIsCaseSensitive() =>
        Assert.AreEqual(SessionAccess.Denied, CheckSessionAccess("abc", "ABC"));

    [TestMethod]
    public void AnonymousVisitorMustSignInToCompleteAUsersSession() =>
        Assert.AreEqual(SessionAccess.SignInRequired, CheckSessionAccess(Owner, null));

    [TestMethod]
    public void AnonymousDonationIsAllowedAnonymously() =>
        Assert.AreEqual(SessionAccess.Allowed, CheckSessionAccess(null, null));

    [TestMethod]
    public void SignedInUserCannotClaimAnAnonymousSession()
    {
        Assert.AreEqual(SessionAccess.Denied, CheckSessionAccess(null, Other));
        Assert.AreEqual(SessionAccess.Denied, CheckSessionAccess("", Other));
    }
}
