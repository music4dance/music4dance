# DanzQ authorization client contract

This is the PR 2 contract from [issue #253](https://github.com/music4dance/music4dance/issues/253). It works only when `FeatureManagement:PublicApi` is enabled in Development or Staging. Production and `PROD_DB` remain blocked. There are no `/v1/*` data endpoints yet.

## Registration and endpoints

| Setting | Value |
| --- | --- |
| Client ID | `danzq-ios` |
| Client type | Public native app, no client secret |
| Exact callback | `com.domke.danzq:/oauth/callback` |
| Requested scopes | `account:read songs:read offline_access` |
| Metadata | `GET /.well-known/oauth-authorization-server` |
| Authorization | `GET /connect/authorize` |
| Code exchange and refresh | `POST /connect/token` |
| Token revocation | `POST /connect/revocation` |
| User-managed connections | `/Identity/Account/Manage/ConnectedApps` |

All endpoints require HTTPS. Obtain endpoint URLs from metadata on the chosen test host. Do not hardcode a production issuer or attempt to parse access or refresh tokens. The app does not request `openid` and receives no `id_token`.

## Sign in and consent

Open the authorization URL in the system browser (`ASWebAuthenticationSession` on iOS), with these query parameters:

| Parameter | Value |
| --- | --- |
| `client_id` | `danzq-ios` |
| `redirect_uri` | `com.domke.danzq:/oauth/callback` |
| `response_type` | `code` |
| `scope` | `account:read songs:read offline_access` |
| `code_challenge` | Base64url-encoded SHA-256 of a fresh, high-entropy PKCE verifier |
| `code_challenge_method` | `S256` |
| `state` | Fresh unpredictable value, retained and checked by the app |

The server requires `state`; only the app can verify that the returned value belongs to its pending request. Missing or mismatched state must not lead to a code exchange. Keep the PKCE verifier on the device until that exchange completes.

OpenIddict caches the validated authorization request for ten minutes. The browser then carries a short `request_uri` through the existing Identity login and optional two-factor flow, preserving the original callback, state, scopes and challenge. The user sees the registered app name and requested permissions before accepting or denying an antiforgery-protected form.

A valid existing grant covering the requested scopes skips repeat consent. Additional scopes require approval. Optional `prompt=consent` forces the consent screen; `prompt=none` returns `login_required` or `consent_required` if interaction is needed. Other prompt values, including `login` and `select_account`, are not supported in this slice.

Success redirects to the registered callback with `code` and the original `state`. Denial returns `error=access_denied` and `state`. Invalid protocol requests may return an HTTP error directly instead of redirecting. An untrusted client or callback is never used as an error redirect.

## Exchange and refresh

Both requests use `Content-Type: application/x-www-form-urlencoded`. Do not send credentials in the URL.

| Parameter | Code exchange | Refresh |
| --- | --- | --- |
| `client_id` | `danzq-ios` | `danzq-ios` |
| `grant_type` | `authorization_code` | `refresh_token` |
| `code` | Callback's code | Omit |
| `redirect_uri` | Exact registered callback | Omit |
| `code_verifier` | Original PKCE verifier | Omit |
| `refresh_token` | Omit | Current refresh token |
| `scope` | Omit | Usually omit; narrowing is allowed, expansion is not |

Successful responses contain `access_token`, `token_type=Bearer`, `expires_in`, and granted `scope`. A `refresh_token` is included when `offline_access` is granted.

| Credential | Lifetime and behavior |
| --- | --- |
| Authorization code | 60 seconds, single use, bound to client and PKCE verifier |
| Access token | One hour, opaque reference token, database entry and grant checked on use |
| Refresh token | 30 days from issue; each successful refresh issues a replacement valid for another 30 days |

There is no separate absolute session lifetime in this slice. Thirty days without a successful refresh requires another browser authorization. Locked, deleted or unconfirmed users and changed security stamps are rejected at token issuance, including refresh. Account eligibility on each data request remains PR 3 work; password changes alone do not immediately invalidate already-issued access tokens in PR 2.

Refresh rotation is strict, with no reuse grace period. Replaying a redeemed authorization code or refresh token revokes the associated grant and token chain. The next bearer request or refresh with that grant fails. The client must combine concurrent refresh requests into one operation and atomically save replacement credentials. If a refresh response is lost, do not blindly retry the old token; start a new browser authorization instead. A narrowed refresh cannot later restore omitted scopes.

Use access tokens only in `Authorization: Bearer <access_token>`. Site cookies, query-string tokens and form-body access tokens cannot authenticate the bearer scheme. Tokens carry the user identifier and protocol metadata, not username, email, roles or subscription state. The security stamp is retained privately in encrypted code/refresh payloads, not in access tokens.

## Revocation and errors

`POST /connect/revocation` takes `client_id`, `token`, and optionally `token_type_hint` (`access_token` or `refresh_token`) as form fields. OpenIddict revokes the submitted token. Revoking a refresh token alone does not promise immediate revocation of a separately issued access token. For app sign-out, revoke both current tokens and clear local storage. Unknown or already-revoked tokens return success; do not interpret that as proof of a live token.

Connected Apps revokes the selected permanent grant and every token associated with it. Only the signed-in owner can disconnect a grant, and the POST requires antiforgery validation. This does not cancel a subscription. Separate grants, such as an explicitly renewed consent, can appear as separate connections.

Typical errors are `invalid_grant` for expired, revoked, replayed or otherwise unusable credentials, `invalid_scope` for unsupported authorization scopes, and `invalid_client` for a client authentication failure. Refresh scope expansion is rejected by OpenIddict as `invalid_grant`. Return to browser authorization after `invalid_grant`; do not retry it in a loop.

All `/connect/*` requests share a fixed-window limit of 60 requests per IP per minute, per server process. Set `PublicApi:RequestsPerMinute` to a positive integer to change it. Excess requests return HTTP 429, a JSON OAuth error and `Retry-After`. This protects the protocol endpoints, not subscriber usage quotas. The existing Identity limiter still protects login. Proxy IP forwarding must be configured correctly; client-based usage limits belong to PR 3.

## Test and deployment boundaries

See [the HTTPS sandbox instructions](contributor-setup.md#testing-the-public-authorization-flow). Protocol tests cover actual OpenIddict handlers, EF storage, Identity cookies, rendered consent and Connected Apps forms. The protected resource in those tests exists only in the test assembly.

Consent, Connected Apps and login pages returning to `/connect/*` suppress page tracking, use `no-store` and `no-referrer`, and forbid framing. Their query strings are omitted from the site's 4xx tracker. With the feature enabled, OpenIddict and hosting request diagnostics are restricted to warning/error levels to avoid recording credentials in routine logs. Production proxy and telemetry redaction must also be checked before deployment.

The sandbox and test host disable EF bulk operations because their in-memory database cannot execute SQL bulk updates. SQL-backed hosts retain bulk operations. Sandbox state is lost on restart, and Development/Staging signing keys are temporary. Durable shared keys and scheduled cleanup of expired OpenIddict tokens/authorizations are required before production enablement. This PR does not enable production, add deployment secrets, change the schema or decide subscriber entitlement.
