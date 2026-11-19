# Cloudflare Edge Cache for Anonymous Pages

**Type:** Runbook
**Status:** Current (written 2026-11-18; the Cloudflare steps haven't been run yet)
**Last verified:** 2026-11-19 (against `CacheControlMiddleware` and `AssetArchive`)
**Code:** `m4d/Middleware/CacheControlMiddleware.cs`, `m4d/Services/AssetArchive.cs`,
`m4d/Middleware/ArchivedAssetMiddleware.cs`

## When to use

- Setting up the edge cache for the first time (once).
- Checking that it works, or why a page isn't cached.
- Purging by hand: to show a change before the cache expires (see [After a deploy](#after-a-deploy)).
- Turning it off.

How it works and why: [hosting-and-identity § Request pipeline notes](../infrastructure/hosting-and-identity.md#request-pipeline-notes).
In short, the app marks anonymous pages that are the same for every signed-out visitor with
`Cloudflare-CDN-Cache-Control: max-age=86400`, a Cloudflare Cache Rule caches HTML only for
requests without the `music4dance` sign-in cookie. Deploys don't purge it. A page cached before
a deploy points at the previous build's bundles, and the app keeps recent builds' bundles and
serves them ([hosting-and-identity § Bundles from earlier builds](../infrastructure/hosting-and-identity.md#bundles-from-earlier-builds)).

## Prerequisites / access

- Cloudflare dashboard access to the `music4dance.net` zone (Free plan).
- `az` signed in to the `music4dance` tenant, with write access to App Configuration
  `music4dance`.

## Steps

### One-time setup (in this order)

1. **Deploy the edge cache and the asset archive** (#414), to test and then
   production. Nothing is cached yet. Then check that production archived its bundles:

   ```bash
   az rest --method get --resource https://management.azure.com/      --url https://msc4dnc.scm.azurewebsites.net/api/vfs/data/m4d/AppData/asset-archive/      --query "[].name" -o tsv
   ```

   Expect one folder per build, named with 16 hex characters. Don't go on until the running
   build is archived: pages cached from a build that never archived itself break at its next
   deploy.
2. **Turn on the origin header** (Production label), then bump the sentinel so the app reloads
   configuration:

   ```bash
   az appconfig kv set -n music4dance --key EdgeCache:Enabled --value true --label Production --yes
   az appconfig kv set -n music4dance --key Configuration:Sentinel --value "$(date +%s)" --yes
   ```

   Check the origin directly (Cloudflare strips the header, so this has to go to the App
   Service host):

   ```bash
   curl -s -o /dev/null -D - https://msc4dnc.azurewebsites.net/dances | grep -i cache-control
   ```

   Expect `cache-control: no-cache` and `cloudflare-cdn-cache-control: max-age=86400`.
   This is still harmless: Cloudflare doesn't cache HTML until the next step.
3. **Turn on Tiered Cache** (free): **Caching → Tiered Cache → Smart Tiered Caching Topology**.
   Data centers then fill from a regional tier instead of each going to the origin.
4. **Create the Cache Rule.** **Caching → Cache Rules → Create rule**. Name it
   `Anonymous HTML`, choose **Edit expression**, and paste:

   ```txt
   (http.host eq "www.music4dance.net"
    and http.request.method eq "GET"
    and not http.cookie contains "music4dance="
    and not starts_with(http.request.uri.path, "/identity")
    and not starts_with(http.request.uri.path, "/api")
    and not starts_with(http.request.uri.path, "/admin")
    and not starts_with(http.request.uri.path, "/payment")
    and not cf.client.bot)
   ```

   Then:
   - **Cache eligibility:** Eligible for cache.
   - **Edge TTL:** *Use cache-control header if present, bypass cache if not*. This is what
     keeps bot variants, error pages and pages rendered during an outage out of the cache: the
     app leaves the header off them.
   - **Browser TTL:** *Respect origin TTL*, so browsers keep revalidating (`no-cache`) and a
     visitor who signs in sees the dynamic page right away.
   - Leave the cache key at its default (it includes the query string).
   - **Deploy**.

   `not cf.client.bot` sends verified crawlers (Googlebot, Bingbot) to the origin, so they keep
   getting their own variant. If the editor rejects that field, drop the line: the origin
   already leaves Googlebot's page out of the cache, and Googlebot would just get the human page
   from the cache.

### After a deploy

Nothing is needed. Pages cached before the deploy keep working for up to a day, but they show
the previous build. Purge by hand when that matters:

- A change should be seen by signed-out visitors right away.
- The deploy changed an API that page scripts call in a way old scripts can't handle. A cached
  page runs the previous build's scripts against the new server.

### Purge by hand

- **Everything:** **Caching → Configuration → Purge Everything**.
- **One page:** **Custom Purge → URL**, with the exact URL. Each query string is a separate
  entry, so `/song/album?title=X` and `/song/album?title=X&page=2` are purged separately.
- **From a shell**, with an API token that has only **Zone · Cache Purge · Purge** on
  `music4dance.net`:

  ```bash
  curl -s -X POST "https://api.cloudflare.com/client/v4/zones/<zone-id>/purge_cache" \
    -H "Authorization: Bearer <token>" -H "Content-Type: application/json" \
    --data '{"purge_everything":true}'
  ```

## Verification

Run each request twice. The first can be a `MISS`; the second should be a `HIT`:

```bash
curl -s -o /dev/null -D - https://www.music4dance.net/dances | grep -i "cf-cache-status\|cache-control"
```

| Check | Expect |
| --- | --- |
| Anonymous page (`/dances`, `/song/album?title=...`) | `cf-cache-status: HIT` the second time, `cache-control: no-cache`, and no `cloudflare-cdn-cache-control` |
| Signed in: add `-H "Cookie: music4dance=x"`, or look at a page's response headers in the browser's dev tools while signed in | `cf-cache-status: DYNAMIC` |
| `/api/health/status`, `/identity/account/login` | `DYNAMIC` |
| A 404 (`/song/details/not-a-song`) | Not `HIT` |
| `/home/contribute` (it has a form token), `/payment/cancel` | Not `HIT`; the origin sends no `cloudflare-cdn-cache-control` |

Over the next day, check **Analytics → Caching** in Cloudflare for the HTML hit ratio, and the
App Service **Requests** metric for the drop at the origin.

### Troubleshooting

- **Never `HIT`, status `DYNAMIC`:** the rule doesn't match. Check the expression and that the
  rule is deployed.
- **Never `HIT`, status `BYPASS` or `MISS` every time:** the origin header is missing (step 2:
  the setting or the sentinel), or the page is one the app keeps out on purpose (a bot user
  agent, a service unhealthy). Don't switch Edge TTL to *Ignore cache-control header*: that
  would cache bot variants, outage pages and errors too.
- **The origin sends the header (step 2 check passes) but it's still `BYPASS`:** Cloudflare
  isn't reading `Cloudflare-CDN-Cache-Control` in this Edge TTL mode. Turn the rule off and
  revisit the origin side. The fallback is for the app to send
  `Cache-Control: public, s-maxage=86400, max-age=0` instead, which first needs
  `UseResponseCaching` removed, because it would serve that page to signed-in users.
- **Broken scripts or styles after a deploy:** a cached page names bundles the archive doesn't
  have. Purge everything to fix it now. Then list the archive (step 1) and search the app log
  for `Couldn't archive this build's bundles`. A build that failed to archive leaves its pages
  without a fallback at the next deploy.
- **A fix or an edit isn't showing for signed-out visitors:** it will within a day; purge that
  URL to show it now.

## Rollback

1. **Turn the Cache Rule off:** **Caching → Cache Rules**, toggle `Anonymous HTML` off. This
   takes effect at once.
2. **Purge Everything**, so nothing cached is served if the rule is turned back on later.
3. Optionally turn the origin header off as well:

   ```bash
   az appconfig kv set -n music4dance --key EdgeCache:Enabled --value false --label Production --yes
   az appconfig kv set -n music4dance --key Configuration:Sentinel --value "$(date +%s)" --yes
   ```

