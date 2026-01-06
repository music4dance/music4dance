using m4d.Services.ServiceHealth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity.UI.Services;
using m4d.Services;
using Owl.reCAPTCHA;

namespace m4d.Configuration;

/// <summary>
/// Extension methods for configuring external services with resilience
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configure Azure Communication Services email sender with resilience
    /// </summary>
    public static IServiceCollection AddEmailSenderWithResilience(
        this IServiceCollection services,
        IConfiguration configuration,
        ServiceHealthManager serviceHealth)
    {
        try
        {
            var emailConnectionString = configuration["Authentication:AzureCommunicationServices:ConnectionString"];
            if (string.IsNullOrEmpty(emailConnectionString))
            {
                throw new InvalidOperationException("Azure Communication Services connection string not configured");
            }

            services.AddTransient<IEmailSender, EmailSender>(provider =>
                new EmailSender(emailConnectionString));
            serviceHealth.MarkHealthy("EmailService");
        }
        catch (Exception ex)
        {
            serviceHealth.MarkUnavailable("EmailService", $"{ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"WARNING: Email service not configured: {ex.Message}");
            // Register a null email sender as fallback
            services.AddTransient<IEmailSender, EmailSender>(provider => new EmailSender(null));
        }

        return services;
    }

    /// <summary>
    /// Configure reCAPTCHA with resilience
    /// </summary>
    public static IServiceCollection AddReCaptchaWithResilience(
        this IServiceCollection services,
        IConfiguration configuration,
        ServiceHealthManager serviceHealth)
    {
        try
        {
            var siteKey = configuration["Authentication:reCAPTCHA:SiteKey"];
            var siteSecret = configuration["Authentication:reCAPTCHA:SecretKey"];

            if (string.IsNullOrEmpty(siteKey) || string.IsNullOrEmpty(siteSecret))
            {
                throw new InvalidOperationException("reCAPTCHA SiteKey or SecretKey not configured");
            }

            services.AddreCAPTCHAV2(x =>
            {
                x.SiteKey = siteKey;
                x.SiteSecret = siteSecret;
            });
            serviceHealth.MarkHealthy("ReCaptcha");
        }
        catch (Exception ex)
        {
            serviceHealth.MarkUnavailable("ReCaptcha", $"{ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"WARNING: reCAPTCHA not configured: {ex.Message}");
        }

        return services;
    }
}
