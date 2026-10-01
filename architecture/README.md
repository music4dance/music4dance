# music4dance Architecture Docs

The index for everything in `architecture/`. See [CONVENTIONS.md](CONVENTIONS.md) for doc types,
the header block, and the plan lifecycle.

> **Reorganization in progress (2026-10).** Docs have been moved into area folders. The next
> step consolidates finished plans and phase reports into current-state docs and pulls runbooks
> out into `runbooks/`. Until then, the **Kind** column below says what each doc really is, and
> the Runbooks table points at runbook sections still embedded in other docs.

Kind: **Ref** = current-state reference · **Plan** = proposal, not (fully) implemented ·
**Done plan** = completed plan or report, to be folded into a reference doc · **Guide** =
procedural, to become a runbook.

## Songs: data model, editing, voting

| Doc | Kind | What it covers |
| --- | --- | --- |
| [song-internal-format](songs/song-internal-format.md) | Ref | The append-only `SongProperty` log: wire format, field reference, edit blocks, user types, index compression |
| [song-details-viewing-editing](songs/song-details-viewing-editing.md) | Ref | Song details page, `SongCore.vue`, editing flow, permissions, pseudo-users, per-dance tempo |
| [song-merge-algorithm](songs/song-merge-algorithm.md) | Ref | Duplicate detection levels, title normalization, merge workflow and modes |
| [song-upload-format](songs/song-upload-format.md) | Ref | `UploadCatalog` file format: header fields, tags, upload form parameters |
| [add-augment-song](songs/add-augment-song.md) | Ref | `/song/augment`: locating a track, server-side lookup/dedup, saving user edits |
| [service-track-lookup](songs/service-track-lookup.md) | Ref | Backend resolution of a Spotify/Apple track ID to a catalog song |
| [drop-target-lookup](songs/drop-target-lookup.md) | Ref | `useDropTarget`: pasted service IDs/URLs in ordinary search boxes |
| [dance-family-voting](songs/dance-family-voting.md) | Ref + Plan | Voting with style families (International, American, …); large "Next Steps" section |
| [unconfirmed-dance-votes](songs/unconfirmed-dance-votes.md) | Ref | Excluding bulk-imported, unconfirmed dance ratings from default search |
| [tempo-validation-rules](songs/tempo-validation-rules.md) | Ref | Half/double-time correction of imported Spotify tempos (`tempo-bot`) |
| [waltz-correction-controls](songs/waltz-correction-controls.md) | Ref | `WaltzCorrectionCard`: fixing Waltz + `4/4` meter conflicts |
| [individual-artists](songs/individual-artists.md) | Ref | `Artists` property, the credit splitter, artist index, backfill and operations |
| [artist-pages](songs/artist-pages.md) | Ref | `/song/artist` page: server wiring, rendering, link generation |

## Search

| Doc | Kind | What it covers |
| --- | --- | --- |
| [song-filter](search/song-filter.md) | Ref | `SongFilter` wire format, sub-query classes, Advanced Search, mapping to Azure `SearchOptions` |
| [song-search-service](search/song-search-service.md) | Ref | `SongSearch`: premium gating, user queries, vote/edited-by post-filters, logging |
| [song-search-results](search/song-search-results.md) | Ref | `SongController` list-returning actions and the results pipeline |
| [saved-searches](search/saved-searches.md) | Ref | Search logging, "My Searches", anonymize/merge |
| [search-index-versioning](search/search-index-versioning.md) | Ref + Guide | Index schema versions; contains the **production migration runbook** |
| [index-backup-streaming](search/index-backup-streaming.md) | Done plan | Streaming index backup with key-set pagination |

## Music services and playlists

| Doc | Kind | What it covers |
| --- | --- | --- |
| [music-service-integration](music-services/music-service-integration.md) | Ref | Overview: service registry, per-service behavior, purchase IDs/filtering, client rendering |
| [music-service-model](music-services/music-service-model.md) | Ref | `MusicService` class hierarchy, `ServiceTrack`, `AlbumDetails`, property encoding |
| [music-service-api-calls](music-services/music-service-api-calls.md) | Ref | `MusicServiceManager` HTTP flows, Spotify OAuth tokens, enrichment, playlists |
| [amazon-music-renewal](music-services/amazon-music-renewal.md) | Done plan | Amazon options survey and the implemented search-link approach |
| [playlist-management](music-services/playlist-management.md) | Ref | `PlayList` model, admin UI, SongsFromSpotify / SpotifyFromSearch |
| [spotify-playlist-automation](music-services/spotify-playlist-automation.md) | Ref + Guide + Plan | What runs when and as whom; **SpotifyFromSearch maintenance runbook**; automation plan |

## Users and admin

| Doc | Kind | What it covers |
| --- | --- | --- |
| [account-management](users-admin/account-management.md) | Ref | Identity, username/password policy, privacy, deletion, user merge |
| [user-name-visibility](users-admin/user-name-visibility.md) | Ref | Who sees real names vs pseudonyms vs `UNAVAILABLE` |
| [admin-pages](users-admin/admin-pages.md) | Ref | Vue-rendered admin index pages and their paging strategies |
| [bulk-admin-modify](users-admin/bulk-admin-modify.md) | Ref | `BatchAdminEdit` / `BatchAdminModify`, `SongModifier` |
| [admin-search-bulk-modify](users-admin/admin-search-bulk-modify.md) | Ref | Admin Search by editor/date range and bulk modify of edit blocks |

## Pages and engagement

| Doc | Kind | What it covers |
| --- | --- | --- |
| [tempo-list-page](pages/tempo-list-page.md) | Ref | Dance Tempi page: client-side filtering, shareable URLs |
| [tempo-counter-page](pages/tempo-counter-page.md) | Ref | Tempo Counter page: tap tempo, matching, shareable URLs |
| [blog-help-sitemap](pages/blog-help-sitemap.md) | Ref + Guide | `blogmap.txt` and help links; how new posts get linked |
| [visitor-engagement-monetization](pages/visitor-engagement-monetization.md) | Ref | Progressive engagement prompts for anonymous visitors and subscribers |
| [gtm-tracking-guide](pages/gtm-tracking-guide.md) | Guide | Google Tag Manager / GA4 triggers for the engagement system |

## Security

| Doc | Kind | What it covers |
| --- | --- | --- |
| [bot-and-abuse-defense](security/bot-and-abuse-defense.md) | Ref | Catalog bot short-circuit (`SpiderManager`), identity rate limits + CAPTCHA escalation + lockout, crawler short-circuit on login pages, 4xx-by-URL tracking, `/Admin/Diagnostics` |

## Infrastructure and deployment

| Doc | Kind | What it covers |
| --- | --- | --- |
| [hosting-and-identity](infrastructure/hosting-and-identity.md) | Ref | Environments, deployment pipeline and modes, managed identity per service, App Configuration, startup sequence, health checks, cache-control headers |
| [service-resilience](infrastructure/service-resilience.md) | Ref | Graceful degradation and recovery when SQL, Search, App Configuration, OAuth, email or reCAPTCHA fail; health endpoints; status banner; admin failure emails; known issues |

## Observability

| Doc | Kind | What it covers |
| --- | --- | --- |
| [client-side-usage-logging](observability/client-side-usage-logging.md) | Ref | Client-side page-view tracking, feature flags, cache-control interplay |
| [usage-log-analysis-plan](observability/usage-log-analysis-plan.md) | Done plan | UsageLog analysis pages, bot detection, indexes |
| [application-log-persistence-plan](observability/application-log-persistence-plan.md) | Ref + Plan | Filesystem log persistence (done); heavier options (proposed) |
| [memory-diagnostics-plan](observability/memory-diagnostics-plan.md) | Ref + Plan | GC diagnostics (Phase 1 done); snapshots/advanced tooling (proposed) |
| [server-side-testing-analysis](observability/server-side-testing-analysis.md) | Done plan | Integration tests for `UsageLogApiController` |

## Development and testing

| Doc | Kind | What it covers |
| --- | --- | --- |
| [contributor-setup](dev-testing/contributor-setup.md) | Guide | Getting a running server: `m4d.Sandbox` or the real app on an empty DB |
| [contributor-test-environments](dev-testing/contributor-test-environments.md) | Ref + Plan | Options for running without production access; L0–L1 shipped, L2+ proposed |
| [testing-patterns](dev-testing/testing-patterns.md) | Ref | Server and client test patterns, infrastructure, pitfalls |
| [playwright-e2e-testing](dev-testing/playwright-e2e-testing.md) | Ref | Playwright e2e suite against `m4d.Sandbox` |
| [adding-a-new-dance](dev-testing/adding-a-new-dance.md) | Guide | Every step to add a new dance type |

## Runbooks

Rows that point outside `runbooks/` are procedures still embedded in other docs. Each will move
into `runbooks/` as its area is consolidated.

| Task | Where it is today |
| --- | --- |
| Roll out a breaking search-index schema change | [search-index-versioning § Production Migration Runbook](search/search-index-versioning.md#production-migration-runbook) |
| Maintain SpotifyFromSearch playlists manually | [spotify-playlist-automation § Runbook](music-services/spotify-playlist-automation.md#runbook-manual-spotifyfromsearch-maintenance) |
| Backfill / rebuild the artist index | [individual-artists § 11 Operations](songs/individual-artists.md#11-operations) |
| Triage the production 4xx export | [runbooks/triage-4xx](runbooks/triage-4xx.md) (automated by the `analyze-4xx` skill) |
| Respond to an attack or traffic spike on login/register | [runbooks/respond-to-attack](runbooks/respond-to-attack.md) |
| Provision a new App Service instance | [runbooks/provision-app-service](runbooks/provision-app-service.md) |
| Deploy to test or production | [runbooks/deploy](runbooks/deploy.md) |
| Configure service-failure email alerts | [runbooks/configure-failure-email](runbooks/configure-failure-email.md) |
| Refresh the cold-start dance fallback snapshot | [runbooks/refresh-dance-fallback-snapshot](runbooks/refresh-dance-fallback-snapshot.md) |
| Set up GTM / GA4 engagement tracking | [gtm-tracking-guide](pages/gtm-tracking-guide.md) |
| Link new blog posts / help articles | [blog-help-sitemap](pages/blog-help-sitemap.md) (+ `scripts/add-new-blog-posts.mjs`) |
| Add a new dance type | [adding-a-new-dance](dev-testing/adding-a-new-dance.md) |
| Run tempo validation over the existing catalog | [tempo-validation-rules § Running Against the Existing Catalog](songs/tempo-validation-rules.md#running-against-the-existing-catalog) |
| Set up a local contributor environment | [contributor-setup](dev-testing/contributor-setup.md) |

## Open plans

| Plan | Status | Where |
| --- | --- | --- |
| Public API & third-party authorization (DanzQ) | Foundation (PR 1) merged behind a disabled flag; PR 2 on hold after Apple denied the business plan, alternatives being explored | [plans/public-api-authorization](plans/public-api-authorization.md) |
| Azure Front Door caching | Not started; app-side prep shipped | [plans/front-door-caching](plans/front-door-caching.md) |
| Key Vault RBAC migration | Not started (vault `music4dance` still uses access policies, verified 2026-10-01) | [plans/key-vault-rbac-migration](plans/key-vault-rbac-migration.md) |
| Attack mitigation Phase 2+ (telemetry, alerts, WAF, honeypots) | Proposed | [plans/attack-mitigation-phase2](plans/attack-mitigation-phase2.md) |
| Contributor environments L2+ | Proposed | [contributor-test-environments](dev-testing/contributor-test-environments.md) |
| Automate browser-driven Spotify playlist jobs | Proposed | [spotify-playlist-automation § Plan](music-services/spotify-playlist-automation.md#plan-automating-category-2) |
| Application log persistence options 2–4 | Proposed | [application-log-persistence-plan](observability/application-log-persistence-plan.md) |
| Memory diagnostics Phases 2–4 | Proposed | [memory-diagnostics-plan](observability/memory-diagnostics-plan.md) |
| Dance family voting next steps | Proposed | [dance-family-voting § Next Steps](songs/dance-family-voting.md#next-steps) |

## Coverage gaps

Areas with real code but no architecture doc yet, in the order they're planned to be written:

1. **System overview**: projects, request flow, data stores, external services.
2. **Frontend architecture**: one Vite entry per page, `PageFrame`, `menuContext` / `window.*Json`
   handoff, the `Vue3()` helper, shared models and composables.
3. **Dance domain model**: DanceLib, `dances.json` / dance groups, tempo ranges, organizations,
   `DanceStats` and its hosted service.
4. **Tag system**: tag categories and groups, `TagController`, the tag index.
5. **Payments and premium**: `CommerceController`, `PaymentController`, subscription roles, gating.
6. **Configuration and feature flags reference**: every key and flag, with its source and default.
7. **CI/CD and release**: `azure-pipelines.yml`, GitHub workflows, environments.
8. **Background work and startup**: `BackgroundTaskQueue`, `StartupInitializationService`,
   `DatabaseRecoveryService`, recompute jobs.
9. **Data layer**: `DanceMusicContext`, EF migrations workflow, backup/restore.
10. **Content pages**: dance details, competition categories, style indexes, wedding, new music,
    Spotify explorer, custom searches.

When you notice another gap, add it here.
