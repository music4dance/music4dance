using OpenIddict.Abstractions;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace m4d.PublicApi;

internal static class DanzQClient
{
    internal static OpenIddictApplicationDescriptor CreateDescriptor()
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = PublicApiDefaults.Clients.DanzQ,
            ApplicationType = ApplicationTypes.Native,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Explicit,
            DisplayName = PublicApiDefaults.Clients.DanzQDisplayName
        };

        descriptor.RedirectUris.Add(new Uri(PublicApiDefaults.Clients.DanzQRedirectUri));
        descriptor.Permissions.UnionWith(
        [
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.Revocation,
            Permissions.Endpoints.Token,
            Permissions.GrantTypes.AuthorizationCode,
            Permissions.GrantTypes.RefreshToken,
            Permissions.ResponseTypes.Code,
            Permissions.Prefixes.Scope + PublicApiDefaults.Scopes.AccountRead,
            Permissions.Prefixes.Scope + PublicApiDefaults.Scopes.SongsRead
        ]);
        descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);

        return descriptor;
    }
}
