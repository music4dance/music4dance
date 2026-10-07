using m4d.Services.ServiceHealth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity.UI.Services;
using m4d.Services;
using Owl.reCAPTCHA;
using Owl.reCAPTCHA.v2;

namespace m4d.Configuration;

/// <summary>
/// Extension methods for configuring external services with resilience. Credentials are read
/// from configuration each time a service is resolved, not captured at registration, so a
/// service whose credentials arrive late (App Configuration recovering after a failed startup
/// load) starts working without a restart. Health is tracked in SecretBackedServices.UpdateHealth.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configure Azure Communication Services email sender with resilience
    /// </summary>
    public static IServiceCollection AddEmailSenderWithResilience(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Fall back to a no-op sender while the connection string is missing - EmailSender's
        // constructor throws on a null connection string, which would turn "email not
        // configured" into a DI activation failure on every page that sends mail.
        services.AddTransient<IEmailSender>(_ =>
        {
            var connectionString = configuration[SecretBackedServices.EmailConnectionStringKey];
            return string.IsNullOrEmpty(connectionString)
                ? new NullEmailSender()
                : new EmailSender(connectionString);
        });

        return services;
    }

    /// <summary>
    /// Configure reCAPTCHA with resilience
    /// </summary>
    public static IServiceCollection AddReCaptchaWithResilience(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Owl.reCAPTCHA reads its options through IOptionsSnapshot, which runs this callback
        // per request, so the keys are picked up as soon as configuration has them.
        services.AddreCAPTCHAV2(x =>
        {
            x.SiteKey = configuration[SecretBackedServices.ReCaptchaSiteKeyKey];
            x.SiteSecret = configuration[SecretBackedServices.ReCaptchaSecretKeyKey];
        });

        // Replaces the verifier AddreCAPTCHAV2 registered. Login/Register/PaymentController
        // constructor-inject IreCAPTCHASiteVerifyV2 unconditionally, so while the keys are
        // missing they get a no-op verifier instead of one that fails every check.
        services.AddTransient<IreCAPTCHASiteVerifyV2>(sp =>
            SecretBackedServices.IsReCaptchaConfigured(configuration)
                ? ActivatorUtilities.CreateInstance<reCAPTCHASiteVerifyV2>(sp)
                : new NullReCaptchaSiteVerify());

        return services;
    }
}
