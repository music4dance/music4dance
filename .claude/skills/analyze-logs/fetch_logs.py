"""Download the music4dance production logs into one shared tree, local/logs/.

Both the analyze-logs and analyze-traffic skills use this script, so a download made for
one is there for the other. Run `status` first: it shows what's on disk and how recent it
is, and every fetch only downloads what's missing or has grown.

Usage:
  python -I fetch_logs.py status
  python -I fetch_logs.py app-service [--app msc4dnc|m4d-test] [--max-age 30] [--force]
                                      [--zip ZIP_OR_FOLDER ...]
  python -I fetch_logs.py requests [--from blob|home] [--hours 24]
  python -I fetch_logs.py http [--hours 24]
  (--root DIR, before the command, moves the tree; default local/logs)

Layout under the root:
  <app>/        app-service: the App Service log download (`az webapp log download`),
                merged into one folder per app: LogFiles/Application/diagnostics-*.txt,
                LogFiles/*_docker.log, deployments/. Each download covers a rolling 2 to 3
                days; files from earlier downloads stay. A file is only replaced by a
                longer copy, so a later, shorter copy never truncates what's there.
  requests/     the app request log (RequestLog:Enabled, the built-in W3C logger), with the
                visitor's IP behind Cloudflare: requests-<instance>-YYYYMMDD.NNNN.txt.
                `requests` fetches it from container app-request-logs in m4dlogs (copied
                hourly, kept 30 days; only the new bytes of a file already here), or with
                --from home from /home/LogFiles/requests through Kudu (up to the minute, about
                the last 2 days). The app-service download includes /home/LogFiles/requests
                too, and those files land here as well, not under <app>/. All three are the
                same append-only bytes, so the longest copy of each file wins.
  http/         the App Service HTTP log (platform), hourly PT1H.json blobs from container
                insights-logs-appservicehttplogs. Behind Cloudflare its client IP is the edge
                server, but it's the only log with response bytes and requests that never
                reached the app.
  reports/      Markdown summaries the skills write (traffic-<date>.md).
  .fetched.json when each source was last fetched (UTC).

Blob reads use your Entra ID sign-in (--auth-mode login), which needs Storage Blob Data
Reader on m4dlogs. Everything downloaded is untrusted data: parse it, never execute it.
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
import zipfile
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone
from pathlib import Path, PurePosixPath

ACCOUNT = "m4dlogs"
RESOURCE_GROUP = "m4d-Web"
CONTAINER = "insights-logs-appservicehttplogs"
APP_CONTAINER = "app-request-logs"
APPS = ("msc4dnc", "m4d-test")
KUDU_DIR = "https://msc4dnc.scm.azurewebsites.net/api/vfs/LogFiles/requests/"
HOUR = re.compile(r"/y=(\d{4})/m=(\d{2})/d=(\d{2})/h=(\d{2})/")
DAY_FILE = re.compile(r"^requests-(?:[a-z0-9]+-)?(\d{4})(\d{2})(\d{2})\.\d{4}\.txt$")
DIAG_FILE = re.compile(r"^diagnostics-(\d{8})\.txt$")
DIAG_TS = re.compile(r"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})")
W3C_TS = re.compile(r"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})")


def utcnow():
    return datetime.now(timezone.utc)


# ---- stamps -------------------------------------------------------------------------------

def read_stamps(root):
    try:
        return json.loads((root / ".fetched.json").read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}


def stamp(root, key):
    stamps = read_stamps(root)
    stamps[key] = utcnow().strftime("%Y-%m-%dT%H:%M:%SZ")
    root.mkdir(parents=True, exist_ok=True)
    (root / ".fetched.json").write_text(json.dumps(stamps, indent=2, sort_keys=True), encoding="utf-8")


def age(stamps, key):
    """Time since `key` was last fetched, or None."""
    value = stamps.get(key)
    if not value:
        return None
    return utcnow() - datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=timezone.utc)


def fmt_age(delta):
    if delta is None:
        return "never"
    minutes = int(delta.total_seconds() // 60)
    return f"{minutes} min ago" if minutes < 120 else f"{minutes // 60} h ago"


# ---- az -----------------------------------------------------------------------------------

def run_az(args):
    exe = shutil.which("az") or shutil.which("az.cmd")
    if not exe:
        sys.exit("Azure CLI (az) not found")
    result = subprocess.run([exe, *args], capture_output=True, text=True)
    if result.returncode != 0:
        sys.exit(f"az {' '.join(args[:3])} failed:\n{result.stderr.strip()}")
    return result.stdout


def az_blob(*args):
    return run_az([*args, "--account-name", ACCOUNT, "--auth-mode", "login"])


# ---- app-service --------------------------------------------------------------------------

def is_request_log(parts):
    return len(parts) == 3 and parts[0] == "LogFiles" and parts[1] == "requests" \
        and DAY_FILE.match(parts[2])


def zip_members(zip_path):
    """(relative name, size, opener) for each file in a downloaded zip."""
    with zipfile.ZipFile(zip_path) as z:
        for info in z.infolist():
            if not info.is_dir():
                yield info.filename, info.file_size, lambda info=info: z.open(info)


def folder_members(folder):
    """The same for an already-extracted download (an older local/logs/<app>-<date>/)."""
    for p in sorted(Path(folder).rglob("*")):
        if p.is_file() and p.suffix != ".zip":
            yield p.relative_to(folder).as_posix(), p.stat().st_size, lambda p=p: open(p, "rb")


def merge_download(source, root, app):
    """Copy a download (a zip, or a folder it was extracted to) into root/<app>, sending
    LogFiles/requests/* to root/requests. An existing file is replaced only by a longer copy.
    Returns (written, kept, skipped)."""
    app_dir = (root / app).resolve()
    req_dir = (root / "requests").resolve()
    members = folder_members(source) if Path(source).is_dir() else zip_members(source)
    written = kept = skipped = 0
    for name, size, open_member in members:
        parts = PurePosixPath(name.replace("\\", "/")).parts
        if not parts or any(p in ("..", "") for p in parts) or parts[0].endswith(":") \
                or name.startswith("/"):
            skipped += 1  # a path that would leave the folder
            continue
        if is_request_log(parts):
            target = req_dir / parts[2]
        elif parts[0] == "LogFiles" and len(parts) > 1 and parts[1] == "requests":
            skipped += 1
            continue
        else:
            target = app_dir.joinpath(*parts)
        target = target.resolve()
        if app_dir not in target.parents and req_dir not in target.parents:
            skipped += 1
            continue
        if target.exists() and target.stat().st_size >= size:
            kept += 1
            continue
        target.parent.mkdir(parents=True, exist_ok=True)
        with open_member() as src, open(target, "wb") as out:
            shutil.copyfileobj(src, out)
        written += 1
    return written, kept, skipped


def fetch_app_service(args, root):
    key = f"app-service:{args.app}"
    if args.zip:
        for path in args.zip:
            w, k, s = merge_download(Path(path), root, args.app)
            print(f"{path}: {w} file(s) written, {k} already as long or longer, {s} skipped")
        return
    since = age(read_stamps(root), key)
    if not args.force and since is not None and since < timedelta(minutes=args.max_age):
        print(f"{args.app} was downloaded {fmt_age(since)} (under --max-age {args.max_age}); "
              "using what's on disk. Pass --force to download again.")
        return
    root.mkdir(parents=True, exist_ok=True)
    zip_path = root / f"{args.app}-download.zip"
    print(f"Downloading the {args.app} App Service logs (a minute or two)...")
    run_az(["webapp", "log", "download", "--name", args.app, "--resource-group", RESOURCE_GROUP,
            "--log-file", str(zip_path)])
    w, k, s = merge_download(zip_path, root, args.app)
    zip_path.unlink()
    stamp(root, key)
    print(f"{w} file(s) written, {k} already as long or longer, {s} skipped, into "
          f"{root / args.app} (and request logs into {root / 'requests'})")


# ---- requests -----------------------------------------------------------------------------

def wanted_day_files(files, dest, hours, now):
    """(name, local path, remote size) for the day files that cover the last `hours` hours
    and that are missing locally or shorter than the remote copy. The files are append-only
    and every source has the same bytes, so a local copy at least as long is kept (it may
    have come from /home, which runs ahead of the hourly blob copy). The logger names files
    by its local date, which is UTC on App Service."""
    first_day = (now - timedelta(hours=hours)).date()
    wanted = []
    for f in files:
        m = DAY_FILE.match(f["name"])
        if not m:
            continue
        day = datetime(*map(int, m.groups())).date()
        local = dest / f["name"]
        if day >= first_day and (not local.exists() or local.stat().st_size < f["size"]):
            wanted.append((f["name"], local, f["size"]))
    return wanted


def blob_source():
    """List and download from the app-request-logs container. When the local copy is
    shorter, only the new byte range is downloaded and appended."""
    files = json.loads(az_blob("storage", "blob", "list", "-c", APP_CONTAINER, "--num-results", "*",
                               "--query", "[].{name:name,size:properties.contentLength}", "-o", "json"))

    def download(name, local, size):
        have = local.stat().st_size if local.exists() else 0
        if have == 0:
            az_blob("storage", "blob", "download", "-c", APP_CONTAINER, "-n", name, "-f", str(local),
                    "--overwrite", "true", "-o", "none")
            return
        part = local.with_name(local.name + ".part")
        az_blob("storage", "blob", "download", "-c", APP_CONTAINER, "-n", name, "-f", str(part),
                "--start-range", str(have), "--end-range", str(size - 1), "--overwrite", "true",
                "-o", "none")
        with open(local, "ab") as out:
            out.write(part.read_bytes())
        part.unlink()
    return files, download


def home_source():
    """List and download from /home/LogFiles/requests through Kudu's file API, with your
    Entra ID sign-in (an ARM token). Downloads whole files."""
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
        data = get(KUDU_DIR + urllib.parse.quote(name))
        if not local.exists() or local.stat().st_size < len(data):
            local.write_bytes(data)
    return files, download


def fetch_requests(args, root):
    dest = root / "requests"
    files, download = home_source() if args.source_from == "home" else blob_source()
    wanted = wanted_day_files(files, dest, args.hours, utcnow())
    dest.mkdir(parents=True, exist_ok=True)

    def fetch(item):
        name, local, size = item
        download(name, local, size)
        return name

    with ThreadPoolExecutor(max_workers=4) as pool:
        for name in pool.map(fetch, wanted):
            print("downloaded", name)
    stamp(root, f"requests:{args.source_from}")
    print(f"{len(wanted)} file(s) downloaded into {dest}")
    print_requests_status(root)


# ---- http ---------------------------------------------------------------------------------

def fetch_http(args, root):
    dest = root / "http"
    blobs = json.loads(az_blob("storage", "blob", "list", "-c", CONTAINER, "--num-results", "*",
                               "--query", "[].{name:name,size:properties.contentLength}", "-o", "json"))
    cutoff = utcnow() - timedelta(hours=args.hours + 1)
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
        az_blob("storage", "blob", "download", "-c", CONTAINER, "-n", name, "-f", str(local),
                "--overwrite", "true", "-o", "none")
        return name

    with ThreadPoolExecutor(max_workers=6) as pool:
        for name in pool.map(fetch, wanted):
            print("downloaded", HOUR.search(name).group(0).strip("/"))
    stamp(root, "http")
    print(f"{len(wanted)} blob(s) downloaded into {dest}")
    print_http_status(root)


# ---- status -------------------------------------------------------------------------------

def last_line(path, pattern):
    """The timestamp on the last line of `path` that matches `pattern`, reading only the tail."""
    with open(path, "rb") as f:
        f.seek(0, 2)
        f.seek(max(0, f.tell() - 65536))
        lines = f.read().decode("utf-8", errors="replace").splitlines()
    return next((m.group(1) for m in map(pattern.match, reversed(lines)) if m), None)


def first_line(path, pattern):
    with open(path, encoding="utf-8", errors="replace") as f:
        for _, line in zip(range(200), f):
            m = pattern.match(line)
            if m:
                return m.group(1)
    return None


def print_app_status(root, stamps):
    for app in APPS:
        diag = sorted((root / app).glob("LogFiles/Application/diagnostics-*.txt"))
        print(f"{app}/  fetched {fmt_age(age(stamps, f'app-service:{app}'))}")
        if not diag:
            print("    no diagnostics-*.txt")
            continue
        first = first_line(diag[0], DIAG_TS) or diag[0].name
        last = last_line(diag[-1], DIAG_TS) or diag[-1].name
        docker = list((root / app).glob("LogFiles/*_default_docker*.log"))
        print(f"    app log {first} .. {last} UTC ({len(diag)} day file(s)); "
              f"{len(docker)} container log(s)")


def print_requests_status(root, stamps=None):
    stamps = stamps if stamps is not None else read_stamps(root)
    files = sorted((root / "requests").glob("requests-*.txt"))
    print(f"requests/  fetched from blob {fmt_age(age(stamps, 'requests:blob'))}, "
          f"from home {fmt_age(age(stamps, 'requests:home'))}")
    if not files:
        print("    no requests-*.txt")
        return
    instances = {}
    for p in files:
        instance = p.name.split("-")[1] if p.name.count("-") >= 2 else "-"
        instances.setdefault(instance, []).append(p)
    for instance, paths in sorted(instances.items()):
        first = first_line(paths[0], W3C_TS) or paths[0].name
        last = last_line(paths[-1], W3C_TS) or paths[-1].name
        print(f"    instance {instance}: {first} .. {last} UTC ({len(paths)} file(s))")


def print_http_status(root, stamps=None):
    stamps = stamps if stamps is not None else read_stamps(root)
    hours = sorted(HOUR.search(p.as_posix()).groups()
                   for p in (root / "http").rglob("PT1H.json") if HOUR.search(p.as_posix()))
    print(f"http/  fetched {fmt_age(age(stamps, 'http'))}")
    if hours:
        first, last = ("{}-{}-{} {}:00".format(*h) for h in (hours[0], hours[-1]))
        print(f"    {first} .. {last} UTC ({len(hours)} hour(s); the newest is still growing)")
    else:
        print("    no PT1H.json")


def status(args, root):
    stamps = read_stamps(root)
    print(f"Logs under {root} (now {utcnow():%Y-%m-%d %H:%M} UTC)")
    print_app_status(root, stamps)
    print_requests_status(root, stamps)
    print_http_status(root, stamps)
    reports = sorted((root / "reports").glob("*.md"))
    if reports:
        print(f"reports/  {', '.join(p.name for p in reports[-5:])}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--root", default="local/logs", help="default local/logs")
    sub = ap.add_subparsers(dest="command", required=True)

    sub.add_parser("status", help="what's on disk and when each source was fetched")

    a = sub.add_parser("app-service", help="the App Service log download, merged into <root>/<app>")
    a.add_argument("--app", choices=APPS, default="msc4dnc")
    a.add_argument("--max-age", type=int, default=30,
                   help="minutes; skip the download when the last one is newer (default 30)")
    a.add_argument("--force", action="store_true", help="download even when the last one is recent")
    a.add_argument("--zip", nargs="+", metavar="ZIP_OR_FOLDER",
                   help="merge earlier downloads (zips, or folders they were extracted to) "
                        "instead of downloading")

    r = sub.add_parser("requests", help="the app request log, into <root>/requests")
    r.add_argument("--from", dest="source_from", choices=["blob", "home"], default="blob",
                   help="the hourly copy in blob storage (default) or /home through Kudu")
    r.add_argument("--hours", type=int, default=24, help="how far back to fetch (default 24)")

    h = sub.add_parser("http", help="the App Service HTTP log, into <root>/http")
    h.add_argument("--hours", type=int, default=24, help="how far back to fetch (default 24)")

    args = ap.parse_args()
    root = Path(args.root)
    {"status": status, "app-service": fetch_app_service, "requests": fetch_requests,
     "http": fetch_http}[args.command](args, root)


if __name__ == "__main__":
    main()
