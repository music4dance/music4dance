using m4d.Utilities;

using Microsoft.Extensions.Logging;

using Owl.reCAPTCHA;
using Owl.reCAPTCHA.v2;

namespace m4d.Services.ServiceHealth;

/// <summary>
/// Null implementation of IreCAPTCHASiteVerifyV2 for when reCAPTCHA isn't configured. Login,
/// Register, and PaymentController all constructor-inject this interface unconditionally, so
/// without a fallback registration here, every environment without reCAPTCHA keys - not just
/// the m4d.Sandbox host, but the plain "run with nothing configured" L0 path too (see
/// architecture/contributor-setup.md) - gets a DI activation failure (HTTP 500) on those pages
/// instead of the captcha step simply being skipped. Fails open (Success = true): captcha is a
/// bot-defense measure, not a security gate, and FeatureManagement:Captcha already controls
/// whether verification is attempted in the first place.
/// </summary>
public class NullReCaptchaSiteVerify : IreCAPTCHASiteVerifyV2
{
    private static readonly ILogger Logger = ApplicationLogging.CreateLogger<NullReCaptchaSiteVerify>();

    public Task<reCAPTCHASiteVerifyResponse> Verify(reCAPTCHASiteVerifyRequest request)
    {
        Logger.LogWarning(
            "reCAPTCHA service is unavailable - verification for this request was skipped (fail-open)");
        return Task.FromResult(new reCAPTCHASiteVerifyResponse { Success = true });
    }
}
