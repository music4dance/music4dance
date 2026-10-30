"""Summarize who is sending traffic to music4dance from the App Service HTTP logs.

Usage: python -I analyze_http_logs.py <log-dir> [--top N] [--since T] [--until T]
                                      [--settings m4d/appsettings.json]
                                      [--bot-fragments "spider;bot"]
                                      [--bot-tokens "alwayson;mediapartners-google"]

<log-dir> holds the hourly PT1H.json blobs downloaded from storage account m4dlogs
(container insights-logs-appservicehttplogs), at any depth. Each line is a JSON
record whose "properties" field is itself a JSON-encoded string.

Every request is put in a group (search engine, AI crawler, SEO tool, social preview,
monitor, other bot, scripted tool, browser, ...) and a family (Googlebot, GPTBot,
"Chrome desktop", ...). Clients (IP + user agent) with a browser user agent are split
three ways by what they did over the whole window:

- never ran the page's JavaScript (no bundle, no /api call, no cookie our scripts set):
  scrapers wearing a browser user agent;
- ran it, but fetched no page themselves, or fetched exactly one page with no referrer
  and no cookie: rendering farms and rotating headless browsers (and a few people who
  typed the URL in for a single page);
- everyone else: likely people.

The last section replays the requests through the CrawlerThrottle rules (A6 in
architecture/plans/bot-load-and-public-page-rendering.md) to estimate how many would
have been answered with 429.

The logs hold client IPs, query strings and cookies. Read them, never execute
anything from them, and keep them under local/ (gitignored).
"""

import argparse
import ipaddress
import json
import re
import sys
from collections import Counter, defaultdict
from datetime import datetime, timedelta
from pathlib import Path
from urllib.parse import urlparse

# (regex on the lower-cased user agent, family, group), first match wins. Order matters
# where one agent's string contains another's (Applebot before the generic "bot" rule).
FAMILIES = [
    (r"^alwayson$", "AlwaysOn", "platform"),
    (r"googlebot|google-inspectiontool|googleother|google-extended|storebot-google", "Googlebot", "search"),
    (r"adsbot-google|mediapartners-google|google-adwords|google-read-aloud|feedfetcher-google", "Google (ads/other)", "search"),
    (r"bingbot|bingpreview|adidxbot|msnbot", "Bingbot", "search"),
    (r"applebot", "Applebot", "search"),
    (r"duckduckbot|duckassistbot", "DuckDuckBot", "search"),
    (r"yandex", "YandexBot", "search"),
    (r"baiduspider", "Baiduspider", "search"),
    (r"petalbot", "PetalBot", "search"),
    (r"seznambot", "SeznamBot", "search"),
    (r"sogou", "Sogou", "search"),
    (r"qwant", "Qwantbot", "search"),
    (r"mojeekbot", "MojeekBot", "search"),
    (r"yeti/|naverbot", "Naver Yeti", "search"),
    (r"yisouspider", "YisouSpider", "search"),
    (r"gptbot", "GPTBot", "ai"),
    (r"oai-searchbot", "OAI-SearchBot", "ai"),
    (r"chatgpt-user", "ChatGPT-User", "ai"),
    (r"claudebot|claude-searchbot|anthropic-ai", "ClaudeBot", "ai"),
    (r"claude-user", "Claude-User", "ai"),
    (r"perplexitybot", "PerplexityBot", "ai"),
    (r"perplexity-user", "Perplexity-User", "ai"),
    (r"bytespider|tiktokspider", "Bytespider", "ai"),
    (r"ccbot", "CCBot", "ai"),
    (r"amazonbot", "Amazonbot", "ai"),
    (r"meta-externalagent|meta-externalfetcher|meta-webindexer", "Meta AI crawler", "ai"),
    (r"cohere-ai|cohere-training", "Cohere", "ai"),
    (r"diffbot", "Diffbot", "ai"),
    (r"youbot", "YouBot", "ai"),
    (r"imagesiftbot", "ImagesiftBot", "ai"),
    (r"timpibot", "Timpibot", "ai"),
    (r"ai2bot", "AI2Bot", "ai"),
    (r"pangubot", "PanguBot", "ai"),
    (r"mistralai-user", "MistralAI-User", "ai"),
    (r"reflectionbot", "Reflectionbot", "ai"),
    (r"exasearchbot|exabot|crawler\.exa\.ai", "Exa", "ai"),
    (r"elevenlabscrawler", "ElevenLabs", "ai"),
    (r"ahrefs", "AhrefsBot", "seo"),
    (r"semrush|siteauditbot", "SemrushBot", "seo"),
    (r"mj12bot", "MJ12bot", "seo"),
    (r"dotbot|rogerbot", "Moz (DotBot)", "seo"),
    (r"dataforseo", "DataForSeoBot", "seo"),
    (r"blexbot", "BLEXBot", "seo"),
    (r"barkrowler", "Barkrowler", "seo"),
    (r"seekport", "SeekportBot", "seo"),
    (r"serpstat", "serpstatbot", "seo"),
    (r"screaming frog", "Screaming Frog", "seo"),
    (r"awariobot|awariosmartbot", "AwarioBot", "seo"),
    (r"megaindex", "MegaIndex", "seo"),
    (r"zoominfobot", "ZoominfoBot", "seo"),
    (r"sidetrade", "Sidetrade", "seo"),
    (r"facebookexternalhit|facebookcatalog", "facebookexternalhit", "social"),
    (r"twitterbot", "Twitterbot", "social"),
    (r"linkedinbot", "LinkedInBot", "social"),
    (r"slackbot|slack-imgproxy", "Slackbot", "social"),
    (r"discordbot", "Discordbot", "social"),
    (r"whatsapp", "WhatsApp", "social"),
    (r"telegrambot", "TelegramBot", "social"),
    (r"pinterest", "Pinterest", "social"),
    (r"redditbot", "redditbot", "social"),
    (r"embedly|iframely|skypeuripreview", "Link preview", "social"),
    (r"uptimerobot|pingdom|statuscake|site24x7|betteruptime|better stack|freshping|hetrixtools", "Uptime monitor", "monitor"),
    (r"zgrab|masscan|nmap|nuclei|censys|expanse|internetmeasurement|paloalto|l9explore|odin\.io|shodan", "Scanner", "scanner"),
    (r"python-requests|python-urllib|aiohttp|httpx|python/", "Python", "tool"),
    (r"^curl/", "curl", "tool"),
    (r"^wget/", "Wget", "tool"),
    (r"go-http-client|^go ", "Go", "tool"),
    (r"^java/|okhttp|apache-httpclient", "Java", "tool"),
    (r"node-fetch|axios/|undici|^node", "Node", "tool"),
    (r"libwww-perl|^perl", "Perl", "tool"),
    (r"scrapy", "Scrapy", "tool"),
    (r"headlesschrome|phantomjs|puppeteer|playwright|selenium", "Headless browser", "tool"),
]
FAMILY_RES = [(re.compile(p), f, g) for p, f, g in FAMILIES]
GENERIC_BOT = re.compile(r"([a-z0-9][a-z0-9_.\-]*(?:bot|spider|crawler)[a-z0-9_.\-]*)")

GROUP_LABELS = {
    "platform": "Platform (AlwaysOn)",
    "search": "Search engines",
    "ai": "AI crawlers/fetchers",
    "seo": "SEO/marketing tools",
    "social": "Social link previews",
    "monitor": "Uptime monitors",
    "scanner": "Scanners",
    "bot": "Other self-declared bots",
    "tool": "Scripts/HTTP libraries",
    "empty": "Empty user agent",
    "human": "People (likely)",
    "headless": "Browser UA, headless pattern",
    "nojs": "Browser UA, never ran JS",
}
GROUP_ORDER = list(GROUP_LABELS)

ASSET = re.compile(
    r"^/(vclient|vendor|lib|images|img|content|fonts)/|"
    r"\.(js|mjs|css|png|ico|svg|jpe?g|gif|webp|avif|woff2?|ttf|map|txt|xml|pdf|json|csv|webmanifest)$",
    re.I,
)
JS_COOKIES = ("_ga", "cookieconsent_status")
AUTH_COOKIE = re.compile(r"(?:^|;\s*)music4dance[:=]")

SEARCH_REFERRERS = [
    (r"(^|\.)google\.", "Google"), (r"(^|\.)bing\.com$", "Bing"), (r"duckduckgo\.com$", "DuckDuckGo"),
    (r"(^|\.)yahoo\.", "Yahoo"), (r"ecosia\.org$", "Ecosia"), (r"yandex\.", "Yandex"),
    (r"baidu\.com$", "Baidu"), (r"search\.brave\.com$", "Brave Search"), (r"qwant\.com$", "Qwant"),
    (r"chatgpt\.com$|openai\.com$", "ChatGPT"), (r"perplexity\.ai$", "Perplexity"),
    (r"claude\.ai$", "Claude"), (r"gemini\.google\.com$", "Gemini"), (r"copilot\.microsoft\.com$", "Copilot"),
    (r"facebook\.com$|fb\.com$", "Facebook"), (r"instagram\.com$", "Instagram"),
    (r"youtube\.com$", "YouTube"), (r"reddit\.com$", "Reddit"), (r"pinterest\.", "Pinterest"),
    (r"^t\.co$|twitter\.com$|x\.com$", "X/Twitter"), (r"music4dance\.(net|blog)$", None),
]
REFERRER_RES = [(re.compile(p), n) for p, n in SEARCH_REFERRERS]


def classify_agent(ua):
    """Return (family, group) for a user agent, before the JavaScript split."""
    low = ua.lower().strip()
    if not low or low == "-":
        return "(empty)", "empty"
    for rx, family, group in FAMILY_RES:
        if rx.search(low):
            return family, group
    m = GENERIC_BOT.search(low)
    if m:
        return m.group(1)[:40], "bot"
    if "mozilla/" not in low and "opera" not in low:
        return ua[:40], "tool"
    return browser_family(low), "browser"


def browser_family(low):
    device = "mobile" if ("mobi" in low or "android" in low or "iphone" in low) else "desktop"
    for token, name in (("edg/", "Edge"), ("opr/", "Opera"), ("samsungbrowser", "Samsung"),
                        ("firefox/", "Firefox"), ("crios/", "Chrome"), ("chrome/", "Chrome"),
                        ("safari/", "Safari")):
        if token in low:
            return f"{name} {device}"
    return f"Other {device}"


def referrer_source(referer):
    if not referer or referer == "-":
        return "(none/direct)"
    host = (urlparse(referer).hostname or "").lower()
    if host.startswith("www."):
        host = host[4:]
    for rx, name in REFERRER_RES:
        if rx.search(host):
            return name  # None for our own pages (internal navigation)
    return host or "(unparseable)"


def kind_of(stem):
    if stem.lower().startswith("/api/"):
        return "api"
    if ASSET.search(stem):
        return "asset"
    return "page"


def path_stem(stem, depth=2):
    parts = stem.lower().rstrip("/").split("/")
    out = "/".join(parts[: depth + 1]) or "/"
    return out + ("/*" if len(parts) > depth + 1 else "")


def subnet(ip):
    try:
        addr = ipaddress.ip_address(ip)
    except ValueError:
        return ip
    bits = 24 if addr.version == 4 else 48
    return str(ipaddress.ip_network(f"{ip}/{bits}", strict=False))


def load(log_dir, since, until):
    rows = []
    files = sorted(Path(log_dir).rglob("PT1H.json"))
    if not files:
        sys.exit(f"No PT1H.json files under {log_dir}")
    intern = {}
    for path in files:
        with open(path, encoding="utf-8") as fh:
            for line in fh:
                line = line.strip()
                if not line:
                    continue
                try:
                    rec = json.loads(line)
                    p = json.loads(rec["properties"])
                except (ValueError, KeyError):
                    continue
                t = rec.get("time", "")[:19]
                if (since and t < since) or (until and t >= until):
                    continue
                ua = p.get("UserAgent") or ""
                ua = intern.setdefault(ua, ua)
                cookie = p.get("Cookie") or ""
                rows.append((
                    t,
                    p.get("CIp") or "",
                    ua,
                    p.get("CsMethod") or "",
                    p.get("CsUriStem") or "",
                    p.get("CsUriQuery") or "",
                    str(p.get("ScStatus") or ""),
                    int(p.get("TimeTaken") or 0),
                    int(p.get("ScBytes") or 0),
                    p.get("Referer") or "",
                    any(c in cookie for c in JS_COOKIES),
                    bool(AUTH_COOKIE.search(cookie)),
                ))
    rows.sort(key=lambda r: r[0])
    return files, rows


T, IP, UA, METHOD, STEM, QUERY, STATUS, TAKEN, BYTES, REFERER, JSCOOKIE, AUTH = range(12)


def pct(n, d):
    return f"{100 * n / d:5.1f}%" if d else "   - "


def heading(title):
    print(f"\n## {title}\n")


def table(headers, rows, aligns=None):
    """Print a Markdown table; numbers right-aligned."""
    aligns = aligns or ["l"] + ["r"] * (len(headers) - 1)
    print("| " + " | ".join(headers) + " |")
    print("| " + " | ".join("---:" if a == "r" else "---" for a in aligns) + " |")
    for r in rows:
        print("| " + " | ".join(str(c).replace("|", "\\|") for c in r) + " |")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("log_dir")
    ap.add_argument("--top", type=int, default=20)
    ap.add_argument("--since", help="UTC, e.g. 2026-10-30T00:00")
    ap.add_argument("--until", help="UTC, exclusive")
    ap.add_argument("--settings", default="m4d/appsettings.json",
                    help="appsettings.json to read CrawlerThrottle defaults from")
    ap.add_argument("--bot-fragments", default="spider;bot",
                    help="Configuration:BotFilter:ExcludeFragments from App Configuration")
    ap.add_argument("--bot-tokens", default="alwayson;mediapartners-google",
                    help="Configuration:BotFilter:ExcludeTokens from App Configuration")
    args = ap.parse_args()

    files, rows = load(args.log_dir, args.since, args.until)
    if not rows:
        sys.exit("No records in range")

    agent_class = {ua: classify_agent(ua) for ua in {r[UA] for r in rows}}
    kinds = [kind_of(r[STEM]) for r in rows]

    # Split browser-UA clients (IP + UA) by behavior over the whole window.
    stats = defaultdict(Counter)
    for r, k in zip(rows, kinds):
        if agent_class[r[UA]][1] != "browser":
            continue
        s = stats[(r[IP], r[UA])]
        s[k] += 1
        s["js"] += k != "page" or r[JSCOOKIE] or r[AUTH]
        s["cookie"] += r[JSCOOKIE] or r[AUTH]
        if k == "page" and referrer_source(r[REFERER]) not in (None, "(none/direct)"):
            s["extref"] += 1
    browser_group = {}
    for client, s in stats.items():
        if not s["js"]:
            browser_group[client] = "nojs"
        elif s["cookie"] or s["extref"] or s["page"] >= 2:
            browser_group[client] = "human"
        else:
            # Ran JS but fetched no page (HTML came from elsewhere: a rendering farm), or
            # exactly one page with no referrer and no cookie (rotating headless browsers).
            # Someone typing the URL in for a single page also lands here.
            browser_group[client] = "headless"

    def group_of(r):
        family, group = agent_class[r[UA]]
        if group == "browser":
            return family, browser_group[(r[IP], r[UA])]
        return family, group

    groups = [group_of(r) for r in rows]
    hours = {r[T][:13] for r in rows}
    total = len(rows)
    pages = sum(1 for k in kinds if k == "page")

    print("# music4dance traffic summary\n")
    print(f"- Window (UTC): {rows[0][T]} to {rows[-1][T]}, {len(hours)} hours, {len(files)} hourly files")
    print(f"- Requests: {total:,} ({total / max(len(hours), 1):,.0f}/hour); pages {pages:,}, "
          f"API {kinds.count('api'):,}, assets {kinds.count('asset'):,}")
    print(f"- Bytes out: {sum(r[BYTES] for r in rows) / 1e9:.2f} GB; "
          f"server time: {sum(r[TAKEN] for r in rows) / 3.6e6:.1f} hours (sum of TimeTaken)")
    print(f"- Distinct client IPs: {len({r[IP] for r in rows}):,}; signed-in requests: "
          f"{sum(1 for r in rows if r[AUTH]):,}")

    # --- By group ---------------------------------------------------------------
    g_req, g_page, g_bytes, g_ms, g_ips = Counter(), Counter(), Counter(), Counter(), defaultdict(set)
    for r, k, (fam, g) in zip(rows, kinds, groups):
        g_req[g] += 1
        g_bytes[g] += r[BYTES]
        g_ms[g] += r[TAKEN]
        g_ips[g].add(r[IP])
        if k == "page":
            g_page[g] += 1
    heading("Traffic by group")
    tb, tm = sum(g_bytes.values()), sum(g_ms.values())
    table(["Group", "Requests", "Pages", "% pages", "MB out", "% bytes", "Server h", "% server", "IPs"],
          [[GROUP_LABELS[g], f"{g_req[g]:,}", f"{g_page[g]:,}", pct(g_page[g], pages),
            f"{g_bytes[g] / 1e6:,.0f}", pct(g_bytes[g], tb), f"{g_ms[g] / 3.6e6:.2f}", pct(g_ms[g], tm),
            f"{len(g_ips[g]):,}"]
           for g in GROUP_ORDER if g_req[g]])

    # --- Top families -------------------------------------------------------------
    f_page, f_bytes, f_ms, f_ips, f_err, f_req = Counter(), Counter(), Counter(), defaultdict(set), Counter(), Counter()
    for r, k, (fam, g) in zip(rows, kinds, groups):
        key = (fam, g)
        f_req[key] += 1
        f_ms[key] += r[TAKEN]
        f_bytes[key] += r[BYTES]
        f_ips[key].add(r[IP])
        if k == "page":
            f_page[key] += 1
        if r[STATUS] >= "400":
            f_err[key] += 1
    heading(f"Top {args.top} agents by server time")
    ranked = sorted(f_req, key=lambda k: -f_ms[k])[: args.top]
    table(["Agent", "Group", "Requests", "Pages", "MB out", "Server min", "% server", "IPs", "4xx/5xx"],
          [[fam, GROUP_LABELS[g], f"{f_req[(fam, g)]:,}", f"{f_page[(fam, g)]:,}",
            f"{f_bytes[(fam, g)] / 1e6:,.1f}", f"{f_ms[(fam, g)] / 6e4:,.1f}", pct(f_ms[(fam, g)], tm),
            f"{len(f_ips[(fam, g)]):,}", f"{f_err[(fam, g)]:,}"]
           for fam, g in ranked])

    # --- Humans -------------------------------------------------------------------
    human = [(r, k) for r, k, (fam, g) in zip(rows, kinds, groups) if g == "human"]
    hpages = [r for r, k in human if k == "page" and r[METHOD] == "GET"]
    heading("People (ran JS, and came from a referrer, viewed 2+ pages, or has our cookies)")
    clients = {(r[IP], r[UA]) for r, _ in human}
    signed = {(r[IP], r[UA]) for r, _ in human if r[AUTH]}
    mobile = sum(1 for c in clients if "mobile" in agent_class[c[1]][0])
    print(f"- Clients (IP + user agent): {len(clients):,}, of which signed in {len(signed):,}; "
          f"mobile {pct(mobile, len(clients)).strip()}")
    print(f"- Page views: {len(hpages):,} ({len(hpages) / max(len(clients), 1):.1f} per client)\n")
    src = Counter(referrer_source(r[REFERER]) for r in hpages)
    src.pop(None, None)
    print("Where people arrive from (page views with an external or no referrer):\n")
    table(["Source", "Page views", "%"],
          [[s, f"{n:,}", pct(n, sum(src.values()))] for s, n in src.most_common(args.top)])
    print("\nWhat people view:\n")
    stems = Counter(path_stem(r[STEM]) for r in hpages)
    table(["Path", "Page views", "%"],
          [[s, f"{n:,}", pct(n, len(hpages))] for s, n in stems.most_common(args.top)])
    fams = Counter(agent_class[c[1]][0] for c in clients)
    print("\nBrowsers (clients): " + ", ".join(f"{f} {n:,}" for f, n in fams.most_common(8)))

    # --- Disguised clients ---------------------------------------------------------
    heading("Browser user agents that behave like automation")
    for g in ("headless", "nojs"):
        sel = [(r, k) for r, k, (fam, gg) in zip(rows, kinds, groups) if gg == g]
        if not sel:
            continue
        print(f"- {GROUP_LABELS[g]}: {len(sel):,} requests ({sum(1 for _, k in sel if k == 'page'):,} pages) "
              f"from {len({(r[IP], r[UA]) for r, _ in sel}):,} clients, {len({r[IP] for r, _ in sel}):,} IPs, "
              f"{len({subnet(r[IP]) for r, _ in sel}):,} subnets (/24, /48)")
        print("  - Paths: " + ", ".join(
            f"{s} {n:,}" for s, n in Counter(path_stem(r[STEM]) for r, k in sel if k == "page").most_common(8)))
        print("  - Claims: " + ", ".join(
            f"{u} {n:,}" for u, n in Counter(r[UA][:110] for r, _ in sel).most_common(3)))
    print()
    sn, sn_ips, sn_g = Counter(), defaultdict(set), defaultdict(Counter)
    for r, (fam, g) in zip(rows, groups):
        if g in ("headless", "nojs"):
            s = subnet(r[IP])
            sn[s] += 1
            sn_ips[s].add(r[IP])
            sn_g[s][f"{fam}, {GROUP_LABELS[g].split(', ')[-1]}"] += 1
    table(["Subnet", "Requests", "IPs", "Top claim"],
          [[s, f"{n:,}", len(sn_ips[s]), sn_g[s].most_common(1)[0][0]] for s, n in sn.most_common(args.top)])

    # --- Top IPs ------------------------------------------------------------------
    heading(f"Top {args.top} IPs by requests")
    ip_req, ip_ms, ip_fam = Counter(), Counter(), defaultdict(Counter)
    for r, (fam, g) in zip(rows, groups):
        ip_req[r[IP]] += 1
        ip_ms[r[IP]] += r[TAKEN]
        ip_fam[r[IP]][f"{fam} ({g})"] += 1
    table(["IP", "Requests", "Server min", "Agent (group)"],
          [[ip, f"{n:,}", f"{ip_ms[ip] / 6e4:.1f}", ip_fam[ip].most_common(1)[0][0]]
           for ip, n in ip_req.most_common(args.top)])

    # --- Paths by group --------------------------------------------------------------
    heading("Page paths by group (requests)")
    pg = defaultdict(Counter)
    for r, k, (fam, g) in zip(rows, kinds, groups):
        if k == "page":
            pg[path_stem(r[STEM])][g] += 1
    shown = [g for g in GROUP_ORDER if g_page[g]]
    top_paths = sorted(pg, key=lambda s: -sum(pg[s].values()))[: args.top]
    table(["Path", "Total"] + [GROUP_LABELS[g].split(" ")[0] for g in shown],
          [[s, f"{sum(pg[s].values()):,}"] + [f"{pg[s][g]:,}" if pg[s][g] else "" for g in shown]
           for s in top_paths])

    # --- Status codes and slow paths --------------------------------------------
    heading("Status codes by group")
    sc = defaultdict(Counter)
    for r, (fam, g) in zip(rows, groups):
        sc[g][r[STATUS]] += 1
    codes = sorted({c for g in sc for c in sc[g]})
    table(["Group"] + codes,
          [[GROUP_LABELS[g]] + [f"{sc[g][c]:,}" if sc[g][c] else "" for c in codes]
           for g in GROUP_ORDER if sc[g]])
    errs = Counter((r[STATUS], path_stem(r[STEM])) for r in rows if r[STATUS] >= "500")
    if errs:
        print("\n5xx by path: " + ", ".join(f"{s} {p} {n:,}" for (s, p), n in errs.most_common(10)))

    heading("Slowest page paths (server time)")
    times = defaultdict(list)
    for r, k in zip(rows, kinds):
        if k != "asset":
            times[path_stem(r[STEM])].append(r[TAKEN])
    def q(v, f):
        return v[min(len(v) - 1, int(len(v) * f))]
    slow = sorted(times, key=lambda s: -sum(times[s]))[: args.top]
    table(["Path", "Requests", "p50 ms", "p95 ms", "max ms", "Server min", "% server"],
          [[s, f"{len(v):,}", q(v, .5), q(v, .95), v[-1], f"{sum(v) / 6e4:,.1f}", pct(sum(v), tm)]
           for s in slow for v in [sorted(times[s])]])

    # --- Hourly -----------------------------------------------------------------
    heading("Requests per hour (UTC)")
    hourly = defaultdict(Counter)
    for r, (fam, g) in zip(rows, groups):
        hourly[r[T][:13]][g] += 1
    hg = [g for g in GROUP_ORDER if g_req[g]]
    table(["Hour", "Total"] + [GROUP_LABELS[g].split(" ")[0] for g in hg],
          [[h[5:].replace("T", " "), f"{sum(hourly[h].values()):,}"] + [f"{hourly[h][g]:,}" for g in hg]
           for h in sorted(hourly)])

    # --- CrawlerThrottle replay -------------------------------------------------
    throttle_replay(rows, groups, args)


def throttle_replay(rows, groups, args):
    """Mirror CrawlerThrottleMiddleware and CrawlerThrottleOptions.LimitFor: per lower-cased
    user agent that no exempt fragment matches and that either matches an AgentLimits entry
    or SpiderManager.IsSpider accepts, a window opens on the first request and lasts one
    minute; requests past the agent's limit in a window get 429. Signed-in requests aren't
    counted."""
    cfg = {"Enabled": True, "MaxRequestsPerMinute": 20,
           "ExemptFragments": "googlebot;google-inspectiontool;adsbot-google;mediapartners-google;"
                              "storebot-google;bingbot;applebot;duckduckbot",
           "PathPrefixes": "/song;/customsearch;/dances",
           "AgentLimits": "reflectionbot=3;barkrowler;meta-externalagent;meta-webindexer"}
    try:
        cfg.update(json.loads(Path(args.settings).read_text(encoding="utf-8-sig")).get("CrawlerThrottle", {}))
    except (OSError, ValueError):
        print(f"\n(Could not read {args.settings}; using built-in CrawlerThrottle defaults)")

    def split(s):
        return [x.strip().lower() for x in (s or "").split(";") if x.strip()]

    limit = int(cfg["MaxRequestsPerMinute"])
    exempt, prefixes = split(cfg["ExemptFragments"]), split(cfg["PathPrefixes"])
    fragments, tokens = split(args.bot_fragments), split(args.bot_tokens)

    agent_limits = []
    for entry in split(cfg.get("AgentLimits")):
        frag, _, n = entry.partition("=")
        agent_limits.append((frag.strip(), int(n) if n.strip().isdigit() else None))

    def is_spider(agent):
        return not agent.strip() or any(f in agent for f in fragments) or agent in tokens

    def limit_for(agent):
        if any(f in agent for f in exempt):
            return None
        for frag, n in agent_limits:
            if frag in agent:
                return n or limit
        return limit if is_spider(agent) else None

    windows = {}
    counted, throttled, by_fam, by_fam_counted = 0, 0, Counter(), Counter()
    escaped = Counter()
    for r, (fam, g) in zip(rows, groups):
        path = r[STEM].lower()
        if r[AUTH] or not any(path == p or path.startswith(p + "/") for p in prefixes):
            continue
        agent = r[UA].lower()
        agent_limit = limit_for(agent)
        if agent_limit is None:
            if not is_spider(agent) and g not in ("human", "nojs", "headless", "search"):
                escaped[fam] += 1
            continue
        counted += 1
        by_fam_counted[fam] += 1
        t = datetime.fromisoformat(r[T])
        w = windows.get(agent)
        if w is None or t >= w[0] + timedelta(minutes=1):
            w = windows[agent] = [t, 0]
        w[1] += 1
        if w[1] > agent_limit:
            throttled += 1
            by_fam[fam] += 1

    overrides = ", ".join(f"{f} {n or limit}" for f, n in agent_limits)
    heading(f"CrawlerThrottle replay ({limit}/min on {', '.join(prefixes)}"
            + (f"; per agent: {overrides}" if overrides else "") + ")")
    print(f"- Counted requests: {counted:,}; would get 429: {throttled:,} ({pct(throttled, counted).strip()})")
    print("- Real crawlers slow down after 429s, so this overstates the 429 count and "
          "understates the saving in later hours.\n")
    table(["Agent", "Counted", "Would 429", "%"],
          [[f, f"{by_fam_counted[f]:,}", f"{n:,}", pct(n, by_fam_counted[f])]
           for f, n in by_fam.most_common(args.top)])
    if escaped:
        print("\nAutomated agents on those paths that IsSpider doesn't recognize (not throttled): " +
              ", ".join(f"{f} {n:,}" for f, n in escaped.most_common(10)))


if __name__ == "__main__":
    main()
