# Shared-Id Review: Client App, Own Controller, and Tests

**Type:** Plan
**Status:** Proposed
**Last verified:** 2026-11-14
**Code:** `m4d/Controllers/AdminController.cs` (`SharedIds` actions), `m4d/Views/Admin/SharedIds.cshtml`,
`m4d/ViewModels/SharedIdsModel.cs`, `m4d/ViewModels/SharedIdsPreviewModel.cs`, `m4dModels/SharedIdLog.cs`,
`m4dModels.Tests/SharedIdLogTests.cs`

How the [shared-id review](../songs/shared-id-review.md) page should be restructured now that its
behavior has settled: a Vue page app for the review page, a controller of its own, the logic that
sits in the controller and view moved somewhere testable, and the tests that are missing. Nothing
here changes what the review does. It changes where the code lives and what can be tested.

## Recommendations at a glance

| Question | Recommendation | Why |
| --- | --- | --- |
| Vue app for the review page? | **Yes.** One page app, `shared-id-review`, with the page's data in `model_` and **Apply** sent as JSON to an API endpoint. Keep the merge-all preview page in Razor. | The page holds four kinds of per-cluster state that interact (action, groups, name picks, removals). Today that state lives in form-field names and 20 lines of inline script, none of it testable. |
| Own controller? | **Yes.** `SharedIdsController` (MVC: page, preview, background tasks) and `APIControllers/SharedIdsController` (JSON: apply), both `dbAdmin` at class level. | 260 lines and a static field come out of a 2,731-line controller, and the partner API's review queues ([partner-api-standalone-host](partner-api-standalone-host.md) PR 5) will need the same home. |
| Server refactoring | Move page loading out of the action into `SharedIdLog`; move the preview's static state into a DI singleton; key name picks and removals by song id, not column index; pull the pure parts (match tiers, clustering) out of `SharedIdLog.cs`. | Each one turns untested controller or view logic into something a unit test can reach. |
| Tests | Domain coverage is good. The gaps are the web layer (zero tests), the client (none possible today), a handful of domain edge cases, and an end-to-end run. | See [Testing](#4-testing). |

Suggested order: server refactor and tests first with no visible change (PR 1), then the Vue app
(PR 2), then the optional group-aware preview (PR 3) and the end-to-end test (PR 4).

## Where it stands

- **The view carries the state.** [SharedIds.cshtml](../../m4d/Views/Admin/SharedIds.cshtml) is
  458 lines with six local Razor functions. A cluster's choice is spread over parallel form arrays
  (`items[i].SongIds`, `items[i].Groups`, `items[i].CandidateIds`), radio values that are column
  indexes with `-1` for the **If merged** column (`SharedIdNamePicks.Proposed`), and removal boxes
  whose value is a `"song|album|id"` string. The view also works out the **Cleans up feat.**
  badge and each cluster's default action.
- **The client behavior is inline script.** Two handlers at the end of the view: one shows the
  split note when a cluster's groups differ, one strikes out what's checked for removal (parsing
  the `|` format again to tell an album from an id).
- **The interactions aren't reflected.** **If merged** always shows every song merged, whatever
  the groups and removals. A name radio on a song outside a group is accepted and silently becomes
  the proposal for that group. These are the places where a reviewer can be surprised by Apply,
  and plain script makes them expensive to fix.
- **The controller holds logic.** `AdminController.SharedIds`
  ([AdminController.cs:187](../../m4d/Controllers/AdminController.cs#L187)) re-clusters up to three
  times while songs turn out to be stale, sorts by the preview's ranks, pages, previews each
  cluster (catching failures), builds the feat. proposals and counts the tiers.
  `ApplySharedIds` composes the result message. The last preview is a `static` field on the
  controller ([:377](../../m4d/Controllers/AdminController.cs#L377)), written from a background
  task, and `SharedIdsPreviewModel` (a view model) carries the rank-inheritance logic that keeps a
  cluster's place after a partial merge.
- **Tests.** `SharedIdLogTests` (31 tests) and `SharedIdGuardTests` (4) cover the domain well.
  Nothing covers the controller actions, `SharedIdChoiceForm.ToChoice`, `DefaultAction`,
  `SharedIdsPreviewModel.RankOf`/`Merged`, the view, or the flow end to end.

## 1. The review page as a Vue app

### Options

| Option | What it is | For | Against |
| --- | --- | --- | --- |
| **A. Stay Razor, tidy up** | Split the view into partials, precompute badges and defaults in the view model, test `ToChoice`, add an E2E test. | Cheapest. No new contract. | Client behavior stays untestable except end to end. Every future interaction (group-aware preview, constraining name picks) is more ad-hoc script against form-field names. |
| **B1. Vue app, form post** | Vue renders the page and builds the same hidden form for `ApplySharedIds`. | Server unchanged. | Keeps the parallel-array, column-index, `"song\|album\|id"` protocol, which is the fragile part. |
| **B2. Vue app, JSON apply** (recommended) | Vue renders the page from `model_` (like `admin-users` and `activity-log`) and posts a typed request with `axiosXsrf`. The server applies it and returns the result; the client reloads the page. | The protocol becomes a typed DTO on both sides. The state is a plain TypeScript class, testable without mounting anything. Matches how the song pages already edit through `/api/song`. | A DTO pair to maintain, and a TypeScript mirror of it. |
| **C. Vue islands in Razor** | Mount a small component per cluster inside the Razor page. | Incremental. | Not a pattern this codebase uses; two rendering models on one page. |

**Recommendation: B2.** The server stays authoritative. The client never clusters, merges or
decides staleness; it collects choices, and `SharedIdLog.Apply` re-checks everything as it does
now.

### Design

**Page model** (`SharedIdReviewPageModel`, built by `SharedIdLog.LoadPage`, see
[§3](#3-server-refactoring)). Everything the view computes today moves server-side, so the
client only renders and collects:

- Page fields: `pendingCandidates`, `clusterCount`, `page`, `pageCount`, `sortedBy`,
  `withoutFeaturingCount`, `closeCount`, `message`.
- Per cluster: `key` (its sorted song ids, for `:key`), `serviceIds`, `match`,
  `hasRejectedPair`, `hasNoMergeSong`, `cleansFeaturing`, `defaultAction`, `candidates` (id,
  source, created, ids, detail), `songs`, `merged` (or null), `proposal` (title, artist,
  artists, or null).
- Per song (and for `merged`): `songId`, `title`, `artist`, `artists` (effective), `length`,
  `tempo`, `dances` (id and weight), `created`, `albums` (index, name, track, and `ids`: each the
  extended id and its track link). Links stay server-side (`MusicService.GetPurchaseLink` needs the
  album's iTunes id), so there's one implementation.

A flat projection rather than `SongHistory` + the client `Song` class: the page needs
`EffectiveArtists`, `GetExtendedPurchaseIds` spellings and `AlbumDetails.Index` exactly as the
server uses them for removals, and duplicating those rules in TypeScript is how the two sides
drift.

**Apply request** (`POST /api/sharedids/apply`):

```json
{
  "page": 3,
  "choices": [
    {
      "action": "Merge",
      "candidateIds": [101, 102],
      "songs": [{ "songId": "…", "group": "A" }, { "songId": "…", "group": "" }],
      "titleFrom": "…songId… or null for the proposal",
      "artistFrom": null,
      "artistsFrom": null,
      "removals": [{ "songId": "…", "album": 0, "id": "I:111" }, { "songId": "…", "album": 1 }]
    }
  ]
}
```

The response is the `SharedIdBatchResult` counts plus the summary message
(`SharedIdBatchResult.Summary()`, moved out of the controller). The client then navigates to
`/SharedIds?page=n&message=…` as the redirect does today. In-place refresh isn't worth it: an
Apply re-clusters the queue, so the whole page changes anyway.

**Client structure** (`m4d/ClientApp/src/pages/shared-id-review/`):

| File | Role |
| --- | --- |
| `SharedIdReviewModel.ts` | TypedJSON classes for the page model. |
| `ClusterChoice.ts` | The state of one cluster's choice, with no Vue in it: built from a cluster (default action, every song in A, names on the proposal, nothing removed). Methods to set a group, pick a name, toggle a removal. Derived: `groups`, `isSplit`, `isDirty`, whether a name pick applies to a song's group, `toRequest()`. |
| `App.vue` | `PageFrame`, counts, the background-task buttons (form posts with `context.xsrfToken`, as `AdminFooter` does), pagination, the cluster list, Apply. Warns before leaving with unapplied changes. |
| `ClusterCard.vue`, `SongColumn.vue`, `AlbumIds.vue`, `NameChoiceRow.vue` | The card, a song column with its group select and albums, an album with its removal boxes, a title/artist/artists radio row. |

Clusters are keyed by `cluster.key`, not index, and every value derived from a prop is a
`computed` (CLAUDE.md, MPA reactivity). **Merge and edit** stays a form post to `/Song/BulkEdit` in
a new tab.

**Behavior worth adding once the state is in one place** (small, and each is a unit test on
`ClusterChoice`):

- A name radio on a song outside every group is disabled; when groups split, each radio says
  which group it applies to.
- Checking an album disables its id boxes (today they're struck through but still postable).
- Apply shows how many clusters differ from their defaults.

**Optional, PR 3: a group-aware preview.** `POST /api/sharedids/preview` with one cluster's
groups and removals returns the merged song per group (`BuildSimpleMerge` on each group, after
applying the removals to in-memory copies with `AppendIdRemoval` and `Song.Create`, saving
nothing). The **If merged** column then shows what Apply will actually do. This is the one change
that needs the Vue app to be practical; it's worth doing only if the split and removal cases keep
coming up in review.

**Stays Razor:** `SharedIdsPreview` (the last merge-all preview). It's read-only, has no state,
and is fine as it is.

## 2. A controller of its own

### Options

| Option | For | Against |
| --- | --- | --- |
| **A. Leave it in `AdminController`** | No URL change. | The controller is 2,731 lines of unrelated tools. The preview's static field and the rank bookkeeping stay buried in it. |
| **B. One MVC `SharedIdsController`** | Everything in one place, including a JSON action. | Mixes MVC and API conventions in one class; the codebase keeps them apart. |
| **C. MVC `SharedIdsController` + `APIControllers/SharedIdsController`** (recommended) | Follows the `SongController` / `APIControllers/SongController` split. The API side is where the partner API's unmatched-track review can join it. | Two small classes. |

### Routes

| Now | After |
| --- | --- |
| `GET /Admin/SharedIds` | `GET /SharedIds` (Vue page via `Vue3(...)`) |
| `POST /Admin/ApplySharedIds` | `POST /api/sharedids/apply` (JSON, antiforgery header) |
| — | `POST /api/sharedids/preview` (PR 3, optional) |
| `POST /Admin/SeedSharedIds` | `POST /SharedIds/Seed` |
| `POST /Admin/MergeMatchingSharedIds` | `POST /SharedIds/MergeMatching` |
| `POST /Admin/PreviewMergeMatchingSharedIds` | `POST /SharedIds/PreviewMergeMatching` |
| `GET /Admin/SharedIdsPreview` | `GET /SharedIds/Preview` |

Both controllers are `[Authorize(Roles = "dbAdmin")]` at class level, so no action can be added
without the role. The background tasks still redirect to `/Admin/AdminStatus`, which stays where
it is. The URLs are admin-only, so no redirects from the old ones; update the admin index link,
the task completion message that names `/Admin/SharedIdsPreview`, and the reference doc.

## 3. Server refactoring

In rough order of value:

1. **Page loading moves into the domain.** `SharedIdLog.LoadPage(user, page, ranks)` returns the
   page: the stale-retry loop, the sort by rank, paging, the per-cluster preview (a failure
   becomes a null preview and a logged warning, as now), proposals, `cleansFeaturing`, the
   default action and the tier counts. `PlanMergeMatching` has its own copy of the stale-retry
   loop; share one helper. The controller action becomes a few lines, and the whole page becomes
   testable with `TestSongIndex`.
2. **The preview's state becomes a service.** A singleton `SharedIdPreviewStore` holds the last
   plan list and the per-song ranks (today's `SharedIdsPreviewModel.RankOf` and `Merged`). The
   background task writes it and the page reads it through DI instead of a `static` field, and
   the rank inheritance gets unit tests of its own. `SharedIdsPreviewModel` shrinks to what the
   preview view needs.
3. **Song ids, not column indexes.** `SharedIdNamePicks` becomes `Guid?` per field (null for the
   proposal) and `SharedIdRemoval` takes a `SongId`. The `Proposed = -1` sentinel,
   `SharedIdRemoval.Parse`/`Format` and the bounds checks in `ApplyRemovals` go away, and so does
   the coupling between a choice and the order its songs were rendered in. `SharedIdChoiceForm`
   is replaced by the request DTO, whose mapping to `SharedIdChoice` is one tested method.
4. **Split the pure parts out of `SharedIdLog.cs`** (859 lines). Two pieces have no database in
   them: the match tiers (`Match`, `TitleArtistMatch`, `CloseMatch`) as `SharedIdMatcher`, and the
   union-find in `PendingClusters` as a function from rows to clusters. Both then get fast
   `DataRow` tests. Move the records and enums (`SharedIdCluster`, `SharedIdChoice`, …) to their
   own file. Leave the guard, seed and review in `SharedIdLog` behind the existing `dms.SharedIds`;
   splitting those further isn't worth the churn.
5. **One helper for admin background tasks.** `SeedSharedIds`, `MergeMatchingSharedIds` and
   `PreviewMergeMatchingSharedIds` repeat the same `StartAdminTask` / `GetTransientService` /
   `Task.Run` / `CompleteTask` / `Dispose` block, and `AdminController` has seven of them. A
   `RunAdminTask(name, Func<DanceMusicCoreService, Task<string>>)` on `DMController` would serve
   both controllers. Do the shared-id ones in PR 1; converting the others is optional.
6. **Small fixes found on the way:**
   - The doc comment on `SharedIdAction` describes `SharedIdMergePlan`.
   - `SharedIdBatchResult.Rejected` counts clusters but `Unshared` counts pairs, and the message
     reads them the same way. Say which each is.
   - `SharedIdCluster.Match` reruns the fuzzy match on every read, and the view reads it more
     than once per cluster. Compute it once when the songs are loaded, which also fixes the
     "songs loaded or not" ambiguity of the mutable `Songs` property.

## 4. Testing

### What's covered

| Layer | Tests | Assessment |
| --- | --- | --- |
| Guard (`CheckIds`, enrichment callers) | `SharedIdLogTests` (5), `SharedIdGuardTests` (4) | Good |
| Seed, clustering, `LoadSongs` follow-up | 3 | Good for the common cases |
| Merge, Apply, groups, names, removals | 17 | Good; this is where the recent work went |
| Merge-all plan and tiers | 6 | Reasons `Different` and `NoArtist` covered; `NoMerge`, `WithoutFeaturing` and `Close` reasons only through `Match_ClassifiesTheTiers` |
| `FeaturedCleanup.Propose` | 3 | Thin for the logic that renames songs |
| Controller actions, form binding, view model | 0 | Not covered |
| Rank sort (`SharedIdsPreviewModel`) | 0 | Not covered, and subtle |
| Client | 0 | Can't be, today |
| End to end | 0 | Not covered |

### Gaps to fill

**Domain (`m4dModels.Tests`), with PR 1:**

- `Repoint`: a song deleted without a merge marks its pending rows `Deleted`; two pending rows
  that land on the same pair fold into one with both rows' ids; a rejected row folding onto a
  pending one leaves it rejected.
- `Apply` with two groups where one group's merge is stale rejects nothing.
- A removal combined with **Not the same**, and removals in a three-song cluster where only one
  pair stops sharing an id.
- `PlanMergeMatching` gives `NoMerge` for a `.NoMerge` song, and the feat. and close reasons.
- `SharedIdMatcher` and the clustering function as `DataRow` tables, including the
  transitive case (A–B, B–C, D–E gives two clusters).
- `Song.AppendNameEdit` and `Song.AppendIdRemoval` directly: one individual artist written as an
  empty list, an album and an id on it removed together, an id with an unknown service prefix,
  an album index the song doesn't have. They're only reached through `Apply` today.
- `FeaturedCleanup.Propose`: feat. in both title and credit, the same artist featured twice in
  different spellings, no artist credit.
- `SharedIdLog.LoadPage` (new): sorted by rank, a stale cluster on the page triggers a reload, a
  preview failure yields a null preview without failing the page, paging past the end.

**Web (`m4d.Tests`):**

- Authorization by reflection for both new controllers, like
  `AdminControllerAuthorizationTests`: class-level `dbAdmin`, no `[AllowAnonymous]`, every POST
  validates antiforgery.
- The request DTO's mapping to `SharedIdChoice`: groups, null name picks, removals by song id,
  missing arrays.
- `SharedIdPreviewStore`: ranks from a plan, a merged song taking the best rank of its sources,
  a song it never saw ranked last.
- `SharedIdBatchResult.Summary()`: each clause appears only when its count is non-zero.

**Client (Vitest), with PR 2:**

- `ClusterChoice` unit tests: defaults (Leave for a rejected pair or `.NoMerge`, otherwise
  Merge); split detection; a name pick outside every group; an album removal covering its ids;
  `toRequest()` for each action.
- Page tests with a fixture model (the `admin-users` pattern: `loadTestPage`, `testPageSnapshot`,
  real `bootstrap-vue-next` components): the badges per tier, the split note, struck and
  disabled boxes, and that Apply posts the expected request (mock `getMenuContext()` with
  `vi.mock` for `axiosXsrf`).

**End to end (Playwright), PR 4.** One spec under `e2e/tests/` as `admin`: create two songs with
distinct Spotify ids under a title unique to the run, give the second the first's ISRC through the
song API, run **Queue existing shared ids**, wait on `/Admin/AdminStatus`, open the page, find the
cluster, choose **Not the same**, Apply, and check it's gone and stays gone after another seed.
A second case merges and checks the merged song. To verify first: that an edit through the song
API doesn't pass through the enrichment guard (the guard is only on enrichment, so it shouldn't),
and that a seed on the sandbox's index is quick enough for a test.

## Sequencing

| PR | Contents | Visible change |
| --- | --- | --- |
| 1 | New controllers with the Razor page moved as is; `LoadPage`, `SharedIdPreviewStore`, ids instead of indexes, matcher and clustering split out, background-task helper; the domain and web tests above. | URLs move from `/Admin/…` to `/SharedIds…` |
| 2 | Page model DTO, apply endpoint, the Vue app and its tests; delete `SharedIds.cshtml` and `SharedIdChoiceForm`. Update the reference doc. | Same page, plus disabled-when-irrelevant controls |
| 3 (optional) | Group-aware preview endpoint and **If merged** per group. | **If merged** follows groups and removals |
| 4 | Playwright spec. | None |

PR 1 has value without the others: if the Vue app is deferred, the Razor page can bind to the new
choice shape with less work than it took to build the current one.

When this ships, fold the as-built design into [shared-id-review](../songs/shared-id-review.md),
add a History line, and delete this plan.

## Decisions

- **URLs** (2026-11-14): move to `/SharedIds` with conventional routes, no redirects from the old
  `/Admin/…` ones.
- **Beyond this feature** (2026-11-14): the catalog upload review (`ReviewBatch`, `CommitPreview`
  and the in-memory `Reviews` list in `AdminController`) has the same shape: a page of choices,
  state held in the controller, no tests. It moves out of `AdminController` too, after this one,
  in a plan of its own.

## Related

- [shared-id-review](../songs/shared-id-review.md): how the review works today
- [partner-api-standalone-host](partner-api-standalone-host.md): PR 5 adds a second queue and a source filter to this page
- [testing-patterns](../dev-testing/testing-patterns.md): song creation and `TestSongIndex`
- [playwright-e2e-testing](../dev-testing/playwright-e2e-testing.md): the E2E suite
