# Cloudflare Migration (DNS and Edge Proxy)

**Type:** Plan
**Status:** In progress. The owner has chosen Cloudflare over Front Door for attack-mitigation step 2.2 and is starting on the Free plan (2026-11-04). Phases 1 to 3 are done: the zone is Active on Cloudflare's nameservers with every record DNS only, the edge certificate is Active, and mail and verification checks pass (2026-11-05). SSL/TLS is Full (strict) with Always Use HTTPS on, and the dangling `awverify` CNAMEs are deleted (2026-11-06). Phase 4 (proxy on) is next, after #385 is deployed.
**Last verified:** 2026-11-05 (every section 2 name queried against Namecheap and both Cloudflare nameservers: no differences)
**Code:** `m4d/Configuration/M4dApplicationExtensions.cs` (`UseForwardedHeaders`), `m4d/Middleware/RateLimitingMiddleware.cs` (`GetClientIdentifier`), `m4d/Areas/Identity/Pages/Account/Login.cshtml.cs`, `Register.cshtml.cs`, `LoginModelBase.cs`, `m4d/Controllers/PaymentController.cs`, `azure-pipelines.yml` (readiness check)

This plan moves `music4dance.net` DNS from Namecheap to Cloudflare and puts Cloudflare's proxy in
front of the App Service (`msc4dnc`). It's step 2.2 of
[attack-mitigation-phase2](attack-mitigation-phase2.md) and option B3 of
[bot-load-and-public-page-rendering](bot-load-and-public-page-rendering.md#b3-in-detail-what-moving-to-cloudflare-involves-2026-10-31).
Those docs already compare Cloudflare with Front Door, so this one covers only how to do it.

The **registration stays at Namecheap**. Only the nameservers change. Registrar and DNS host are
independent, so there's no reason to transfer the domain, though moving it to Cloudflare
Registrar later (at-cost renewals) is an option.

HTML caching isn't part of this plan. Cloudflare doesn't cache HTML by default, and the app
marks anonymous pages `no-cache` and signed-in pages `no-store`. Edge caching of HTML is Phase 2
of the bot-load plan and can follow once this is stable.

## 1. Can this run on the Free plan?

Yes. Free covers what step 2.2 needs: unmetered DDoS protection, the proxy, Universal SSL at the
edge, a free Origin CA certificate, Bot Fight Mode, AI Crawl Control, Browser Integrity Check,
the Free managed WAF ruleset, 5 custom WAF rules, 1 rate-limiting rule, and "I'm Under Attack"
mode. Free isn't billed by requests or bandwidth, so a flood costs nothing.

"Deferring to Cloudflare on which bots to allow" means accepting **Bot Fight Mode** as it comes:

- It's one switch for the whole zone, on or off. It can't be tuned, and no WAF rule can skip it
  for a path, a host or a caller.
- Verified bots (Googlebot, Bingbot, and the AI crawlers on Cloudflare's verified list such as
  GPTBot and ClaudeBot) pass. Unverified crawlers and scripts may be challenged or blocked. This
  is where Cloudflare's judgement replaces ours. Reflectionbot, for example, may not be on the
  list.
- It's aimed at simple bots. Whether it stops the residential-proxy headless wave seen on
  2026-10-31 isn't known until it's tried. Pro's Super Bot Fight Mode adds JavaScript
  detections and settings that can be adjusted.

Free's limits that affect this site, and how the plan deals with each:

| Limit | Affects | Handling |
| --- | --- | --- |
| Bot Fight Mode can't be skipped | Any script that calls `www`: the Logic Apps (`/playlist/updatebatch`, `/api/recompute/*`) and any future partner API on `www` | Move the Logic Apps to `msc4dnc.azurewebsites.net`, which bypasses Cloudflare (Phase 1). The deploy pipeline already uses that host. The planned `api.music4dance.net` stays DNS only. |
| 100 MB request body | Admin uploads: `LoadIdx` index files are about 135 MB (`local/index-2026-09-30.txt`) | Upload on `msc4dnc.azurewebsites.net`, not `www`. |
| 100 s origin timeout (the visitor gets a 524) | Long synchronous admin actions | Same: use the `azurewebsites.net` host for those. |
| No log push (Enterprise only) | HTTP request logs, which record the connecting IP. After the proxy goes on, that's Cloudflare's address. | The app fix in Phase 1 corrects the IP the app sees. The `analyze-traffic` skill's per-IP grouping needs a follow-up (section 4). |

The `azurewebsites.net` workarounds depend on the origin staying reachable directly. That's why
Phase 7 (locking the origin) is optional and doesn't use App Service IP restrictions.

**When to pay for Pro ($20/month annual, $25 monthly):** if Bot Fight Mode challenges something
the site needs and it can't be moved off `www`; if the headless wave still reaches the origin
with Bot Fight Mode on; or if the 5 custom rules or 1 rate-limit rule run out. Upgrading is a
plan change on the same zone, with no migration.

## 2. Current DNS inventory (queried 2026-11-04, reconciled 2026-11-05)

First queried against 1.1.1.1 for the usual names, then reconciled against the Namecheap
Advanced DNS page in Phase 2. Since Phase 3, **Cloudflare's DNS Records page is the source of
truth**; add new records there, not at Namecheap.

| Name | Type | Value | Proxy at Cloudflare |
| --- | --- | --- | --- |
| `@` | A | `40.82.255.134` (the App Service inbound IP) | DNS only at first, proxied in Phase 4 |
| `www` | CNAME | `msc4dnc.azurewebsites.net` | DNS only at first, proxied in Phase 4 |
| `@` | MX | `music4dance-net.mail.protection.outlook.com` (copy the priority from Namecheap) | n/a |
| `@` | TXT | `v=spf1 include:spf.protection.outlook.com -all` | n/a |
| `@` | TXT | `MS=ms79811529` (Microsoft 365 verification) | n/a |
| `@` | TXT | `ms-domain-verification=7aa2f36c-…` (ACS email domain) | n/a |
| `@` | TXT | `google-site-verification=ts3n79ot…` (Search Console) | n/a |
| `@` | TXT | `music4dance.azurewebsites.net` (legacy, probably an old App Service verification) | n/a. Copy as-is; clean up afterwards |
| `@` | TXT | ~~`Value: v=spf1 include:hotmail.com ~all`~~ (malformed legacy record) | **Deleted 2026-11-05.** Cloudflare flagged it as a second SPF record, which can send mail to spam |
| `asuid` | TXT | `0316C3E4…ED55` (App Service domain verification) | n/a |
| `asuid.www` | TXT | same value | n/a |
| ~~`awverify`~~ | CNAME | `awverify.m4d.azurewebsites.net` (pre-`asuid` App Service verification, for an old app name) | **Deleted 2026-11-06** (see below) |
| ~~`awverify.www`~~ | CNAME | `awverify.m4d.azurewebsites.net` | **Deleted 2026-11-06** |
| `autodiscover` | CNAME | `autodiscover.outlook.com` | **DNS only** |
| `selector1._domainkey` | CNAME | `selector1-music4dance-net._domainkey.msc4dnc.onmicrosoft.com` | **DNS only** |
| `selector2._domainkey` | CNAME | `selector2-music4dance-net._domainkey.msc4dnc.onmicrosoft.com` | **DNS only** |
| `selector1-azurecomm-prod-net._domainkey` | CNAME | `selector1-azurecomm-prod-net._domainkey.azurecomm.net` (ACS DKIM) | **DNS only** |
| `selector2-azurecomm-prod-net._domainkey` | CNAME | `selector2-azurecomm-prod-net._domainkey.azurecomm.net` (ACS DKIM) | **DNS only** |
| `enterpriseregistration` | CNAME | `enterpriseregistration.windows.net` | **DNS only** |
| `enterpriseenrollment` | CNAME | `enterpriseenrollment.manage.microsoft.com` | **DNS only** |
| `lyncdiscover` | CNAME | `webdir.online.lync.com` | **DNS only** |
| `sip` | CNAME | `sipdir.online.lync.com` | **DNS only** |
| `_sip._tls` | SRV | priority 100, weight 0, port 443, target `sipdir.online.lync.com` | n/a |
| `_sipfederationtls._tcp` | SRV | priority 100, weight 0, port 5061, target `sipfed.online.lync.com` | n/a |
| `_dmarc` | TXT | `v=DMARC1; p=none` (monitoring only; added 2026-11-05) | n/a |
| `api` | CNAME | the `m4d-api` function app's default hostname (**planned**, partner API PR 6) | **DNS only, always** (see below) |
| `asuid.api` | TXT | the function app's domain verification ID (**planned**, PR 6) | n/a |

Also checked: no CAA record (Universal SSL needs none), no DNSSEC (no DS at the registry), and
nothing at `blog`, `mail` or `test`.

Every record except `@` and `www` must stay **DNS only**. Cloudflare's import tends to mark
CNAMEs as proxied, and a proxied `autodiscover` or DKIM CNAME breaks Outlook setup or mail
signing. The import missed the SRV records, the four `selector*._domainkey` CNAMEs, the
`awverify` CNAMEs and `asuid.www`; all were added by hand.

The two `awverify` CNAMEs were deleted early because Cloudflare flagged them as dangling.
`m4d.azurewebsites.net` no longer exists (NXDOMAIN, and no `m4d` app in the subscription), so
anyone could create an app with that name and claim the target. App Service has used the
`asuid` TXT records for verification for years, so nothing depends on `awverify`.

**`api` stays DNS only** when the
[partner API](partner-api-standalone-host.md#operations) gets its custom domain:

- Its Flex site-scoped managed certificate can only be issued and renewed while the name
  resolves to the function, the same problem Phase 5 solves for `www`.
- Bot Fight Mode can't be skipped for a host, and partner calls are server to server with no
  browser to pass a challenge.
- Keeping it off the proxy means a Cloudflare outage or setting mistake can't take the API down,
  the same independence the partner API plan wants from the web app.

## 3. Steps

The cutover is split so that each step changes one thing and can be undone on its own:

1. Move DNS with **everything DNS only**. Traffic still goes straight to Azure, so the move is
   invisible.
2. Turn the proxy on for `@` and `www`.
3. Swap the origin certificate.
4. Turn on bot protection.

### Phase 0: Prepare (no user impact)

1. **Secure the accounts.** Turn on two-factor authentication at Namecheap if it isn't already.
   Choose the email address for the Cloudflare login. It should **not** be an
   `@music4dance.net` address, so that account recovery still works if this zone's DNS breaks.
   `dwgray67@hotmail.com` works.
2. **Export the Namecheap records.** Domain List, then **Manage** next to `music4dance.net`,
   then **Advanced DNS**. Screenshot or copy every host record, including TTLs, MX priority and
   SRV fields. Reconcile them with section 2 and add anything missing to the table. Note
   anything Namecheap-specific, such as **URL Redirect Records** or **Email Forwarding**. Those
   are Namecheap features, not DNS, so Cloudflare can't import them. None are expected, because
   mail is Microsoft 365.
3. **Check DNSSEC is off** on the same page (DNSSEC toggle). The registry has no DS record, so
   it should be off. If it's on, turn it off and wait 24 hours before Phase 3, or resolvers will
   reject Cloudflare's answers.
4. **Lower the TTLs** on `@` (A) and `www` (CNAME) to 5 minutes, at least a day before Phase 3.
   That shortens how long old answers linger if something needs undoing.
5. **Note the App Service certificate expiry dates:** `az webapp config ssl list -g <group>
   --query "[].{name:name, hosts:hostNames, expires:expirationDate}"`. Phase 5 must finish
   before the managed certificates' next renewal.
6. **Check what the app sees as the client IP today.** On `/Admin/Diagnostics`, look at whether
   the rate-limit entries show real visitor IPs. If they do, App Service's forwarded-header
   handling is setting `RemoteIpAddress` from its own `X-Forwarded-For`, which the Phase 1
   middleware builds on. Record what you find here.

### Phase 1: App and caller changes (ship before Phase 4)

**Done 2026-11-04.** The client-IP change shipped in #385, and all six Logic Apps now call
`msc4dnc.azurewebsites.net`. The two steps below are kept for reference until this plan is folded
into the reference docs.

**PR: trust `CF-Connecting-IP` from Cloudflare only.** Behind the proxy,
`Connection.RemoteIpAddress` becomes a Cloudflare edge address. Everything that keys on it would
then lump thousands of visitors together: the identity rate limits and lockouts in
`RateLimitingMiddleware`, the login and register logging, the reCAPTCHA and Stripe remote IP in
`PaymentController`, and `/Admin/Diagnostics`. A login lockout would then hit everyone who shares
an edge node.

- Add a small middleware, or a `ForwardedHeadersOptions` setup, placed with the existing
  `UseForwardedHeaders` call and before `UseAuthentication`. If the peer address is inside
  Cloudflare's published ranges (<https://www.cloudflare.com/ips-v4>,
  <https://www.cloudflare.com/ips-v6>) and `CF-Connecting-IP` parses as an IP, set
  `RemoteIpAddress` to it. Otherwise leave the address alone, so a direct
  `azurewebsites.net` request can't spoof the header.
- Keep the ranges in configuration (for example `Cloudflare:IpRanges`), with today's list as the
  default. They rarely change.
- Replace the stale "Azure Front Door" comment on `UseForwardedHeaders`.
- Test it: a Cloudflare peer with the header is rewritten; a non-Cloudflare peer with the header
  is not; a malformed header is ignored.
- The change does nothing until the proxy is on, so it can ship any time before Phase 4.
- Document it in [bot-and-abuse-defense](../security/bot-and-abuse-defense.md).

**Move the Logic Apps off `www`** (Azure portal, any time). Change every Logic App in `m4d-Web`
(UpdatePlaylists, UpdateSongStats, UpdateSubscriptions, RefreshTopNPlaylists,
RefreshSeasonalPlaylists, and the disabled UpdateSpotify) to call
`https://msc4dnc.azurewebsites.net/...` instead of `https://www.music4dance.net/...`. Confirm the
next runs get a `200`. The app redirects only the `music4dance.net` apex to `www`, so the
`azurewebsites.net` host is served directly, and Phase 7's origin lock exempts `/api/*` and
`/playlist/updatebatch*` on that host. They authenticate with a token, not a host-bound
cookie, so the host shouldn't matter. Update the table in
[spotify-playlist-automation](../music-services/spotify-playlist-automation.md) and the
[runbook](../runbooks/spotify-from-search-maintenance.md).

### Phase 2: Create the Cloudflare account and zone (no user impact)

**Done 2026-11-05.** Every record reconciled with section 2 and set to DNS only. Step 4's
settings were applied 2026-11-06: **Custom SSL/TLS, Full (strict)** (switched from the
Automatic default) and **Always Use HTTPS** on, with the origin redirects kept; the other step 4
settings were confirmed the same day. Cloudflare Fonts
waits until after Phase 4, and Early Hints stays off.

1. Sign up at <https://dash.cloudflare.com/sign-up> with the email from Phase 0, verify it, and
   turn on two-factor authentication (**My Profile**, then **Authentication**). Save the backup
   codes in a password manager.
2. **Add a domain** (the "Onboard a domain" button on the account home): enter
   `music4dance.net` and keep **quick scan for DNS records** selected. Turn off any option to
   **block AI crawlers** that onboarding offers, since AI crawlers are allowed by the decision
   recorded in the bot-load plan (open question 1). Choose the **Free** plan.
3. **Reconcile the imported records** against section 2 and the Namecheap export, line by line:
   - Set **every** record to **DNS only** for now, including `@` and `www`.
   - Add anything the scan missed, usually the SRV records and some TXT records.
   - Check the MX priority and the exact TXT values. Quoting gets mangled sometimes.
   - TTL "Auto" is fine.
4. **Configure these settings now**, so they're already in place when the proxy goes on:
   - **SSL/TLS, Overview:** choose **Custom SSL/TLS**, then encryption mode **Full (strict)**.
     Don't leave it on **Automatic SSL/TLS** (Cloudflare's default for new zones):
     - Automatic moves up one mode at a time. It scans the origin about monthly, then shifts
       traffic to the new mode in 10% steps. So after Phase 4 the zone could spend weeks on
       **Full**, which encrypts but doesn't check the origin certificate.
     - We already know the origin supports Full (strict). Setting it outright means every proxied
       request checks the certificate from the first one, and a broken origin certificate shows
       up at once as a 526 error, not as a quiet step down.
     - Full (strict) works with today's App Service managed certificates, which are publicly
       trusted for both hostnames, and with the Origin CA certificate from Phase 5.
     - Never use **Flexible**: it sends plain HTTP to the origin, and the app's HTTPS redirect
       then loops.
   - **SSL/TLS, Edge Certificates:** **Always Use HTTPS** on, **Minimum TLS Version** 1.2. Leave
     Cloudflare's HSTS off, because the app already sends HSTS (`UseHsts`).
     - The dashboard warns that this can loop with an origin that also redirects to HTTPS. That
       happens only with **Flexible**. With Full (strict), Cloudflare always connects to the
       origin over HTTPS, so the app's `UseHttpsRedirection` / `AddRedirectToHttps` and App
       Service **HTTPS Only** never fire for proxied requests. **Keep the origin redirects**: they
       still protect the `azurewebsites.net` host and anything that reaches the origin directly.
   - **Security, Bots:** **Bot Fight Mode off** for now (turned on in Phase 6).
   - **Speed** (or **Optimization**): **Rocket Loader** off. Leave any automatic Web Analytics
     beacon injection off; the site already has GA4.
     - **Cloudflare Fonts:** worth turning on, but **after Phase 4 is stable**, so the cutover
       changes one thing at a time. It rewrites the Google Fonts stylesheet link in
       `m4d/Views/Shared/_head.cshtml` (Source Sans Pro and Merienda) to serve the fonts from
       `music4dance.net`. That saves the connections to `fonts.googleapis.com` and
       `fonts.gstatic.com` and stops sending visitors' IPs to Google for fonts. The app sets no
       Content-Security-Policy, so nothing blocks the rewritten URLs. Once it's on, drop the two
       `preconnect` links above it. Check a page's fonts and the browser console afterwards.
     - **Early Hints:** leave it off. Cloudflare sends `103 Early Hints` only from `Link`
       preload headers on origin responses, and the app sends none (Vite's preloads are tags in
       the HTML). It would do nothing until the app adds those headers.
   - **Scrape Shield:** **Email Address Obfuscation** off. It rewrites emails in the HTML and
     could corrupt the inline JSON page models.
   - **Caching, Configuration:** leave the defaults (static files by extension, respecting the
     origin's `Cache-Control`). **Browser Cache TTL:** "Respect existing headers".
5. Note the two nameservers Cloudflare assigns. This zone got `algin.ns.cloudflare.com` (spelled
   `algin`, not `align`) and `hazel.ns.cloudflare.com`.

### Phase 3: Move the nameservers at Namecheap (DNS only, so no traffic change)

**Done 2026-11-05.** The zone is Active, every check in step 3 passed, and the edge certificate
is Active.

Pick a quiet time. Nothing should visibly change for users.

1. Namecheap **Domain List**, then **Manage** next to `music4dance.net`. In the **Nameservers**
   row, change **Namecheap BasicDNS** to **Custom DNS**, enter the two Cloudflare nameservers,
   and save with the green check mark.
   - Cloudflare's copy becomes the live DNS, which is why Phase 2's reconcile matters.
     Namecheap's own servers (`dns1.registrar-servers.com`) kept answering with the old records
     afterwards, so resolvers that still had the old NS cached got the same answers. Edits made
     at Namecheap after this point are ignored.
   - The registration, auto-renew and registrar lock stay at Namecheap. Keep auto-renew on.
2. In Cloudflare, use **Check nameservers** on the zone overview. Activation usually takes
   minutes to a few hours. The `.net` registry caches nameserver changes for up to 48 hours, and
   resolvers switch over gradually (on the day, 8.8.8.8 had switched while 1.1.1.1 still had
   Namecheap cached). Both providers serve the same answers in the meantime, so that's harmless.
   Cloudflare emails when the zone is **Active**.
3. **Verify.**
   - `Resolve-DnsName music4dance.net -Type NS -Server 8.8.8.8` shows the Cloudflare
     nameservers.
   - Run the section 2 query list against each Cloudflare nameserver directly
     (`-Server algin.ns.cloudflare.com`, `-Server hazel.ns.cloudflare.com`) and against
     `dns1.registrar-servers.com`, and diff them. `Resolve-DnsName` can't query CAA, so check
     that one in the dashboard. Done 2026-11-05: no differences.
   - Send and receive a test email on a Microsoft 365 mailbox.
   - Trigger an ACS email (a password reset) and check that the received message shows DKIM
     `pass`. Use a Gmail address and **Show original**: the outer `Authentication-Results`
     header should show `dkim=pass header.i=@music4dance.net
     header.s=selector1-azurecomm-prod-net` and `spf=pass`. (The `ARC-Authentication-Results`
     from Microsoft's internal hop shows `dkim=none`; that's expected.) Passed 2026-11-05.
   - Search Console still shows the property as verified.
4. **Wait** until **SSL/TLS, Edge Certificates** shows the Universal certificate covering
   `music4dance.net, *.music4dance.net` as **Active**. Proxying before that serves TLS errors.

**Rollback:** set the Namecheap nameservers back to **Namecheap BasicDNS**. Re-enter records from
the Phase 0 export if Namecheap didn't keep them.

### Phase 4: Turn the proxy on

Prerequisites: the Phase 1 PR is deployed, the zone is Active, and the edge certificate is
Active. All of Phase 2 step 4 is set (confirmed 2026-11-06): Custom SSL/TLS, Full (strict);
Always Use HTTPS on; Minimum TLS 1.2; Cloudflare HSTS off; Bot Fight Mode off; Rocket Loader
off; Email Address Obfuscation off.

Proxy only `@` and `www`. A proxied record's TTL is fixed at Auto (5 minutes), so there's no
TTL to set on them. The DNS-only records keep their own TTLs.

1. In **DNS, Records**, switch `www` to **Proxied**. Wait 10 minutes and check:
   - `curl -sI https://www.music4dance.net/` shows `server: cloudflare` and a `cf-ray` header.
   - The site works signed out and signed in. Check search, a song page, sign-in and sign-out,
     and a Spotify connect if convenient.
   - `/Admin/Diagnostics` shows real visitor IPs, not `104.x`/`172.64.x`/`162.158.x` Cloudflare
     addresses.
   - A Stripe checkout return lands correctly (test mode, or watch the next real purchase).
2. Switch `@` to **Proxied**. Check that `curl -sI https://music4dance.net/some/path` still
   308-redirects to `www` with the path kept.
3. Watch for a day: App Service logs (`analyze-logs` skill), the Cloudflare **Analytics** tab,
   and the next Logic App runs.

**Rollback:** switch `@` and `www` back to **DNS only**. It takes effect within minutes (the
proxied TTL is 5 minutes) and goes straight back to today's path, with no nameserver change.

### Phase 5: Origin certificate (within two weeks of Phase 4, before managed-cert renewal)

Azure renews a managed certificate only while the hostname resolves to App Service. Behind the
proxy it resolves to Cloudflare, so renewals would fail silently at some point. Replace the
managed certificates with a Cloudflare Origin CA certificate.

1. Cloudflare **SSL/TLS, Origin Server, Create Certificate**: "Generate private key and CSR
   with Cloudflare", key type RSA (2048), hostnames `music4dance.net` and `*.music4dance.net`,
   validity 15 years. Copy the certificate and the private key to two local files
   (`origin.pem`, `origin.key`) in `local/`. **The key is shown only once.**
2. Convert them to PFX (Git Bash has `openssl`):
   `openssl pkcs12 -export -out local/origin.pfx -inkey local/origin.key -in local/origin.pem`,
   and set an export password.
3. App Service `msc4dnc`, **Certificates, Bring your own certificates (.pfx), Add certificate**:
   upload `origin.pfx` with its password.
4. **Custom domains:** for `www.music4dance.net` and then `music4dance.net`, **Add binding**,
   choose the Origin certificate, SNI SSL. Each one replaces the managed-certificate binding.
5. Verify the site still loads through Cloudflare. Full (strict) accepts Origin CA
   certificates. Then delete the two App Service managed certificates so they don't send
   failed-renewal noise.
6. Store `origin.pfx` and its password in the password manager, then delete the `local/` copies.
   Optionally import the PFX into Key Vault `music4dance` as a certificate, as a backup.

After this, anyone who goes to the custom hostnames directly at the origin gets a certificate
error, because Origin CA is trusted only by Cloudflare. That's intended. `msc4dnc.azurewebsites.net`
keeps Microsoft's own certificate and isn't affected.

### Phase 6: Turn on bot protection and observe

1. **Security, Bots: Bot Fight Mode on.** This is the "defer to Cloudflare" step. Afterwards:
   - Watch **Security, Events** daily for a week. Cloudflare logs every challenge and block
     there.
   - Check Search Console **Crawl stats** for a drop in Googlebot requests or a rise in errors.
   - Confirm the Logic Apps still succeed. They should, since Phase 1 moved them off `www`.
   - Rerun `analyze-traffic` and compare server time with the 2026-10-31 numbers in the bot-load
     plan.
2. **One custom WAF rule** for scanner noise: block the probe paths that the 4xx triage keeps
   seeing (for example URI path ends with `.php`, or starts with `/wp-`, `/.env` or `/.git`).
   Take the list from [runbooks/triage-4xx](../runbooks/triage-4xx.md). This keeps them off the
   origin and out of the 4xx tracker.
3. **Keep the Free plan's one rate-limiting rule spare.** The app's own identity rate limits and
   `CrawlerThrottle` already cover login and crawlers.
4. **Update [runbooks/respond-to-attack](../runbooks/respond-to-attack.md)** with the edge
   controls: Security level **I'm Under Attack** (a challenge for every visitor, one switch), a
   temporary custom rule to challenge a country, ASN or user agent, and **Development Mode**
   (bypasses the cache for 3 hours) for troubleshooting.

If Bot Fight Mode blocks something that matters and can't be moved off `www`, turn it off
again. That's the trigger to consider Pro (section 1).

### Phase 7 (optional): Lock the origin

There are two ways around Cloudflare:

- **The default host.** Bots that know `msc4dnc.azurewebsites.net` can use it directly.
- **The custom domain, sent straight to the origin.** A client can skip the DNS lookup and
  connect to the origin address while still asking for `www`, for example
  `curl --resolve www.music4dance.net:443:40.82.255.134 https://www.music4dance.net/`. App
  Service serves it because the hostname is bound there. The origin address is easy to find: look
  up `msc4dnc.azurewebsites.net`, or check DNS-history services for where `www` pointed before
  the move.

The bare IP with no matching `Host` gets Azure's generic 404, so it isn't a third way in.

Don't use App Service
access restrictions to stop that: they apply to every hostname, so they would also block the
Logic Apps, admin uploads and the pipeline's readiness check
(`azure-pipelines.yml` curls `https://$HOST/` on the default hostname).

Instead, mirror the Front Door plan's `X-Azure-FDID` idea:

- Add a Cloudflare **Transform Rule** (Rules, Transform Rules, modify request header; Free
  allows this) that sets a secret header, say `X-M4d-Edge: <random>`, on every proxied request.
- Add a middleware: requests to custom hostnames without the secret get a 403. Requests to
  `*.azurewebsites.net` that are anonymous and outside `/identity/*`, `/api/*`,
  `/playlist/updatebatch*` and `/health/*` get a 301 to `www`, so stray bots end up at the edge.
  Admins sign in on the `azurewebsites.net` host when they need it for uploads.
- Point the pipeline's readiness check at `/health/startup`, which returns 200 as soon as the
  process listens.

An alternative to the secret header is **Authenticated Origin Pulls**, which Free allows:
- Cloudflare presents its client certificate on every connection to the origin.
- App Service is set to request client certificates, with exclusion paths for the exempt
  routes above.
- The middleware checks the forwarded certificate (`X-ARR-ClientCert`) against Cloudflare's
  origin-pull CA.

It's stronger, because there's no shared secret to leak, but it takes more setup.

Decide on this after a few weeks of Phase 6 data. It's worth doing only if the HTTP logs show
meaningful traffic still reaching the origin outside Cloudflare. The `analyze-traffic` skill's
**Traffic by host** section measures both ways in. Each log record's `CsHost` gives the default
host. For the custom domains, a `c-ip` outside Cloudflare's published ranges means the request
went straight to the origin.

**Baseline before the cutover (2026-10-29 22:00 to 2026-11-04 19:00 UTC, 142 hours, 2.13M
requests):**

| Host | Requests | Pages | Server time | Notes |
| --- | ---: | ---: | ---: | --- |
| `www.music4dance.net` | 2,061,959 | 316,509 | 80.4 h | Everything else |
| `music4dance.net` | 59,453 | 53,031 | 3 min | Redirects to `www`; mostly SEO tools |
| `msc4dnc.azurewebsites.net` | 3,696 | 3,460 | 7 min | See below |
| `msc4dnc.scm.azurewebsites.net` | 29 | 2 | 5 min | Pipeline deploys and `az` |

- **Almost nobody uses the default host today:** 0.17% of requests, and 0.15% of server time.
  - Of its 3,696 requests, 3,427 were a single `curl` loop against `/` on 2026-11-03, from the
    maintainer's own network (the same /24 is signed in on `www`).
  - The rest, about 270 over six days, was:
    - Mobile browsers fetching only `/` with no referrer, from about 20 rotating IPs: a small
      headless farm.
    - Censys.
    - Probes for `/mcp` and `/sse` from a Python client.
    - The new Logic App calls, which started 2026-11-04.
  - None of it was crawling: apart from health and API calls, every page request was for `/`.
- **The direct-to-origin check has nothing to measure yet.** Only 2 of 2.12M custom-domain
  requests came from Cloudflare addresses (WARP or Workers clients), which is expected while
  DNS points at Azure.
- **After Phase 4, compare against this.** Expect:
  - `www` and the apex at close to 100% via Cloudflare. The exceptions are AlwaysOn, which
    calls `www` from inside Azure, and signed-in requests; the skill's detail leaves out both,
    along with the other platform callers.
  - The default host at the Logic Apps plus admin uploads.

  Growth of either back door past a few hundred anonymous page views a day is the signal to do
  this phase.

## 4. Follow-ups and docs

When the phases ship, fold the as-built result into the reference docs and delete this plan
(see [CONVENTIONS](../CONVENTIONS.md)):

- [hosting-and-identity](../infrastructure/hosting-and-identity.md): DNS at Cloudflare,
  registration at Namecheap, Origin CA certificate (15-year expiry date), SSL mode, the
  `azurewebsites.net` host as the back door for admin uploads and long requests.
- [bot-and-abuse-defense](../security/bot-and-abuse-defense.md): the client-IP middleware, Bot
  Fight Mode, the scanner WAF rule.
- `analyze-traffic` skill: after Phase 4, the App Service HTTP logs record Cloudflare's IP as
  `c-ip`, so the per-client grouping (IP + user agent) degrades. Options: have the app's
  HTTP logging include `CF-Connecting-IP`, or lean on Cloudflare's Security Events and Analytics.
  Decide before the next traffic review. The skill's per-host section (Phase 7 baseline) doesn't
  depend on this: it needs only the edge IP, which is what tells proxied and direct requests
  apart.
- [plans/front-door-caching](front-door-caching.md): mark it superseded, or delete it.
- [plans/bot-load-and-public-page-rendering](bot-load-and-public-page-rendering.md): close open
  question 3 (edge vendor) and point B3 here.
- **DNS cleanup, about a month after Phase 4 (early December 2026):**
  - Delete the legacy TXT record at `@` (`music4dance.azurewebsites.net`).
  - **Tighten DMARC.** `_dmarc` has been `v=DMARC1; p=none` (report only) since 2026-11-05.
    Turn on Cloudflare's **Email, DMARC Management** now so reports collect in the meantime.
    Once a few weeks of reports show only Microsoft 365 and ACS sending as `music4dance.net`,
    and both pass SPF or DKIM, change it to `v=DMARC1; p=quarantine`. After another quiet
    month, consider `p=reject`. If a legitimate sender shows up failing, fix its SPF or DKIM
    first.
  - Consider turning on DNSSEC in Cloudflare, then adding the DS record it gives you at
    Namecheap (**Advanced DNS, DNSSEC**).

## Related

- [attack-mitigation-phase2](attack-mitigation-phase2.md): the overall Phase 2 plan; this is step 2.2
- [bot-load-and-public-page-rendering](bot-load-and-public-page-rendering.md): the Front Door vs Cloudflare comparison and the traffic data
- [front-door-caching](front-door-caching.md): the alternative not taken
- [hosting-and-identity](../infrastructure/hosting-and-identity.md): App Service, custom domains, certificates
- [bot-and-abuse-defense](../security/bot-and-abuse-defense.md): rate limiting and crawler handling that depend on the client IP
- [runbooks/respond-to-attack](../runbooks/respond-to-attack.md): to gain the edge controls in Phase 6
