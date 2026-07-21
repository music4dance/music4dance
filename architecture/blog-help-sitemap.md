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
  marks the post as "OneTime"**, the literal string doesn't matter), `Order` (an integer,
  hand-assigned, roughly but not strictly chronological).

`OneTime` posts are excluded from the rotating "featured post" links on the home page and from the
recursive `BlogFeatureLink` listing under Info ▸ Blog — it's used for early
announcement/one-off posts that shouldn't keep resurfacing. `Order` is used only to find the
"newest" post (max `Order` wins) for the home page's primary featured link; it does not drive any
visible sort order elsewhere.

### `helpmap.txt`

- **Depth 0 (section) row** — 1–2 columns: `Title`, and optionally a `Reference` if the section
  heading is itself a clickable article (e.g. `Beta`, `Feedback`, `Bug Report`, `Subscriptions`).
- **Depth 1+ (article) row** — 2 columns: `Title`, `Reference`
  (`blog/music4dance-help/<slug>/`). No description/OneTime/Order columns exist for help rows.

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
   - Manually figure out and increment the next `Order` value (blog rows only) — there's no
     tooling that computes this; it's done by eyeballing the previous highest number.
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
| `HomeController.Index` → `HomeModel` → home page (`pages/home/App.vue`) | `HomeModel.BlogEntries` = the Blog entry's children (i.e. the blog categories, each still holding its post children). `App.vue` flattens this to all posts, picks the highest-`Order` post as the primary head link, and picks 3 more at random (excluding `OneTime` posts) for the rotating header links. The "Info" home section also renders the full blog tree via `<BlogFeatureLink>`, which recursively skips any entry (and its subtree) where `oneTime` is set. |
| `HomeController.SiteMap` → `Views/Home/SiteMap.cshtml` + `_SiteMapEntry.cshtml` | The `/Home/SiteMap` page — a plain nested `<ul>` rendering of every category (Music/Info/Tools), including the full Help and Blog trees, at unlimited depth. This is the only place `helpmap.txt`'s data is used. |

`m4d/ClientApp/src/models/SiteMapInfo.ts` is a client-side `typedjson` mirror of the server
`SiteMapEntry` shape (title/reference/description/oneTime/order/children), used to deserialize the
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

## Related

- [`SiteMapInfo.cs`](../m4d/ViewModels/SiteMapInfo.cs) — server-side parser/tree + static cache
- [`SiteMapInfo.ts`](../m4d/ClientApp/src/models/SiteMapInfo.ts) — client-side DTO mirror
- [`BlogFeatureLink.vue`](../m4d/ClientApp/src/components/BlogFeatureLink.vue) — recursive blog-tree renderer used on the home page
- [`HomeModel.cs`](../m4d/ViewModels/HomeModel.cs) / [`HomeModel.ts`](../m4d/ClientApp/src/pages/home/HomeModel.ts)
- [`Views/Home/SiteMap.cshtml`](../m4d/Views/Home/SiteMap.cshtml) / [`_SiteMapEntry.cshtml`](../m4d/Views/Home/_SiteMapEntry.cshtml)
