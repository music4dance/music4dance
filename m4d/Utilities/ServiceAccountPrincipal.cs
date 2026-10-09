using m4d.Services;

using System.Security.Claims;

namespace m4d.Utilities;

/// <summary>
/// Pass as the principal to MusicServiceManager to make a call as the stored music4dance service
/// account (see architecture/music-services/spotify-playlist-automation.md) rather than as the
/// signed-in user or with the app token. AdmAuthentication checks for this type before anything
/// else, and its identity is unauthenticated so nothing mistakes it for a site user.
/// </summary>
public sealed class ServiceAccountPrincipal(ServiceType service, IServiceAccountTokenStore store)
    : ClaimsPrincipal(new ClaimsIdentity())
{
    public ServiceType Service { get; } = service;
    public IServiceAccountTokenStore Store { get; } = store;
}
