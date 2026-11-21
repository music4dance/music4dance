"""Group music4dance App Service log entries into signatures and count them.

Usage: python -I summarize_logs.py local/logs/msc4dnc [--since T] [--until T]
                                  [--requests local/logs/requests] [--top N] [--examples]

Reads LogFiles/Application/diagnostics-*.txt (the AddAzureWebAppDiagnostics
provider: "<ts> +00:00 [Level] Category: message" followed by continuation
lines) and LogFiles/*_default_docker*.log (container stdout/stderr) for
startup/crash lines that never went through ILogger.

The folder keeps every download fetch_logs.py has merged into it, so pass --since
(and --until) to pick the window: UTC, "2026-11-19" or "2026-11-19T15:00".

When the app request log is on disk (--requests, default local/logs/requests), it also
lists the 5xx responses in the same window by status and path, with the visitor IPs
and user agents behind them, to match against the signatures.
"""

import argparse
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

ENTRY = re.compile(
    r"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\.\d+ [+-]\d{2}:\d{2} \[(\w+)\] ([^:]+): (.*)$"
)

# Replace variable parts of a message so similar entries group together.
NORMALIZERS = [
    (re.compile(r'Connection id "[^"]+", Request id "[^"]+"'), "Connection <kestrel-id>"),
    (re.compile(r"IdSample='.*?'\)"), "IdSample=<s>)"),
    (re.compile(r"boundary=[-\w]+"), "boundary=<b>"),
    (re.compile(r"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", re.I), "<guid>"),
    (re.compile(r"https?://\S+"), "<url>"),
    (re.compile(r"(?<=[ =])/[^\s,;\"']*"), "<path>"),
    (re.compile(r"\b\d+(\.\d+)?(ms|s)\b"), "<dur>"),
    (re.compile(r"\(\d+\.\d+s\)"), "(<dur>)"),
    (re.compile(r"\b\d{1,3}(\.\d{1,3}){3}\b"), "<ip>"),
    (re.compile(r"\b[0-9a-f]{16,}\b", re.I), "<hex>"),
    (re.compile(r"\b\d+\b"), "<n>"),
]

DOCKER_PATTERNS = [
    "Unhandled exception",
    "=== music4dance.net Service Health Report ===",
    "is now unavailable",
    "remains unavailable",
    "fail:",
    "crit:",
    "OutOfMemory",
    "exited",
    "Application is shutting down",
]


DOCKER_TS = re.compile(r"^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})")
EXCEPTION_LINE = re.compile(r"^\s*([\w.]+(?:Exception|Error))\b:?\s*(.*)$")
APP_FRAME = re.compile(r"^\s*at ((?:m4d|m4dModels|DanceLib)\.[^(]+)")


def normalize(msg):
    for rx, repl in NORMALIZERS:
        msg = rx.sub(repl, msg)
    return msg[:200]


def signature(entry):
    """Message signature, plus exception type/message and first app frame when present."""
    sig = normalize(entry["msg"])
    exc = next((m for m in map(EXCEPTION_LINE.match, entry["extra"]) if m), None)
    if exc:
        sig += f"\n    -> {exc.group(1)}: {normalize(exc.group(2))[:160]}"
        frame = next((m.group(1) for m in map(APP_FRAME.match, entry["extra"]) if m), None)
        if frame:
            sig += f"\n    @  {frame}"
    return sig


def read_entries(path):
    current = None
    with path.open(encoding="utf-8", errors="replace") as f:
        for line in f:
            line = line.rstrip("\n")
            m = ENTRY.match(line)
            if m:
                if current:
                    yield current
                ts, level, category, msg = m.groups()
                current = {"ts": ts, "level": level, "category": category, "msg": msg, "extra": []}
            elif current is not None:
                current["extra"].append(line)
    if current:
        yield current


def window_bound(value):
    """'2026-11-19' or '2026-11-19T15:00' as the 'YYYY-MM-DD HH:MM:SS' the logs use."""
    if not value:
        return None
    value = value.replace("T", " ")
    return value + " 00:00:00"[len(value) - 10:] if len(value) < 19 else value[:19]


def in_window(ts, since, until):
    return (not since or ts >= since) and (not until or ts < until)


def day_in_window(name, since, until):
    """Whether a file named for its day (diagnostics-20261119.txt,
    requests-x-20261119.0000.txt, 2026_11_19_..._docker.log) can hold entries in the window.
    Files without a date in the name are always read."""
    m = re.search(r"(\d{4})[_-]?(\d{2})[_-]?(\d{2})", name)
    if not m:
        return True
    day = "-".join(m.groups())
    return (not since or day >= since[:10]) and (not until or day <= until[:10])


def request_errors(req_dir, since, until, top):
    """The app request log's 5xx responses in the window, grouped by status and path, with
    the visitors (behind Cloudflare, X-M4d-Log-Client is the real client IP)."""
    files = [p for p in sorted(Path(req_dir).glob("requests-*.txt")) if day_in_window(p.name, since, until)]
    print("=" * 70)
    if not files:
        print(f"App request log: no requests-*.txt for the window under {req_dir}")
        return
    groups = defaultdict(lambda: {"count": 0, "first": None, "last": None,
                                  "ips": Counter(), "agents": Counter()})
    total = 0
    for p in files:
        names = []
        with p.open(encoding="utf-8", errors="replace") as f:
            for line in f:
                if line.startswith("#Fields:"):
                    names = line[len("#Fields:"):].split()
                    continue
                if line.startswith("#") or not names:
                    continue
                row = dict(zip(names, line.rstrip("\n").split(" ")))
                ts = f"{row.get('date', '')} {row.get('time', '')}"
                if not in_window(ts, since, until):
                    continue
                total += 1
                status = row.get("sc-status", "")
                if not status.startswith("5"):
                    continue
                g = groups[(status, row.get("cs-method", ""), row.get("cs-uri-stem", ""))]
                g["count"] += 1
                g["first"] = g["first"] or ts
                g["last"] = ts
                g["ips"][row.get("cs(X-M4d-Log-Client)", "-")] += 1
                g["agents"][row.get("cs(User-Agent)", "-").replace("+", " ")[:80]] += 1
    print(f"App request log: {', '.join(p.name for p in files)}")
    print(f"{total} requests in the window, {sum(g['count'] for g in groups.values())} with a 5xx")
    for (status, method, stem), g in sorted(groups.items(), key=lambda kv: -kv[1]["count"])[:top]:
        ips = ", ".join(f"{ip} x{n}" for ip, n in g["ips"].most_common(3))
        agent, n = g["agents"].most_common(1)[0]
        print(f"  {status} x{g['count']}  {g['first']} .. {g['last']}  {method} {stem}")
        print(f"      {len(g['ips'])} client(s): {ips}; top agent (x{n}): {agent}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("root", help="the merged App Service log folder, e.g. local/logs/msc4dnc")
    ap.add_argument("--since", help="UTC, e.g. 2026-11-19 or 2026-11-19T15:00")
    ap.add_argument("--until", help="UTC, exclusive")
    ap.add_argument("--requests", default="local/logs/requests",
                    help="the app request log folder (default local/logs/requests); '' to skip")
    ap.add_argument("--top", type=int, default=60)
    ap.add_argument("--examples", action="store_true", help="print one full example per signature")
    args = ap.parse_args()

    root = Path(args.root)
    since, until = window_bound(args.since), window_bound(args.until)
    app_files = [p for p in sorted(root.glob("**/LogFiles/Application/diagnostics-*.txt"))
                 if day_in_window(p.name, since, until)]
    if not app_files:
        sys.exit(f"No diagnostics-*.txt for the window under {root}")

    groups = defaultdict(lambda: {"count": 0, "first": None, "last": None, "example": None})
    for p in app_files:
        for e in read_entries(p):
            if not in_window(e["ts"], since, until):
                continue
            key = (e["level"], e["category"], signature(e))
            g = groups[key]
            g["count"] += 1
            g["first"] = g["first"] or e["ts"]
            g["last"] = e["ts"]
            if g["example"] is None or (e["extra"] and not g["example"]["extra"]):
                g["example"] = e

    print(f"Application logs: {', '.join(p.name for p in app_files)}")
    total = sum(g["count"] for g in groups.values())
    by_level = defaultdict(int)
    for (level, _, _), g in groups.items():
        by_level[level] += g["count"]
    print(f"{total} entries, {len(groups)} signatures; " + ", ".join(f"{k}={v}" for k, v in sorted(by_level.items())))
    print()

    rank = {"Critical": 0, "Error": 1, "Warning": 2}
    ordered = sorted(groups.items(), key=lambda kv: (rank.get(kv[0][0], 3), -kv[1]["count"]))
    for (level, category, sig), g in ordered[: args.top]:
        print(f"[{level}] x{g['count']}  {g['first']} .. {g['last']}  {category}")
        print(f"    {sig}")
        if args.examples:
            ex = g["example"]
            for extra in ex["extra"][:25]:
                if extra.strip():
                    print(f"      | {extra}")
        print()

    docker_files = [p for p in sorted(root.glob("**/LogFiles/*_default_docker*.log"))
                    if day_in_window(p.name, since, until)]
    print("=" * 70)
    print(f"Container stdout/stderr: {', '.join(p.name for p in docker_files)}")
    hits = defaultdict(list)
    for p in docker_files:
        with p.open(encoding="utf-8", errors="replace") as f:
            for line in f:
                ts = DOCKER_TS.match(line)
                if ts and not in_window(ts.group(1).replace("T", " "), since, until):
                    continue
                for pat in DOCKER_PATTERNS:
                    if pat in line:
                        hits[pat].append(line.rstrip())
    for pat in DOCKER_PATTERNS:
        lines = hits.get(pat, [])
        print(f"  {pat!r}: {len(lines)}")
        for line in lines[-3:]:
            print(f"      {line[:220]}")

    if args.requests and Path(args.requests).is_dir():
        request_errors(args.requests, since, until, args.top)


if __name__ == "__main__":
    main()
