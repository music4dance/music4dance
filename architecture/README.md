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
| [dance-family-voting](songs/dance-family-voting.md) | Ref | Voting with style families (International, American, …): auto-selection, family choice modal, vote/tag encoding |
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
| [search-index-versioning](search/search-index-versioning.md) | Ref | Index schema versions (`CodeVersion` / `ConfigVersion`, `SongIndexNext`), current index state, launch profiles, known cleanup debt |
| [index-backup-streaming](search/index-backup-streaming.md) | Ref | Unlimited full-index streaming (composite key-set pagination) for backups, clones, migrations and reloads |

## Music services and playlists

| Doc | Kind | What it covers |
| --- | --- | --- |
| [music-service-integration](music-services/music-service-integration.md) | Ref | Overview: service registry, per-service behavior (incl. Amazon search links + OneLink), purchase IDs/filtering, client rendering |
| [music-service-model](music-services/music-service-model.md) | Ref | `MusicService` class hierarchy, `ServiceTrack`, `AlbumDetails`, property encoding |
| [music-service-api-calls](music-services/music-service-api-calls.md) | Ref | `MusicServiceManager` HTTP flows, Spotify OAuth tokens, enrichment, playlists |
| [playlist-management](music-services/playlist-management.md) | Ref | `PlayList` model, admin UI, SongsFromSpotify / SpotifyFromSearch |
| [spotify-playlist-automation](music-services/spotify-playlist-automation.md) | Ref | What runs when and as whom (app token vs user token), Logic App jobs, `UpdateBatch`, known issues |

## Users and admin

| Doc | Kind | What it covers |
| --- | --- | --- |
| [account-management](users-admin/account-management.md) | Ref | Identity, username/password policy, privacy, deletion, user merge |
| [user-name-visibility](users-admin/user-name-visibility.md) | Ref | Who sees real names vs pseudonyms vs `UNAVAILABLE` |
| [admin-pages](users-admin/admin-pages.md) | Ref | Vue-rendered admin index pages and their paging strategies |
| [bulk-operations](users-admin/bulk-operations.md) | Ref | Bulk song changes: filter-driven `BatchAdminEdit` / `BatchAdminModify` + `SongModifier`, and Admin Search modify/refresh by editor and date range |

## Pages and engagement

| Doc | Kind | What it covers |
| --- | --- | --- |
| [tempo-list-page](pages/tempo-list-page.md) | Ref | Dance Tempi page: client-side filtering, shareable URLs |
| [tempo-counter-page](pages/tempo-counter-page.md) | Ref | Tempo Counter page: tap tempo, matching, shareable URLs |
| [content-pages](pages/content-pages.md) | Ref | `/dances/...` dispatcher (style index, dance details, competition categories, wedding), custom searches, New Music, Spotify Explorer |
| [blog-help-sitemap](pages/blog-help-sitemap.md) | Ref | `blogmap.txt` / `helpmap.txt` format, parsing (`SiteMapInfo`), where it's consumed, data history |
| [visitor-engagement-monetization](pages/visitor-engagement-monetization.md) | Ref | Progressive engagement prompts for anonymous visitors and subscribers |

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
| [usage-tracking](observability/usage-tracking.md) | Ref | Page-view `UsageLog`: server vs client-side recording, batch endpoint, admin analysis pages, the antiforgery 400 root cause |
| [logging-and-diagnostics](observability/logging-and-diagnostics.md) | Ref | Where app logs go (filesystem Warning+), orphaned App Insights, GC snapshots, forced GC, memory dumps |

## Development and testing

| Doc | Kind | What it covers |
| --- | --- | --- |
| [contributor-setup](dev-testing/contributor-setup.md) | Guide | Getting a running server: `m4d.Sandbox` or the real app on an empty DB |
| [contributor-test-environments](dev-testing/contributor-test-environments.md) | Ref | Design of the no-production-access environments: L0 (empty DB) and L1 `m4d.Sandbox` (stubs, seeded users, `SongIndexLocal`) |
| [testing-patterns](dev-testing/testing-patterns.md) | Ref | Server and client test patterns, infrastructure, pitfalls |
| [playwright-e2e-testing](dev-testing/playwright-e2e-testing.md) | Ref | Playwright e2e suite against `m4d.Sandbox` |

## Runbooks

Rows that point outside `runbooks/` are procedures still embedded in other docs. Each will move
into `runbooks/` as its area is consolidated.

| Task | Where it is today |
| --- | --- |
| Roll out a breaking search-index schema change | [runbooks/search-index-breaking-migration](runbooks/search-index-breaking-migration.md) |
| Maintain SpotifyFromSearch playlists (new dances, seasons, refresh) | [runbooks/spotify-from-search-maintenance](runbooks/spotify-from-search-maintenance.md) |
| Roll out / re-run / roll back the artist index | [runbooks/artist-index-operations](runbooks/artist-index-operations.md) |
| Triage the production 4xx export | [runbooks/triage-4xx](runbooks/triage-4xx.md) (automated by the `analyze-4xx` skill) |
| Respond to an attack or traffic spike on login/register | [runbooks/respond-to-attack](runbooks/respond-to-attack.md) |
| Set up GTM / GA4 engagement tracking | [runbooks/gtm-ga4-setup](runbooks/gtm-ga4-setup.md) |
| Link new blog posts / help articles | [runbooks/link-new-blog-posts](runbooks/link-new-blog-posts.md) |
| Provision a new App Service instance | [runbooks/provision-app-service](runbooks/provision-app-service.md) |
| Deploy to test or production | [runbooks/deploy](runbooks/deploy.md) |
| Configure service-failure email alerts | [runbooks/configure-failure-email](runbooks/configure-failure-email.md) |
| Refresh the cold-start dance fallback snapshot | [runbooks/refresh-dance-fallback-snapshot](runbooks/refresh-dance-fallback-snapshot.md) |
| Read production logs | [runbooks/read-production-logs](runbooks/read-production-logs.md) |
| Capture memory diagnostics / dumps | [runbooks/capture-memory-diagnostics](runbooks/capture-memory-diagnostics.md) |
| Analyze usage logs | [runbooks/analyze-usage-logs](runbooks/analyze-usage-logs.md) |
| Add a new dance type | [runbooks/add-a-dance](runbooks/add-a-dance.md) |
| Run tempo validation over the existing catalog | [runbooks/validate-catalog-tempo](runbooks/validate-catalog-tempo.md) |
| Set up a local contributor environment | [contributor-setup](dev-testing/contributor-setup.md) |

## Open plans

| Plan | Status | Where |
| --- | --- | --- |
| Public API & third-party authorization (DanzQ) | Foundation (PR 1) merged behind a disabled flag; PR 2 on hold after Apple denied the business plan, alternatives being explored | [plans/public-api-authorization](plans/public-api-authorization.md) |
| Azure Front Door caching | Not started; app-side prep shipped | [plans/front-door-caching](plans/front-door-caching.md) |
| Key Vault RBAC migration | Not started (vault `music4dance` still uses access policies, verified 2026-10-01) | [plans/key-vault-rbac-migration](plans/key-vault-rbac-migration.md) |
| Attack mitigation Phase 2+ (telemetry, alerts, WAF, honeypots) | Proposed | [plans/attack-mitigation-phase2](plans/attack-mitigation-phase2.md) |
| Contributor environments beyond the sandbox (test deploys, diagnostics, samplified data, own Azure) | Proposed | [plans/contributor-environments-next](plans/contributor-environments-next.md) |
| Spotify service-account automation (write playlists without a browser session) | Proposed; `UpdateBatch` step shipped (#299) | [plans/spotify-service-account-automation](plans/spotify-service-account-automation.md) |
| Durable log storage (Blob / Log Analytics / tuned App Insights) | Proposed | [plans/log-persistence-options](plans/log-persistence-options.md) |
| Memory diagnostics: history, allocation tracking, pressure health check | Proposed | [plans/memory-diagnostics-next](plans/memory-diagnostics-next.md) |
| Visitor engagement next steps (post-launch priorities, enhancements) | Proposed | [plans/visitor-engagement-next](plans/visitor-engagement-next.md) |
| Dance family voting next steps | Proposed | [plans/dance-family-voting-next](plans/dance-family-voting-next.md) |

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

When you notice another gap, add it here.
