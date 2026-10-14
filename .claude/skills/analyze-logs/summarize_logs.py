"""Group music4dance App Service log entries into signatures and count them.

Usage: python -I summarize_logs.py <unzipped-log-dir> [--top N] [--examples]

Reads LogFiles/Application/diagnostics-*.txt (the AddAzureWebAppDiagnostics
provider: "<ts> +00:00 [Level] Category: message" followed by continuation
lines) and LogFiles/*_default_docker.log (container stdout/stderr) for
startup/crash lines that never went through ILogger.
"""

import argparse
import re
import sys
from collections import defaultdict
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


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("root")
    ap.add_argument("--top", type=int, default=60)
    ap.add_argument("--examples", action="store_true", help="print one full example per signature")
    args = ap.parse_args()

    root = Path(args.root)
    app_files = sorted(root.glob("**/LogFiles/Application/diagnostics-*.txt"))
    if not app_files:
        sys.exit(f"No diagnostics-*.txt under {root}")

    groups = defaultdict(lambda: {"count": 0, "first": None, "last": None, "example": None})
    for p in app_files:
        for e in read_entries(p):
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

    docker_files = sorted(root.glob("**/LogFiles/*_default_docker.log"))
    print("=" * 70)
    print(f"Container stdout/stderr: {', '.join(p.name for p in docker_files)}")
    hits = defaultdict(list)
    for p in docker_files:
        with p.open(encoding="utf-8", errors="replace") as f:
            for line in f:
                for pat in DOCKER_PATTERNS:
                    if pat in line:
                        hits[pat].append(line.rstrip())
    for pat in DOCKER_PATTERNS:
        lines = hits.get(pat, [])
        print(f"  {pat!r}: {len(lines)}")
        for line in lines[-3:]:
            print(f"      {line[:220]}")


if __name__ == "__main__":
    main()
