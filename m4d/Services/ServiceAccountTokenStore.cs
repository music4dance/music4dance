using m4d.Utilities;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace m4d.Services;

/// <summary>
/// The stored service-account connections (see <see cref="ServiceAccountToken"/>), which let
/// scheduled jobs write as the music4dance account on a music service. Keyed by ServiceType so
/// Apple Music can reuse it.
/// </summary>
public interface IServiceAccountTokenStore
{
    // The connection's bookkeeping, or null if it was never connected. Doesn't decrypt the token.
    Task<ServiceAccountToken> Get(ServiceType service);

    // The refresh token, or null when not connected or marked invalid
    Task<string> GetRefreshToken(ServiceType service);

    Task Connect(ServiceType service, string refreshToken, string accountId, string accountName,
        string scopes, string authorizedBy);

    // The service returned a new refresh token on refresh. Doesn't restart the expiry clock.
    Task UpdateRefreshToken(ServiceType service, string refreshToken);

    Task MarkInvalid(ServiceType service, string reason);

    Task MarkAlertSent(ServiceType service);
}

/// <summary>
/// A database row per service, with the refresh token protected by ASP.NET Data Protection.
/// A singleton, so each call opens its own scope for the (scoped) DbContext.
/// </summary>
public class ServiceAccountTokenStore(
    IServiceScopeFactory scopeFactory, IDataProtectionProvider dataProtection,
    ILogger<ServiceAccountTokenStore> logger) : IServiceAccountTokenStore
{
    private readonly IDataProtector _protector =
        dataProtection.CreateProtector("m4d.ServiceAccountToken.v1");

    public async Task<ServiceAccountToken> Get(ServiceType service)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DanceMusicContext>();
        return await context.ServiceAccountTokens.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Service == service.ToString());
    }

    public async Task<string> GetRefreshToken(ServiceType service)
    {
        var token = await Get(service);
        if (token == null || !token.IsValid)
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(token.ProtectedRefreshToken);
        }
        catch (System.Security.Cryptography.CryptographicException e)
        {
            // The Data Protection key ring changed (e.g. keys lost on a redeploy), so the stored
            // token can't be read - the account has to be reconnected
            logger.LogError(e, "Unable to unprotect the {Service} service account token", service);
            await MarkInvalid(service, "The stored token can't be decrypted; reconnect the account");
            return null;
        }
    }

    public async Task Connect(ServiceType service, string refreshToken, string accountId,
        string accountName, string scopes, string authorizedBy)
    {
        await Update(service, true, token =>
        {
            token.ProtectedRefreshToken = _protector.Protect(refreshToken);
            token.AccountId = accountId;
            token.AccountName = accountName;
            token.Scopes = scopes;
            token.AuthorizedAt = DateTimeOffset.UtcNow;
            token.AuthorizedBy = authorizedBy;
            token.InvalidatedAt = null;
            token.InvalidReason = null;
            token.LastAlertSent = null;
        });

        // Drop any cached auth built from the previous connection
        AdmAuthentication.ClearServiceAccount(service);
        logger.LogInformation(
            "Connected the {Service} service account {AccountId} ({AccountName})", service, accountId, accountName);
    }

    public Task UpdateRefreshToken(ServiceType service, string refreshToken) =>
        Update(service, false, token => token.ProtectedRefreshToken = _protector.Protect(refreshToken));

    public async Task MarkInvalid(ServiceType service, string reason)
    {
        logger.LogWarning("The {Service} service account token was marked invalid: {Reason}", service, reason);
        await Update(service, false, token =>
        {
            // Keep the first failure's time and reason; later calls just see the invalid token
            if (token.InvalidatedAt != null)
            {
                return;
            }

            token.InvalidatedAt = DateTimeOffset.UtcNow;
            token.InvalidReason = reason?[..Math.Min(reason.Length, 1024)];
        });
    }

    public Task MarkAlertSent(ServiceType service) =>
        Update(service, false, token => token.LastAlertSent = DateTimeOffset.UtcNow);

    private async Task Update(ServiceType service, bool create, Action<ServiceAccountToken> update)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DanceMusicContext>();
        var key = service.ToString();
        var token = await context.ServiceAccountTokens.FindAsync(key);
        if (token == null)
        {
            if (!create)
            {
                return;
            }

            token = new ServiceAccountToken { Service = key };
            _ = context.ServiceAccountTokens.Add(token);
        }

        update(token);
        _ = await context.SaveChangesAsync();
    }
}
