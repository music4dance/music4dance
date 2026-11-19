# Cloudflare Edge Cache for Anonymous Pages

**Type:** Runbook
**Status:** Current (written 2026-11-18; the Cloudflare steps haven't been run yet)
**Last verified:** 2026-11-18 (against `CacheControlMiddleware` and `azure-pipelines.yml`)
**Code:** `m4d/Middleware/CacheControlMiddleware.cs`, `azure-pipelines.yml` (Purge Cloudflare cache)

## When to use

- Setting up the edge cache for the first time (once).
- Checking that it works, or why a page isn't cached.
- Purging by hand: after a failed deploy purge, or to show a fix before the cache expires.
- Turning it off.

How it works and why: [hosting-and-identity § Request pipeline notes](../infrastructure/hosting-and-identity.md#request-pipeline-notes).
In short, the app marks anonymous pages that are the same for every signed-out visitor with
`Cloudflare-CDN-Cache-Control: max-age=86400`, a Cloudflare Cache Rule caches HTML only for
requests without the `music4dance` sign-in cookie, and every production deploy purges the zone.

## Prerequisites / access

- Cloudflare dashboard access to the `music4dance.net` zone (Free plan), with permission to
  create API tokens.
- Edit access to the Azure DevOps deploy pipeline (its variables).
- `az` signed in to the `music4dance` tenant, with write access to App Configuration
  `music4dance`.

## Steps

### One-time setup (in this order)

1. **Create a purge-only API token.** Cloudflare dashboard → your profile (top right) →
   **My Profile → API Tokens → Create Token → Create Custom Token**.
   - Name: `azure-devops-cache-purge`.
   - Permissions: **Zone · Cache Purge · Purge**. Nothing else.
   - Zone Resources: **Include · Specific zone · music4dance.net**.
   - Client IP filtering: leave empty (hosted agents use changing IPs).
   - TTL: either none, or an end date with a calendar reminder: an expired token makes the
     deploy's purge step fail.
   - **Create Token** and copy it; it's shown only once. Check it:

     ```bash
     curl -s https://api.cloudflare.com/client/v4/user/tokens/verify -H "Authorization: Bearer <token>"
     ```

     The answer includes `"status":"active"`.
2. **Find the zone ID.** The `music4dance.net` zone → **Overview**; it's under **API** in the
   right-hand column.
3. **Add both to the pipeline.** Azure DevOps → Pipelines → the deploy pipeline → **Edit →
   Variables → +**:
   - `CLOUDFLARE_ZONE_ID`: the zone ID.
   - `CLOUDFLARE_API_TOKEN`: the token, with **Keep this value secret** checked.

   Leave **Let users override this value when running this pipeline** unchecked on both, then
   **Save**.
4. **Deploy the PR** that added the edge cache, to test and then production. The production
   run's last step, **Purge Cloudflare cache**, should log `The new build is serving after Ns`
   and `Cloudflare cache purged`. That proves the token works. Nothing is cached yet.
5. **Turn on the origin header** (Production label), then bump the sentinel so the app reloads
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
6. **Turn on Tiered Cache** (free): **Caching → Tiered Cache → Smart Tiered Caching Topology**.
   Data centers then fill from a regional tier instead of each going to the origin.
7. **Create the Cache Rule.** **Caching → Cache Rules → Create rule**. Name it
   `Anonymous HTML`, choose **Edit expression**, and paste:

   ```txt
   (http.host eq "www.music4dance.net"
    and http.request.method eq "GET"
    and not http.cookie contains "music4dance="
    and not starts_with(http.request.uri.path, "/identity")
    and not starts_with(http.request.uri.path, "/api")
    and not starts_with(http.request.uri.path, "/admin")
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

### Purge by hand

- **Everything:** **Caching → Configuration → Purge Everything**. Use this after a failed or
  skipped deploy purge, or if pages load with broken scripts.
- **One page:** **Custom Purge → URL**, with the exact URL. Each query string is a separate
  entry, so `/song/album?title=X` and `/song/album?title=X&page=2` are purged separately.
- **From a shell:**

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

Over the next day, check **Analytics → Caching** in Cloudflare for the HTML hit ratio, and the
App Service **Requests** metric for the drop at the origin.

### Troubleshooting

- **Never `HIT`, status `DYNAMIC`:** the rule doesn't match. Check the expression and that the
  rule is deployed.
- **Never `HIT`, status `BYPASS` or `MISS` every time:** the origin header is missing (step 5:
  the setting or the sentinel), or the page is one the app keeps out on purpose (a bot user
  agent, a service unhealthy). Don't switch Edge TTL to *Ignore cache-control header*: that
  would cache bot variants, outage pages and errors too.
- **The origin sends the header (step 5 check passes) but it's still `BYPASS`:** Cloudflare
  isn't reading `Cloudflare-CDN-Cache-Control` in this Edge TTL mode. Turn the rule off and
  revisit the origin side. The fallback is for the app to send
  `Cache-Control: public, s-maxage=86400, max-age=0` instead, which first needs
  `UseResponseCaching` removed, because it would serve that page to signed-in users.
- **Broken scripts or styles after a deploy:** the purge failed or was skipped. Purge
  everything.
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

The pipeline variables can stay. If you delete the token or the variables while the rule is
on, deploys stop purging: the step warns and skips when the variables are missing, and fails
when the token is rejected.
