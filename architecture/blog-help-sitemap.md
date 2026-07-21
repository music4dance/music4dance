# Blog / Help / Site Map linking

Current-state reference for how blog posts (hosted on the separate WordPress site
`music4dance.blog`) and help articles get linked into `www.music4dance.net`. This is a manual,
hand-edited process today — this doc exists to make that process legible before any automation
work starts.

## The two data files

| File | Purpose | Populated with |
| --- | --- | --- |
| `m4d/ClientApp/src/assets/content/blogmap.txt` | Every blog post, grouped into WordPress categories | Category name/slug/description, then one row per post |
| `m4d/ClientApp/src/assets/content/helpmap.txt` | The help-article outline | Section name, then one row per article |

Both are plain tab-separated text files, hand-edited in an editor (not generated from the
WordPress API or anything else). There is no validation, schema, or build-time check on their
contents — a malformed row is either silently dropped or silently misparsed (see
[Data quirks in blogmap.txt](#data-quirks-in-blogmaptxt-fixed-2026-07-20) below).

`m4d/wwwroot/content/blogmap.txt` and `helpmap.txt` are **build output, not source** — `wwwroot/`
is entirely gitignored. They're copied there from `ClientApp/src/assets/content/` by the `assets`
MSBuild target in [`m4d/m4d.csproj`](../m4d/m4d.csproj) (`BeforeTargets="Build"`, copies
`ClientApp/src/assets/**/*` to `wwwroot/%(RecursiveDir)`). **Always edit the copy under
`ClientApp/src/assets/content/`** — the `wwwroot` copy is regenerated on every `dotnet build` and
any direct edit there is lost.

## File format

Both files use a tab-indentation outline format: the number of leading tab characters on a line is
its depth in the tree, and depth can only change by one level at a time between consecutive lines
(a jump of more than one level is silently skipped by the parser — see
[`SiteMapFile`](../m4d/ViewModels/SiteMapInfo.cs)). Beyond indentation, columns are tab-separated
positionally — there are no headers and no column names in the file itself.

### `blogmap.txt`

- **Depth 0 (category) row** — 3 columns: `Title`, `Reference` (`blog/category/<slug>`),
  `Description` (the WordPress category description).
- **Depth 1 (post) row** — up to 5 columns: `Title`, `Reference` (`blog/<post-slug>/`),
  `Description` (an excerpt, frequently containing raw `<a href="...">` HTML — it's rendered with
  `v-html` client-side, see below), `OneTime` (optional — **any non-whitespace text in this column
  marks the post as "OneTime"**, the literal string doesn't matter), `Date` (the post's WordPress
  publish date, `YYYY-MM-DD`).

`OneTime` posts are excluded from the rotating "featured post" links on the home page and from the
recursive `BlogFeatureLink` listing under Info ▸ Blog — it's used for early
announcement/one-off posts that shouldn't keep resurfacing. `Date` is used only to find the
"newest" post (max `Date` wins, via plain string comparison, which sorts correctly for `YYYY-MM-DD`)
for the home page's primary featured link; it does not drive any visible sort order elsewhere.

Until 2026-07-21 this column held a hand-assigned integer ("`Order`", roughly but not strictly
chronological) instead of a real date — see
[Migrating `Order` to `Date`](#migrating-order-to-date-2026-07-21) below for why and how that
changed.

### `helpmap.txt`

- **Depth 0 (section) row** — 1–2 columns: `Title`, and optionally a `Reference` if the section
  heading is itself a clickable article (e.g. `Beta`, `Feedback`, `Bug Report`, `Subscriptions`).
- **Depth 1+ (article) row** — 2 columns: `Title`, `Reference`
  (`blog/music4dance-help/<slug>/`). No description/OneTime/Date columns exist for help rows.

## How a new post/article actually gets linked in today

1. Write and publish the post on WordPress (`music4dance.blog`) or the article under
   `music4dance.blog/music4dance-help/`.
2. Manually add a new tab-indented row to `blogmap.txt` (or `helpmap.txt`), by hand, under the
   right category/section:
   - Copy the post title.
   - Copy the post's URL slug into the `Reference` column (`blog/<slug>/` — the loader rewrites
     `blog/...` to `https://music4dance.blog/...` at render time, see `MakeFullPath` in both
     [`SiteMapInfo.cs`](../m4d/ViewModels/SiteMapInfo.cs) (server) and
     [`SiteMapInfo.ts`](../m4d/ClientApp/src/models/SiteMapInfo.ts) (client) — these two are
     independent, hand-kept-in-sync implementations of the same rewrite rule).
   - Copy/write an excerpt into `Description` (blog rows only).
   - Copy the post's publish date (from WordPress, or from the
     [WordPress.com REST API](https://developer.wordpress.com/docs/api/1.1/get/sites/%24site/posts/))
     into `Date` as `YYYY-MM-DD` (blog rows only) — see
     [Migrating `Order` to `Date`](#migrating-order-to-date-2026-07-21) for a script that automates
     this by slug lookup instead of doing it by hand.
   - Leave `OneTime` blank, or put any text in it, to suppress the post from the home page
     rotation.
3. Get the edited file onto the server, via either path:
   - **Normal path**: commit the change, build/deploy as usual — the `assets` MSBuild target
     copies `ClientApp/src/assets/content/*` into `wwwroot/content/` as part of the build.
   - **Fast path (skip the full build/deploy)**: FTP/FTPS the edited `blogmap.txt` and/or
     `helpmap.txt` directly to the App Service, overwriting the file(s) at
     `/site/wwwroot/m4d/wwwroot/content/` (using the site's FTPS deployment credentials — see
     [`SELF_CONTAINED_DEPLOYMENT.md`](SELF_CONTAINED_DEPLOYMENT.md) for the app's layout on the App
     Service; the startup command runs `/home/site/wwwroot/m4d`, and `wwwroot/content/` sits
     alongside it). This bypasses source control entirely, so **remember to also commit the same
     change to `ClientApp/src/assets/content/` in the repo** — otherwise the next normal deploy
     silently overwrites the live file with the stale, un-updated one from source.
4. In production, the parsed tree is cached in a static field (`SiteMapInfo.Categories`, see
   `SiteMapInfo.cs`) after first load, so either path above requires forcing a reload: an admin
   hits **Admin ▸ Initialization Tasks ▸ "Update Sitemap"**
   (`AdminController.UpdateSitemap`, calls `SiteMapInfo.ReloadCategories`) to force a re-read of the
   file from disk without restarting the app. This is what makes the FTP fast path useful — a typo
   fix or new post can go live without a full build/deploy cycle, at the cost of the repo/server
   drift risk noted above.

## Where the parsed data is consumed

`SiteMapFile` (in [`SiteMapInfo.cs`](../m4d/ViewModels/SiteMapInfo.cs)) parses one file into a tree
of `SiteMapEntry`. `SiteMapInfo.LoadCategories` wires two `SiteMapFile` instances into the "Info"
category, alongside hand-coded static entries (About, FAQ, etc.):

- `new SiteMapFile("helpmap", fileProvider) { Title = "Help", Reference = "blog/music4dance-help" }`
- `new SiteMapFile("blogmap", fileProvider) { Title = "Blog", Reference = "blog" }`

From there:

| Consumer | What it shows |
| --- | --- |
| `HomeController.Index` → `HomeModel` → home page (`pages/home/App.vue`) | `HomeModel.BlogEntries` = the Blog entry's children (i.e. the blog categories, each still holding its post children). `App.vue` flattens this to all posts, picks the highest-`Date` post as the primary head link, and picks 3 more at random (excluding `OneTime` posts) for the rotating header links. The "Info" home section also renders the full blog tree via `<BlogFeatureLink>`, which recursively skips any entry (and its subtree) where `oneTime` is set. |
| `HomeController.SiteMap` → `Views/Home/SiteMap.cshtml` + `_SiteMapEntry.cshtml` | The `/Home/SiteMap` page — a plain nested `<ul>` rendering of every category (Music/Info/Tools), including the full Help and Blog trees, at unlimited depth. This is the only place `helpmap.txt`'s data is used. |

`m4d/ClientApp/src/models/SiteMapInfo.ts` is a client-side `typedjson` mirror of the server
`SiteMapEntry` shape (title/reference/description/oneTime/date/children), used to deserialize the
`HomeModel` JSON payload the `Vue3()` helper sends down for the home page. It re-implements
`fullPath`'s `blog/` → `https://music4dance.blog/` rewrite independently of the C# version.

## Data quirks in `blogmap.txt` (fixed 2026-07-20)

The file had accumulated a few artifacts from hand-editing, illustrating how easily this format
corrupts silently since there's no validation. Cleaned up on 2026-07-20 — noted here so future
edits don't reintroduce the same shape of mistake:

- **A dead 6th column.** ~18 rows (mostly "Special Occasions" posts) had a 6th tab-separated
  value, e.g. `...\t46\tSpecial Occasions`. `SiteMapFile` only ever reads columns 0–4, so anything
  past `Order` was silently ignored — a leftover cross-category tag from an earlier version of the
  format. Removed.
- **Two rows with the slug accidentally in the `OneTime` column.** The two "Wedding Music Part
  I/II" posts had an unrelated, older slug string sitting in the `OneTime` column instead of
  blank. Since `OneTime` just checks "is this column non-whitespace", both posts were
  unintentionally marked `OneTime` and excluded from home-page rotation. Column cleared.
- **One row with `OneTime` typed into the `Order` column.** "Sorry about that nasty bug" had
  `OneTime` in both the `OneTime` and `Order` columns (`Order`'s real value, `80`, had ended up in
  the unused 6th column instead). Because `int.TryParse` on `"OneTime"` fails, this post's `Order`
  was silently defaulting to `0`. Fixed to read `OneTime` / `80`.
- **One row with a stray slug in the `OneTime` column, no extra column involved.** "If you like to
  dance Cha-Cha to a song does that mean you 'like' that song?" had the slug of an unrelated,
  later post sitting in its `OneTime` column with no accompanying 6th-column artifact, so the
  6th-column scan above didn't catch it. Found instead while cross-checking every row's `OneTime`
  column for non-`OneTime` text (see the migration below). Column cleared.
- **Two rows whose `Reference` pointed at the wrong post entirely.** "Beta Feature: Export to a
  file" and "Western Partner Dances and Line Dances?" both had a `Reference` copy-pasted from a
  *different*, unrelated row (`holiday-music-for-partner-dancing-2022` and
  `ballroom-songs-for-your-first-dance`, respectively — each already correctly used by another
  row). Silent because the parser has no way to check a `Reference` resolves to the right post;
  only surfaced when the [`Order`-to-`Date` migration script](#migrating-order-to-date-2026-07-21)
  matched by slug against the real WordPress post list and found two slugs each claimed by two
  rows. Both `Reference`s corrected to their real slugs
  (`beta-feature-export-to-a-file`, `western-partner-dances-and-line-dances`).

## Migrating `Order` to `Date` (2026-07-21)

The hand-assigned `Order` integer (see [File format](#file-format) above) has been replaced with
the post's real publish `Date`, sourced from WordPress rather than eyeballed. This was a one-off
data migration plus a small rename through the code — not an ongoing process change; new posts
still need a `Date` value hand-entered (or looked up via the same slug-matching approach) when
their `blogmap.txt` row is added.

**Why**: `Order` was a manually incremented integer with no relationship to anything WordPress
knows about — assigning the next value meant eyeballing the previous highest number, and nothing
enforced that it was correct (see the two `Reference`-swap bugs above, both surfaced only by cross-
checking `Order` against real post dates). A real publish date is independently verifiable, sorts
correctly as a plain string (`YYYY-MM-DD`), and removes the "guess the next number" step from
adding a post.

**Data source**: the [WordPress.com REST API](https://developer.wordpress.com/docs/api/1.1/get/sites/%24site/posts/),
`https://public-api.wordpress.com/rest/v1.1/sites/music4dance.blog/posts/`. It's public and
unauthenticated but paginates at 20 posts/page by default — pass `number=100` and `page=N` to page
through everything (the site currently has 143 posts, so two pages). Each post's `slug` and `date`
fields are what matter here.

**Script**: `local/scripts/blogmap-add-dates.js` (gitignored, lives under `local/` per this repo's
scratch-file convention — copy it out of `local/` if it needs to become a permanent, checked-in
tool). Given a merged posts JSON (slug + date + title per post) and `blogmap.txt`, it:

1. Derives each blogmap post row's slug from its `Reference` column the same way `SiteMapInfo.cs`'s
   `MakeFullPath` does (strip `blog/` prefix and trailing `/`).
2. Looks the slug up in the WordPress post list and replaces the row's last column with that post's
   `date`, truncated to `YYYY-MM-DD` (WordPress.com's `date` field carries the site's Pacific-time
   offset, so slicing the first 10 characters avoids a UTC day-shift).
3. Reports, rather than silently applying, anything that doesn't line up cleanly: blogmap rows
   whose slug has no matching WordPress post, and — the useful part — **any slug claimed by more
   than one blogmap row**, which is how the two `Reference`-swap bugs above were found. Rows in
   either category are left untouched so they can be fixed by hand and the script re-run.
4. Also reports WordPress posts that have no blogmap row at all (candidates to add, not acted on
   automatically).

Run with `node local/scripts/blogmap-add-dates.js` from the repo root; it writes
`local/blogmap.dated.txt` for review rather than editing `blogmap.txt` in place.

**Code changes that went with it** (all renames, `Order`/`order` → `Date`/`date`, no behavior
change beyond the string-vs-integer comparison):

- [`SiteMapInfo.cs`](../m4d/ViewModels/SiteMapInfo.cs) — `SiteMapEntry.Order` (`int`) →
  `SiteMapEntry.Date` (`string`); `SiteMapFile`'s parser drops the `int.TryParse` and just carries
  column 4 through as-is (or `null` if the row doesn't have one).
- [`SiteMapInfo.ts`](../m4d/ClientApp/src/models/SiteMapInfo.ts) — `order?: number` →
  `date?: string`.
- [`pages/home/App.vue`](../m4d/ClientApp/src/pages/home/App.vue) — `newestEntry` now picks the max
  by `date` string comparison instead of max `order`.
- [`BlogFeatureLink.vue`](../m4d/ClientApp/src/components/BlogFeatureLink.vue) — the recursive
  `v-for`'s `:key` switched from `child.order` to `child.reference`. `Order` values were hand-
  assigned unique integers, guaranteeing unique Vue keys; `Date` values are not guaranteed unique
  (two posts could publish same-day), so the key needed to move to something that already is
  unique — the post's own `reference` slug.
- `pages/home/__tests__/model.ts` (frozen test fixture mirroring a `HomeModel` JSON payload) was
  regenerated from the real `blogmap.txt` via a second script,
  `local/scripts/update-home-model-fixture.js`, rather than hand-edited — same slug-matching
  approach, since two of the fixture's old `order` values belonged to rows whose `Reference` was
  itself wrong (see above) and a blind number-preserving rename would have kept that error baked
  into the test data.

## Related

- [`SiteMapInfo.cs`](../m4d/ViewModels/SiteMapInfo.cs) — server-side parser/tree + static cache
- [`SiteMapInfo.ts`](../m4d/ClientApp/src/models/SiteMapInfo.ts) — client-side DTO mirror
- [`BlogFeatureLink.vue`](../m4d/ClientApp/src/components/BlogFeatureLink.vue) — recursive blog-tree renderer used on the home page
- [`HomeModel.cs`](../m4d/ViewModels/HomeModel.cs) / [`HomeModel.ts`](../m4d/ClientApp/src/pages/home/HomeModel.ts)
- [`Views/Home/SiteMap.cshtml`](../m4d/Views/Home/SiteMap.cshtml) / [`_SiteMapEntry.cshtml`](../m4d/Views/Home/_SiteMapEntry.cshtml)
