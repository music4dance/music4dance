using m4d.Utilities;

using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4d.Tests.Utilities;

[TestClass]
public class AdmAuthenticationTests
{
    private sealed class TestAuthentication() : SpotAuthentication(new ConfigurationBuilder().Build())
    {
        public void Expire() => OnTokenExpiredCallback(this);
    }

    [TestMethod]
    public void OnTokenExpired_BeforeTheRenewerIsStored_DoesNotThrow()
    {
        // CI 2026-11-24: a token with no time left fired its timer before `new Timer(...)` was
        // assigned to AccessTokenRenewer; the NullReferenceException on the timer thread
        // crashed the test host.
        using var auth = new TestAuthentication();

        auth.Expire();
    }
}
