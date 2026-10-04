# Payments and Premium

**Type:** Reference
**Status:** Current
**Last verified:** 2026-10-03
**Code:** `m4d/Controllers/PaymentController.cs`, `m4d/Controllers/CommerceController.cs`,
`m4d/Controllers/HomeController.cs` (`Contribute`), `m4dModels/SubscriptionLevelDescription.cs`,
`m4dModels/ApplicationUser.cs` (`SubscriptionLevel`), `m4dModels/CheckoutSession.cs`, `m4d/APIControllers/RecomputeController.cs`
(`DoHandleSubscriptions`), `m4d/Controllers/ApplicationUsersController.cs`
(`UpdateSubscriptionRole`, `ClearPremium`), `m4d/Views/Home/Contribute.cshtml`,
`m4d/ClientApp/src/models/MenuContext.ts` (`isPremium`)

How people pay music4dance (one-time Stripe Checkout payments for a year of premium, or a
donation), how that turns into the `premium` role and a `SubscriptionLevel`, how it expires, and
where the server and client check for it. There are no recurring Stripe subscriptions: every
purchase is a single `payment`-mode Checkout session, and "subscription" means "one year of
premium from the purchase date".

## Data model

Subscription state lives on `ApplicationUser` (`m4dModels/ApplicationUser.cs`):

| Property | Meaning |
| --- | --- |
| `SubscriptionLevel` | `SubscriptionLevel` enum: `None = 0`, `Trial = 1`, `Basic = 2`, `Bronze = 3`, `Silver = 4`, `Gold = 5`. Ordered, so code compares with `<` / `>=`. |
| `SubscriptionStart` | First time the user subscribed. Set once (`??=`) and never moved by renewals. |
| `SubscriptionEnd` | When premium lapses. Drives expiry and the renewal banner. |
| `LifetimePurchased` | Running total of everything paid, subscription and donation (`decimal(18,2)`, set in `DanceMusicContext`). |
| `FailedCardAttempts` | Incremented on each cancelled or failed Checkout; used for fraud blocking. |

These fields round-trip through the user backup format (`DanceMusicService.SerializeUsers` /
`LoadUsers`, columns `SubscriptionLevel`, `SubscriptionStart`, `SubscriptionEnd`,
`LifetimePurchased`).

Two Identity roles carry the entitlement at request time (constants on `DanceMusicCoreService`):
`PremiumRole = "premium"` and `TrialRole = "trial"`. Most gates check the role; a few check
`SubscriptionLevel` directly (see [Server-side gates](#server-side-gates)).

Credited checkouts are recorded in the `CheckoutSessions` table (`m4dModels/CheckoutSession.cs`):
`SessionId` (the Stripe session id, primary key), `ApplicationUserId` (null for an anonymous
donation) and `Processed`. It exists only to stop a session being credited twice; see
[`Success`](#3-success).

### Price tiers

`SubscriptionLevelDescription.SubscriptionLevels` is the hard-coded price list, ordered highest
first:

| Level | Price (USD, one year) |
| --- | --- |
| Gold | 100.00 |
| Silver | 50.00 |
| Bronze | 25.00 |
| Basic | 15.00 |

`FindSubscriptionLevel(decimal amount)` returns the first (highest) tier whose price is at most
`amount`, so any amount maps to the best tier it covers, or `null` below $15.
`CommerceController.AnnualSubscription` is `SubscriptionLevels.Last().Price` (the Basic price, $15)
and is the threshold between "donation" and "purchase" throughout. `Trial` has no price; it's
only ever set by an admin.

## Payment flow (Stripe Checkout)

The integration uses the `Stripe.net` package (`Stripe` and `Stripe.Checkout` namespaces) with
server-created Checkout sessions. There's no Stripe.js and no publishable key on the client; the
browser just follows a redirect to Stripe's hosted page. There's also **no webhook**: the
subscription is granted when the browser comes back to the success URL.

`PaymentController` derives from `CommerceController`, which adds three helpers:
`IsCommerceEnabled()`, `GetCardFailLimit()` and `IsFraudDetected(user)`
(`FailedCardAttempts >= GetCardFailLimit()`).

### 1. Contribute page

`HomeController.Contribute` (`/Home/Contribute`, help page `subscriptions`, ads and warnings
hidden) renders `Views/Home/Contribute.cshtml` from a `ContributeModel` (`CommerceEnabled`,
`IsAuthenticated`, `CurrentPremium`, `PremiumExpiration`, `FraudDetected`, `RecaptchaFailed`).

- If `CommerceEnabled` is false, the page shows only a "we're working on it" message and the
  non-monetary ways to help.
- A signed-in user who isn't fraud-blocked gets one button per tier (partial
  `Views/Home/_paymentButton.cshtml`). Each is a form posting `kind=Purchase` and
  `amount=<tier price>` to `Payment/CreateCheckoutSession`. Current subscribers see "Renew your
  Subscription" and their `SubscriptionEnd`.
- A fraud-blocked user sees a message asking them to email support instead of the buttons.
- Everyone gets a **Donate** form (`kind=Donation`, free-form `amount`). Anonymous donors also
  get a reCAPTCHA widget when the `Captcha` feature flag is on.

### 2. `CreateCheckoutSession` (POST)

`PaymentController.CreateCheckoutSession(decimal amount, PurchaseKind kind, string recaptchaToken)`:

1. **Anonymous caller.** `kind == Purchase` is rejected (logged and thrown). A donation is
   allowed; if the `Captcha` flag is on, the token is verified with `IreCAPTCHASiteVerifyV2` and a
   failure redirects back to `Contribute` with `recaptchaFailed = true`.
2. **Signed-in caller.** If `IsFraudDetected(user)` or `!IsCommerceEnabled()`, it redirects back to
   `/Home/Contribute`. (The commerce switch is only checked for signed-in callers here; anonymous
   donors are stopped only by the page not rendering the form.)
3. **Line items.** If `kind == Purchase`, or a signed-in user's amount exceeds
   `AnnualSubscription`, the matching tier becomes a "`<Level>` Subscription" line item and its
   price is subtracted. Whatever is left becomes a "Donation" line item. So a signed-in $60
   donation is a Silver subscription plus a $10 donation.
4. Creates a `SessionCreateOptions` with `Mode = "payment"`, currency `usd`, `ClientReferenceId`
   set to the user's id (null for an anonymous donation), `CustomerEmail` set to the user's
   email, metadata `kind`, and success/cancel URLs built by `CreateStripeUrl` to
   `/payment/success` and `/payment/cancel` with `session_id={CHECKOUT_SESSION_ID}` (the braces are
   un-escaped so Stripe can substitute them). The host comes from the request only when
   `StripeReturnHost` recognizes it (music4dance.net or a subdomain, `localhost`, or the App
   Service's own `WEBSITE_HOSTNAME`); otherwise it's `www.music4dance.net`.
5. Responds `303` with `Location` set to the session URL.

### 3. `Success`

`PaymentController.Success(session_id)` retrieves the session with `SessionService.Get`.

It first checks that the visitor may complete it, with `CheckSessionAccess(ClientReferenceId,
user id)`. A session only ever credits the account that started it:

| Session started by | Visitor | Result |
| --- | --- | --- |
| A signed-in user | That user | Continues below. |
| A signed-in user | Signed out | `Challenge()`: sign in, then return to the success URL. |
| A signed-in user | A different user | `Forbid()` (403), with a warning logged. |
| Anonymous (a donation) | Signed out | Continues below. |
| Anonymous (a donation) | Any signed-in user | `Forbid()` (403), with a warning logged. |

Then, if `PaymentStatus == "paid"`:

- **Claims** the session with `TryClaimSession`, which inserts a `CheckoutSessions` row keyed by
  the Stripe session id. If the row already exists (or a concurrent request inserts it first and
  this insert fails on the primary key), the session is a duplicate: the success page re-renders
  but nothing below is credited or logged again. If crediting throws, `ReleaseSession` deletes the
  row so a reload can retry.
- **Classifies** the payment by amount, not metadata: `kind` is `Purchase` when
  `AmountTotal / 100 >= AnnualSubscription` and the user is signed in, otherwise `Donation`.
  (Reading `kind` back from the session metadata is commented out with a TODO.)
- **Purchase:** the new year starts at the later of now and the current `SubscriptionEnd`, so early
  renewals stack. It sets `SubscriptionStart` (first time only), `SubscriptionEnd = start + 1
  year`, `SubscriptionLevel` from `FindSubscriptionLevel(amount)`, adds to `LifetimePurchased`,
  calls `UserManager.AddToRoleAsync(user, PremiumRole)` and then
  `signInManager.RefreshSignInAsync` so the new role is in the cookie immediately.
- **Donation by a signed-in user:** adds to `LifetimePurchased` only.
- **Anonymous donation:** takes the email from `session.CustomerDetails`.
- If the `ActivityLogging` feature flag is on, it writes an `ActivityLog` row (`"Purchase"`, with
  the `PurchaseModel`) and saves.
- Renders `Views/Payment/Success.cshtml`, which thanks the user, names the tier, and splits out
  any extra donation.

Because the claim is in the database, a reload or Back on the success page never grants another
year, including after a restart or on another instance. If the session isn't `paid`, `Success`
renders the `Cancel` view.

### 4. `Cancel`

`PaymentController.Cancel(session_id)` increments the signed-in user's `FailedCardAttempts`,
writes a `"FailedPurchase"` `ActivityLog` row when `ActivityLogging` is on, and redirects to
`Contribute`. After `FailLimit` cancellations the user is treated as fraud-blocked (see
[Configuration](#configuration)). Only an admin can reset the counter, through the user edit page.

## Configuration

Key names only; values are secrets or environment-specific. See
[hosting-and-identity](../infrastructure/hosting-and-identity.md#identity-and-secrets) for where
configuration and secrets come from.

| Key / flag | Used by | Default when missing |
| --- | --- | --- |
| `Authentication:Stripe:SecretKey` | `PaymentController` constructor sets `StripeConfiguration.ApiKey` | none |
| `Authentication:StripeTest:SecretKey` | Same, when `GlobalState.UseTestKeys` is true | none |
| `Configuration:Commerce:Enabled` | `CommerceController.IsCommerceEnabled` | `true` (the sandbox sets `false`) |
| `Configuration:Commerce:FailLimit` | `CommerceController.GetCardFailLimit` | `5` |
| `Configuration:Marketing` section | `GlobalState.SetMarketing` / `GetMarketing`: promo banner and notices on Contribute and Success | disabled |
| Feature flag `Captcha` | reCAPTCHA on anonymous donations (keys and fail-open behavior: [bot-and-abuse-defense](../security/bot-and-abuse-defense.md)) | off |
| Feature flag `ActivityLogging` | `Purchase` / `FailedPurchase` activity-log rows | off |
| Feature flag `CustomerReminder` | "please contribute" banner for signed-in non-premium users | off |

`GlobalState.UseTestKeys` is set to `isDevelopment` at startup
(`M4dApplicationExtensions`) and can be flipped at runtime by `AdminController.ToggleTestKeys`
(`dbAdmin`, POST with antiforgery, from the Toggle button on `/Admin/InitializationTasks`).
Because the API key is a process-wide static assigned in the controller's constructor, the toggle
takes effect on the next payment request.

## Granting, changing and expiring premium

| Path | What it does |
| --- | --- |
| `PaymentController.Success` | Purchase: sets level/dates and adds `premium` (above). |
| `ApplicationUsersController.EditPost` (admin, `dbAdmin`) | Saves the edited user, then `UpdateSubscriptionRole(old, new)` reconciles roles: `None`→`Trial` adds `trial`; `None`→paid adds `premium`; `Trial`→paid adds `premium` and removes `trial`; `Trial`→`None` removes `trial`; paid→`None` removes `premium`; paid→`Trial` adds `trial` and removes `premium`. Dates are edited by hand. |
| `ApplicationUsersController.ChangeRoles` (admin) | Can add or remove `premium` / `trial` directly, without touching the level or dates. |
| `ApplicationUsersController.ClearPremium` (admin) | Resets level to `None`, clears both dates, removes `premium`. |
| `GET /api/recompute/subscription` | `RecomputeController.DoHandleSubscriptions`: for every user with `SubscriptionEnd < now`, removes `premium` if present. Token-authorized like the other recompute jobs (`TokenRequirement`, see [spotify-playlist-automation](../music-services/spotify-playlist-automation.md#entry-point)). |

Expiry only removes the `premium` role. It doesn't reset `SubscriptionLevel`, doesn't touch the
`trial` role, and nothing in this repo schedules the call: the Logic App inventory in
[spotify-playlist-automation](../music-services/spotify-playlist-automation.md) lists only the
`songstats` recompute, so whether `subscription` runs on a schedule isn't recorded here.

## Server-side gates

"Is premium" is computed several slightly different ways:

| Check | Definition | Used for |
| --- | --- | --- |
| `ContentController.IsPremium()` | `premium` or `trial` or `showDiagnostics` | Passed to `SongSearch` from `SongController` and `CustomSearchController`: the bonus-content (`Filter.Level`) gate, described in [song-search-service](../search/song-search-service.md#search--entry-point) |
| `ContentController.DefaultCruftFilter()` | Same three roles | `CruftFilter.AllCruft` instead of `NoCruft` on title/artist lookups |
| `SpotifyAuthService.IsPremium` | `premium` or `trial` | `ValidateSpotifyAccess` (used by `SpotifyPlaylistController` `api/spotify/playlist/user` and `/add`) and `SongController.CreateSpotify` |
| `SongController.ExportPlaylist` | `premium` or `trial` (inline `IsInRole`) | CSV export detail: `PlaylistExport` uses `ExportLevel.Personal` for premium, `ExportLevel.Sparse` otherwise; non-premium users can still export their own songs (`IsSelf`) |
| `Views/Shared/_head.cshtml` | `premium` only | Suppresses Google AdSense, the engagement system and the `CustomerReminder` banner |

Gates on `SubscriptionLevel` itself (these ignore `SubscriptionEnd`):

- **Spotify playlist viewer** (`SongController.Playlist`): `MatchLimitForSubscription` caps how
  many tracks are fetched and matched: Basic 25, Bronze 100, Silver 250, Gold 500, everyone else
  10. See [music-service-api-calls](../music-services/music-service-api-calls.md#playlist-lookup).
- **Create Spotify playlist** (`SpotifyCreateInfo.Validate`): more than 100 songs needs Bronze or
  higher (hard cap 1000). Below Silver, a page number other than 1 is ignored (`PageWarning`).
- **CSV export** (`ExportPlaylist` POST): row count is 100 below Silver and 1000 at Silver and up
  (5000 for a user's own songs).
- **Marketing banner** (`GlobalState.GetMarketing`): hidden for Bronze and up whose
  `SubscriptionEnd` is after 2024-11-30, and on `/contribute` and `/payment/` pages.

Spotify OAuth requirements for the playlist features are covered in
[music-service-api-calls](../music-services/music-service-api-calls.md#user-oauth-token-lifecycle-spotify).

## Client-side gates

The client sees roles, not levels. `_head.cshtml` serializes `UserMetadata` into the menu context:
`roles`, `expiration` (`SubscriptionEnd`), `started`, and `customerReminder`.

- `MenuContext.isPremium` is true for the `premium` or `trial` role. `daysToExpiration` is derived
  from `expiration`.
- `MainMenu.vue` shows a dismissible renewal alert (`expiration-alert`) when
  `daysToExpiration < 30`, including after expiry, linking to `/home/contribute`. Otherwise, if
  `customerReminder` is set and engagement prompts aren't active, it shows the "please contribute"
  alert (`premium-alert`). Dismissals are remembered in `sessionStorage`.
- `AddToPlaylistButton.vue` enables the add-to-Spotify-playlist feature only when
  `isAuthenticated && isPremium`; the server's `ValidateSpotifyAccess` is the real check, and
  `SpotifyRequirementsModal.vue` explains what's missing.
- The engagement bottom bar and offcanvas are skipped for premium users. Their prompts and copy
  are covered in [visitor-engagement-monetization](../pages/visitor-engagement-monetization.md).

Admin-only views of this data: the admin users table shows `LifetimePurchased`,
`SubscriptionLevel` and `FailedCardAttempts` (`AdminUsersModel`), and the account manage page
(`Areas/Identity/Pages/Account/Manage/Index`) shows the user their own level and dates.

## Future improvements

- No Stripe webhook. If the browser never returns to `/payment/success` (closed tab, network
  failure), the charge succeeds but no premium is granted. A `checkout.session.completed` webhook
  would fix this.
- `Success` classifies by amount rather than the `kind` metadata (TODO in code: "Not sure why LineItems don't come through").
- A signed-in user's donation updates `LifetimePurchased` but nothing calls `UpdateAsync` on that
  path; it appears to be saved only by the `ActivityLogging` `SaveChanges`.
- Expiry doesn't reset `SubscriptionLevel` or remove `trial`, so the level-based gates above
  keep working after `premium` is removed. The "is premium" checks also disagree (`_head.cshtml`
  ignores `trial`; `ContentController` adds `showDiagnostics`).
- `GlobalState.GetMarketing` hard-codes a 2024-11-30 cutoff.
- Tests cover the session-access rule (`PaymentControllerSessionAccessTests`), the claim
  (`PaymentControllerSessionClaimTests`) and the return host (`PaymentControllerReturnHostTests`),
  not the Stripe flow or the tier math.
- A checkout started before the purchaser check shipped has no `ClientReferenceId`, so a
  signed-in user finishing one gets a 403 and has to be credited by hand. Stripe sessions expire
  within 24 hours, so this only mattered around the 2026-10-02 deploy.

## History

- 2021-12-24, ADO PR 253 (`2c093417`): card validation and `FailedCardAttempts` fraud limit.
- 2022-01-09, ADO PR 258 (`b4fecd53`): moved to Stripe Checkout sessions in `PaymentController`.
- 2022-06-04, ADO PR 301 (`2ba27ad3`): `recompute/subscription` expiry task.
- 2023-12-01, ADO PRs 428-429 (`2e2c05c1`, `69739a83`): configurable marketing promos, hidden on
  purchase pages and for Bronze+ subscribers.
- 2025-01-29, ADO PR 535 (`3b78138c`): Spotify playlist export limits by subscription level.
- 2025-06-27, #10 (`9459cfdb`): global settings moved to feature flags (`Captcha`,
  `ActivityLogging`).
- 2025-07-01, #13 (`064b90e9`): `CustomerReminder` banner asking registered users to upgrade.
- 2025-09-20, #47 (`c78fc6ad`): Contribute page update.
- 2026-07-01, #204 (`749dd0db`): Spotify playlist viewer with tiered match limits.
- 2026-10-01: this doc created.
- 2026-10-02, #326 (`af5513b8`): checkout sessions record the purchaser (`ClientReferenceId`) and
  `Success` credits only that user (`CheckSessionAccess`); `ToggleTestKeys` requires `dbAdmin`.
- 2026-10-03: processed sessions recorded in the `CheckoutSessions` table instead of an in-memory
  set; Stripe return URLs only use recognized hosts; `ToggleTestKeys` is POST-only.

## Related

- [song-search-service](../search/song-search-service.md): bonus-content premium gate in search
- [visitor-engagement-monetization](../pages/visitor-engagement-monetization.md): upgrade prompts
- [account-management](account-management.md): Identity, roles and user admin
- [music-service-api-calls](../music-services/music-service-api-calls.md): Spotify playlist
  features that require premium
- [bot-and-abuse-defense](../security/bot-and-abuse-defense.md): CAPTCHA configuration
