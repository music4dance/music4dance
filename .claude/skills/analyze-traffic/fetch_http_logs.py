"""Download the msc4dnc request logs from storage account m4dlogs.

Usage: python -I fetch_http_logs.py [--source platform|app] [--hours 24] [--dest DIR]

--source platform (default): the App Service HTTP log, container
insights-logs-appservicehttplogs, into local/http-logs. Behind Cloudflare its client IP
is the edge server.
--source app: the app's own request log (RequestLogMiddleware), container
app-request-logs, into local/app-request-logs. Its client IP is the visitor's.

Lists the hourly PT1H.json blobs and downloads the ones from the last --hours hours
that are missing locally or whose size changed (the current hour's blob is still being
appended to). Reads with your Entra ID sign-in (--auth-mode login), which needs Storage
Blob Data Reader on the account. Keep the two sources in separate directories: the
analyzer reads every PT1H.json under the one it's given.
"""

import argparse
import json
import re
import shutil
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone
from pathlib import Path

ACCOUNT = "m4dlogs"
SOURCES = {
    "platform": ("insights-logs-appservicehttplogs", "local/http-logs"),
    "app": ("app-request-logs", "local/app-request-logs"),
}
HOUR = re.compile(r"/y=(\d{4})/m=(\d{2})/d=(\d{2})/h=(\d{2})/")


def az(*args):
    exe = shutil.which("az") or shutil.which("az.cmd")
    if not exe:
        sys.exit("Azure CLI (az) not found")
    result = subprocess.run([exe, *args, "--account-name", ACCOUNT, "--auth-mode", "login"],
                            capture_output=True, text=True)
    if result.returncode != 0:
        sys.exit(f"az {' '.join(args[:3])} failed:\n{result.stderr.strip()}")
    return result.stdout


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--source", choices=SOURCES, default="platform")
    ap.add_argument("--hours", type=int, default=24, help="how far back to fetch (default 24)")
    ap.add_argument("--dest", help="default local/http-logs (platform) or local/app-request-logs (app)")
    args = ap.parse_args()
    container, default_dest = SOURCES[args.source]
    args.dest = args.dest or default_dest

    blobs = json.loads(az("storage", "blob", "list", "-c", container, "--num-results", "*",
                          "--query", "[].{name:name,size:properties.contentLength}", "-o", "json"))
    cutoff = datetime.now(timezone.utc) - timedelta(hours=args.hours + 1)
    wanted = []
    for b in blobs:
        m = HOUR.search(b["name"])
        if not m:
            continue
        hour = datetime(*map(int, m.groups()), tzinfo=timezone.utc)
        local = Path(args.dest) / b["name"]
        if hour >= cutoff and (not local.exists() or local.stat().st_size != b["size"]):
            wanted.append((b["name"], local))

    def fetch(item):
        name, local = item
        local.parent.mkdir(parents=True, exist_ok=True)
        az("storage", "blob", "download", "-c", container, "-n", name, "-f", str(local),
           "--overwrite", "true", "-o", "none")
        return name

    with ThreadPoolExecutor(max_workers=6) as pool:
        for name in pool.map(fetch, wanted):
            print("downloaded", HOUR.search(name).group(0).strip("/"))
    hours = sorted(HOUR.search(str(p).replace("\\", "/")).group(0)
                   for p in Path(args.dest).rglob("PT1H.json"))
    print(f"{len(wanted)} blob(s) downloaded; {len(hours)} hour(s) on disk"
          + (f", {hours[0].strip('/')} to {hours[-1].strip('/')}" if hours else ""))


if __name__ == "__main__":
    main()
