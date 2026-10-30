"""Download the App Service HTTP request logs for msc4dnc from storage account m4dlogs.

Usage: python -I fetch_http_logs.py [--hours 24] [--dest local/http-logs]

Lists the hourly PT1H.json blobs in container insights-logs-appservicehttplogs and
downloads the ones from the last --hours hours that are missing locally or whose size
changed (the current hour's blob is still being appended to). Reads with your Entra ID
sign-in (--auth-mode login), which needs Storage Blob Data Reader on the account.
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
CONTAINER = "insights-logs-appservicehttplogs"
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
    ap.add_argument("--hours", type=int, default=24, help="how far back to fetch (default 24)")
    ap.add_argument("--dest", default="local/http-logs")
    args = ap.parse_args()

    blobs = json.loads(az("storage", "blob", "list", "-c", CONTAINER, "--num-results", "*",
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
        az("storage", "blob", "download", "-c", CONTAINER, "-n", name, "-f", str(local),
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
