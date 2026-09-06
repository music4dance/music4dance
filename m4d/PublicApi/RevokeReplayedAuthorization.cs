using OpenIddict.Abstractions;
using OpenIddict.Server;

using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace m4d.PublicApi;

public sealed class RevokeReplayedAuthorization(
    IOpenIddictTokenManager tokens,
    IOpenIddictAuthorizationManager authorizations) : IOpenIddictServerHandler<ValidateTokenContext>
{
    public async ValueTask HandleAsync(ValidateTokenContext context)
    {
        if (context.Principal == null || string.IsNullOrEmpty(context.TokenId) ||
            string.IsNullOrEmpty(context.AuthorizationId) ||
            !(context.Principal.HasTokenType(TokenTypeIdentifiers.Private.AuthorizationCode) ||
              context.Principal.HasTokenType(TokenTypeIdentifiers.RefreshToken)))
        {
            return;
        }

        var token = await tokens.FindByIdAsync(context.TokenId);
        if (token == null || !await tokens.HasStatusAsync(token, Statuses.Redeemed)) return;

        var authorization = await authorizations.FindByIdAsync(context.AuthorizationId);
        if (authorization != null && !await authorizations.TryRevokeAsync(authorization))
        {
            context.Reject(Errors.InvalidToken);
        }
        // OpenIddict's next handler rejects the replay and revokes the related tokens.
    }
}
