"""Download the msc4dnc request logs.

Usage: python -I fetch_http_logs.py [--source platform|app] [--from blob|home]
                                    [--hours 24] [--dest DIR]

--source platform (default): the App Service HTTP log. Lists the hourly PT1H.json blobs
in container insights-logs-appservicehttplogs and downloads the ones from the last
--hours hours that are missing locally or whose size changed (the current hour's blob
is still being appended to), into local/http-logs. Reads with your Entra ID sign-in
(--auth-mode login), which needs Storage Blob Data Reader on the account. Behind
Cloudflare its client IP is the edge server.

--source app: the app request log (the built-in W3C logger, RequestLog:Enabled). Lists
the daily requests-<instance>-YYYYMMDD.NNNN.txt files (one set per App Service instance;
the analyzer merges them) and downloads the ones from the last --hours hours that are
missing or whose size changed, into local/app-request-logs. Its client IP is the
visitor's. Where from:
  --from blob (default): container app-request-logs in m4dlogs, where the app copies the
    files every hour (RequestLog:UploadContainer) and keeps them 30 days. Same sign-in and
    role as the platform log. The blobs only grow, so a file that's already partly
    downloaded gets just its new bytes.
  --from home: /home/LogFiles/requests on the app itself, through Kudu's file API with
    your Entra ID sign-in. Up to the minute, but only about the last 2 days once they've
    been copied to blob storage. Downloads whole files.

Keep the two sources in separate directories; the analyzer reads one kind per directory.
"""

import argparse
import json
import re
import shutil
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone
from pathlib import Path

ACCOUNT = "m4dlogs"
RESOURCE_GROUP = "m4d-Web"
CONTAINER = "insights-logs-appservicehttplogs"
APP_CONTAINER = "app-request-logs"
KUDU_DIR = "https://msc4dnc.scm.azurewebsites.net/api/vfs/LogFiles/requests/"
HOUR = re.compile(r"/y=(\d{4})/m=(\d{2})/d=(\d{2})/h=(\d{2})/")
DAY_FILE = re.compile(r"^requests-(?:[a-z0-9]+-)?(\d{4})(\d{2})(\d{2})\.\d{4}\.txt$")


def run_az(args):
    exe = shutil.which("az") or shutil.which("az.cmd")
    if not exe:
        sys.exit("Azure CLI (az) not found")
    result = subprocess.run([exe, *args], capture_output=True, text=True)
    if result.returncode != 0:
        sys.exit(f"az {' '.join(args[:3])} failed:\n{result.stderr.strip()}")
    return result.stdout


def az(*args):
    return run_az([*args, "--account-name", ACCOUNT, "--auth-mode", "login"])


def fetch_platform(args):
    dest = Path(args.dest or "local/http-logs")
    blobs = json.loads(az("storage", "blob", "list", "-c", CONTAINER, "--num-results", "*",
                          "--query", "[].{name:name,size:properties.contentLength}", "-o", "json"))
    cutoff = datetime.now(timezone.utc) - timedelta(hours=args.hours + 1)
    wanted = []
    for b in blobs:
        m = HOUR.search(b["name"])
        if not m:
            continue
        hour = datetime(*map(int, m.groups()), tzinfo=timezone.utc)
        local = dest / b["name"]
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
                   for p in dest.rglob("PT1H.json"))
    print(f"{len(wanted)} blob(s) downloaded; {len(hours)} hour(s) on disk"
          + (f", {hours[0].strip('/')} to {hours[-1].strip('/')}" if hours else ""))


def wanted_day_files(files, dest, hours, now):
    """(name, local path, remote size) for the day files that cover the last `hours` hours
    and are missing locally or a different size. The logger names files by its local date,
    which is UTC on App Service."""
    first_day = (now - timedelta(hours=hours)).date()
    wanted = []
    for f in files:
        m = DAY_FILE.match(f["name"])
        if not m:
            continue
        day = datetime(*map(int, m.groups())).date()
        local = dest / f["name"]
        if day >= first_day and (not local.exists() or local.stat().st_size != f["size"]):
            wanted.append((f["name"], local, f["size"]))
    return wanted


def blob_source():
    """List and download from the app-request-logs container. The blobs are append-only, so
    when the local copy is shorter, only the new byte range is downloaded and appended."""
    files = json.loads(az("storage", "blob", "list", "-c", APP_CONTAINER, "--num-results", "*",
                          "--query", "[].{name:name,size:properties.contentLength}", "-o", "json"))

    def download(name, local, size):
        have = local.stat().st_size if local.exists() else 0
        if not 0 < have < size:
            az("storage", "blob", "download", "-c", APP_CONTAINER, "-n", name, "-f", str(local),
               "--overwrite", "true", "-o", "none")
            return
        part = local.with_name(local.name + ".part")
        az("storage", "blob", "download", "-c", APP_CONTAINER, "-n", name, "-f", str(part),
           "--start-range", str(have), "--end-range", str(size - 1), "--overwrite", "true",
           "-o", "none")
        with open(local, "ab") as out:
            out.write(part.read_bytes())
        part.unlink()
    return files, download


def home_source():
    """List and download from /home/LogFiles/requests through Kudu's file API, with your
    Entra ID sign-in (an ARM token)."""
    token = run_az(["account", "get-access-token", "--resource", "https://management.azure.com/",
                    "--query", "accessToken", "-o", "tsv"]).strip()

    def get(url):
        req = urllib.request.Request(url, headers={"Authorization": f"Bearer {token}"})
        with urllib.request.urlopen(req, timeout=300) as resp:
            return resp.read()
    try:
        listing = json.loads(get(KUDU_DIR))
    except urllib.error.HTTPError as e:
        if e.code == 404:
            sys.exit(f"{KUDU_DIR} not found: is RequestLog:Directory /home/LogFiles/requests?")
        raise
    files = [{"name": f["name"], "size": f["size"]} for f in listing
             if f.get("mime") != "inode/directory"]

    def download(name, local, size):
        local.write_bytes(get(KUDU_DIR + urllib.parse.quote(name)))
    return files, download


def fetch_app(args):
    dest = Path(args.dest or "local/app-request-logs")
    files, download = home_source() if args.source_from == "home" else blob_source()
    wanted = wanted_day_files(files, dest, args.hours, datetime.now(timezone.utc))
    dest.mkdir(parents=True, exist_ok=True)

    def fetch(item):
        name, local, size = item
        download(name, local, size)
        return name

    with ThreadPoolExecutor(max_workers=4) as pool:
        for name in pool.map(fetch, wanted):
            print("downloaded", name)
    days = sorted(p.name for p in dest.glob("requests-*.txt"))
    print(f"{len(wanted)} file(s) downloaded; {len(days)} file(s) on disk"
          + (f", {days[0]} to {days[-1]}" if days else ""))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--source", choices=["platform", "app"], default="platform")
    ap.add_argument("--from", dest="source_from", choices=["blob", "home"], default="blob",
                    help="app source only: the hourly copy in blob storage (default) or /home")
    ap.add_argument("--hours", type=int, default=24, help="how far back to fetch (default 24)")
    ap.add_argument("--dest", help="default local/http-logs (platform) or local/app-request-logs (app)")
    args = ap.parse_args()
    if args.source == "app":
        fetch_app(args)
    else:
        fetch_platform(args)


if __name__ == "__main__":
    main()
