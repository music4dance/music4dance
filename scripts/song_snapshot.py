"""Answer questions about the production song catalog from an /Admin/IndexBackup snapshot.

Usage: python -I scripts/song_snapshot.py [--snapshot local/index-YYYY-MM-DD.txt] <query> [options]

Queries:
  summary         song count, album / purchase id coverage per service
  duplicate-ids   service ids (Spotify, iTunes, Amazon, ISRC) shared by more than one song
  id-formats      purchase ids not in their service's canonical spelling (MusicService.NormalizeId)

Run `python -I scripts/song_snapshot.py <query> --help` for a query's own options.

The snapshot is the search index export described in
architecture/songs/song-internal-format.md S12.2: one song per line,
"SongId=<guid><TAB><property log>", already decompressed. Without --snapshot the newest
local/index-*.txt is used. (local/backup-*.txt is the SQL backup and holds no songs.)

Each line is replayed into a Song with a subset of the C# rules in m4dModels/Song.cs:
scalar fields follow the "a pseudo user can't overwrite a real user's value" rule, and
albums/purchase ids follow Song.BuildAlbumInfo (additive ids per slot, empty value clears
the slot, Purchase- removes one id, an empty Album:NN removes the album). Tags, dance
ratings and comments are not replayed yet - the raw properties are kept on each Song for
queries that need them.

To add a query, write a `def query_<name>(songs, args)` that takes the list of Songs, and
register it in QUERIES with a function that adds its arguments to the subparser.
"""

from __future__ import annotations

import argparse
import csv
import re
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent

# Service char codes (MusicService.CID) and the purchase-type suffix (MusicService.PurchaseTypes)
SERVICES = {"S": "Spotify", "I": "iTunes", "A": "Amazon", "X": "Xbox", "R": "ISRC"}
PURCHASE_TYPES = {"S": "song", "A": "album"}

# Song-level qualifiers identify a recording; album-level ones are shared by every track on
# an album, so they're only duplicates in a much weaker sense.
SONG_QUALIFIERS = ["SS", "IS", "AS", "RS"]

SCALAR_FIELDS = {"Title", "Artist", "Artists", "Tempo", "Length", "Sample",
                 "Danceability", "Energy", "Valence"}
ALBUM_FIELDS = {"Album", "Publisher", "Track", "Purchase", "Purchase-",
                "PromoteAlbum", "OrderAlbums"}


def unescape(value: str) -> str:
    """Reverse SongProperty.Load and the SongProperty(name, value) constructor's unescaping."""
    return (value.replace("\u001aT", "\t")
            .replace("\u001aR", "\r\n")
            .replace("\\t", "\t")
            .replace("\\<EQ>\\", "="))


@dataclass
class Prop:
    name: str        # full name, e.g. "Purchase:00:SS"
    base: str        # "Purchase"
    index: int | None
    qualifier: str | None   # purchase qualifier ("SS") or dance id ("CHA")
    value: str


def parse_prop(raw: str) -> Prop:
    name, _, value = raw.partition("=")
    parts = name.split(":")
    index = None
    qualifier = None
    if len(parts) > 1:
        if re.fullmatch(r"-?\d+", parts[1]):
            index = int(parts[1])
            qualifier = parts[2] if len(parts) > 2 else None
        else:
            qualifier = parts[1]
    return Prop(name, parts[0], index, qualifier, unescape(value))


@dataclass
class Album:
    index: int
    name: str | None = None
    track: int | None = None
    publisher: str | None = None
    # qualifier -> ids in the order they were added; the first is the primary link
    purchase: dict[str, list[str]] = field(default_factory=dict)


@dataclass
class Song:
    song_id: str
    props: list[Prop]
    scalars: dict[str, str] = field(default_factory=dict)
    albums: list[Album] = field(default_factory=list)
    users: list[str] = field(default_factory=list)   # decorated names, first is creator
    created: str | None = None
    modified: str | None = None
    deleted: bool = False

    @property
    def title(self) -> str:
        return self.scalars.get("Title", "")

    @property
    def artist(self) -> str:
        return self.scalars.get("Artist", "")

    @property
    def length(self) -> int | None:
        value = self.scalars.get("Length")
        return int(value) if value and value.isdigit() else None

    def purchase_ids(self, qualifier: str) -> list[str]:
        """Distinct ids for one qualifier across all albums, in first-seen order."""
        return list(dict.fromkeys(i for a in self.albums for i in a.purchase.get(qualifier, [])))

    def describe(self) -> str:
        length = self.length
        duration = f" [{length // 60}:{length % 60:02d}]" if length else ""
        return f"{self.song_id}  {self.title} - {self.artist}{duration}"


def build_albums(props: list[Prop]) -> list[Album]:
    """Mirror of Song.BuildAlbumInfo, ignoring PromoteAlbum (it only changes display order)."""
    albums: dict[int, Album] = {}
    removed: set[int] = set()
    reorder: list[int] | None = None
    for p in props:
        if p.base not in ALBUM_FIELDS or p.index is None:
            continue
        album = albums.setdefault(p.index, Album(p.index))
        clear = not p.value.strip()
        match p.base:
            case "Album":
                if clear:
                    album.name = None
                    removed.add(p.index)
                else:
                    album.name = p.value
                    removed.discard(p.index)
            case "Publisher":
                album.publisher = None if clear else p.value
            case "Track":
                album.track = None if clear else (int(p.value) if p.value.isdigit() else 0)
            case "Purchase" if (p.qualifier or "").lower() != "ms":
                if clear:
                    album.purchase.pop(p.qualifier, None)
                else:
                    ids = album.purchase.setdefault(p.qualifier, [])
                    if p.value not in ids:
                        ids.append(p.value)
            case "Purchase-" if not clear and (p.qualifier or "").lower() != "ms":
                ids = album.purchase.get(p.qualifier)
                if ids and p.value in ids:
                    ids.remove(p.value)
            case "OrderAlbums":
                reorder = [int(i) for i in p.value.split(",") if i.strip()]

    for index in removed:
        albums.pop(index, None)
    if reorder is not None:
        return [albums[i] for i in reorder if i in albums]
    return [albums[i] for i in sorted(albums) if albums[i].name is not None]


def parse_song(line: str) -> Song:
    fields = line.rstrip("\r\n").split("\t")
    song_id = fields[0].removeprefix("SongId=").strip("{}").lower()
    props = [parse_prop(f) for f in fields[1:] if f]
    song = Song(song_id, props)

    pseudo = False
    user_set: set[str] = set()
    for p in props:
        match p.base:
            case "User" | "UserProxy":
                pseudo = p.value.endswith("|P")
                if p.value not in song.users:
                    song.users.append(p.value)
            case "Time":
                song.created = song.created or p.value
                song.modified = p.value
            case ".Delete":
                song.deleted = p.value in ("", "true", "True")
            case base if base in SCALAR_FIELDS and p.qualifier is None:
                if base in user_set and pseudo:
                    continue
                song.scalars[base] = p.value
                if not pseudo:
                    user_set.add(base)

    if song.deleted:
        song.scalars.clear()
    song.albums = build_albums(props)
    return song


def latest_snapshot() -> Path:
    candidates = sorted((REPO_ROOT / "local").glob("index-*.txt"))
    if not candidates:
        sys.exit("No local/index-*.txt found; download one from /Admin/IndexBackup or pass --snapshot.")
    return candidates[-1]


def load_songs(path: Path) -> list[Song]:
    with path.open(encoding="utf-8") as f:
        songs = [parse_song(line) for line in f if line.strip()]
    return [s for s in songs if not s.deleted and s.title]


def qualifier_label(qualifier: str) -> str:
    service = SERVICES.get(qualifier[0], qualifier[0])
    kind = PURCHASE_TYPES.get(qualifier[1:], qualifier[1:]) if len(qualifier) > 1 else ""
    return f"{service} {kind}".strip()


def normalize_id(qualifier: str, value: str) -> str:
    """Mirror of MusicService.NormalizeId for the service named by qualifier[0]."""
    if not value.strip():
        return value
    match qualifier[:1]:
        case "R":   # ISRCService: "gb-smu-25-38994" -> "GBSMU2538994"
            return re.sub(r"[-\s]", "", value).upper()
        case "A":   # AmazonService: a bare ASIN gets the digital prefix
            return value if ":" in value else "D:" + value
    return value


# What a well-formed id looks like once normalized, per service char
CANONICAL = {
    "S": re.compile(r"[0-9A-Za-z]{22}"),
    "I": re.compile(r"\d+"),
    "A": re.compile(r"[AD]:[0-9A-Z]{10}"),
    "R": re.compile(r"[A-Z]{2}[0-9A-Z]{3}\d{7}"),
}


# --- summary -----------------------------------------------------------------------------

def query_summary(songs: list[Song], args: argparse.Namespace) -> None:
    print(f"Songs: {len(songs):,}")
    print(f"Albums: {sum(len(s.albums) for s in songs):,}")
    print()
    qualifiers = sorted({q for s in songs for a in s.albums for q in a.purchase})
    print(f"{'Qualifier':<10}{'Service':<16}{'Songs with id':>15}{'Distinct ids':>15}{'Songs w/ >1 id':>16}")
    for q in qualifiers:
        per_song = [s.purchase_ids(q) for s in songs]
        with_id = sum(1 for ids in per_song if ids)
        multi = sum(1 for ids in per_song if len(ids) > 1)
        distinct = len({normalize_id(q, i) for ids in per_song for i in ids})
        print(f"{q:<10}{qualifier_label(q):<16}{with_id:>15,}{distinct:>15,}{multi:>16,}")


def add_summary_args(parser: argparse.ArgumentParser) -> None:
    pass


# --- duplicate-ids -----------------------------------------------------------------------

def find_components(groups: list[list[str]]) -> list[set[str]]:
    """Union-find over song ids: songs linked by any shared id end up in one component."""
    parent: dict[str, str] = {}

    def find(x: str) -> str:
        while parent.setdefault(x, x) != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    for group in groups:
        root = find(group[0])
        for other in group[1:]:
            parent[find(other)] = root

    components: dict[str, set[str]] = defaultdict(set)
    for x in parent:
        components[find(x)].add(x)
    return list(components.values())


def query_duplicate_ids(songs: list[Song], args: argparse.Namespace) -> None:
    by_id = {s.song_id: s for s in songs}
    qualifiers = args.qualifiers.split(",")

    # qualifier -> normalized id -> song ids holding it
    holders: dict[str, dict[str, list[str]]] = {q: defaultdict(list) for q in qualifiers}
    for s in songs:
        for q in qualifiers:
            for value in dict.fromkeys(normalize_id(q, i) for i in s.purchase_ids(q)):
                holders[q][value].append(s.song_id)

    print(f"Snapshot: {args.snapshot.name}  ({len(songs):,} songs)")
    print()
    print(f"{'Qualifier':<10}{'Service':<16}{'Distinct ids':>14}{'Shared ids':>12}"
          f"{'Songs sharing':>15}{'Largest':>9}")
    all_groups: list[list[str]] = []
    shared_by_qualifier: dict[str, list[tuple[str, list[str]]]] = {}
    for q in qualifiers:
        shared = [(value, ids) for value, ids in holders[q].items() if len(ids) > 1]
        shared.sort(key=lambda item: (-len(item[1]), item[0]))
        shared_by_qualifier[q] = shared
        all_groups.extend(ids for _, ids in shared)
        songs_sharing = len({sid for _, ids in shared for sid in ids})
        largest = len(shared[0][1]) if shared else 0
        print(f"{q:<10}{qualifier_label(q):<16}{len(holders[q]):>14,}{len(shared):>12,}"
              f"{songs_sharing:>15,}{largest:>9}")

    components = find_components(all_groups) if all_groups else []
    involved = sum(len(c) for c in components)
    print()
    print(f"Across {', '.join(qualifiers)}: {involved:,} songs share at least one id with another "
          f"song, forming {len(components):,} clusters; merging each cluster down to one song "
          f"would remove {involved - len(components):,}.")
    sizes = Counter(len(c) for c in components)
    print("Cluster sizes: " + ", ".join(f"{n} songs x{count:,}" for n, count in sorted(sizes.items())))

    for q in qualifiers:
        shared = shared_by_qualifier[q]
        if not shared or args.top == 0:
            continue
        print()
        print(f"== {qualifier_label(q)} ({q}): top {min(args.top, len(shared))} of {len(shared):,} shared ids ==")
        for value, ids in shared[:args.top]:
            print(f"{value}  ({len(ids)} songs)")
            for sid in ids:
                print(f"    {by_id[sid].describe()}")

    if args.csv:
        with args.csv.open("w", newline="", encoding="utf-8") as f:
            writer = csv.writer(f)
            writer.writerow(["qualifier", "id", "songs_sharing", "song_id", "title", "artist", "length"])
            for q in qualifiers:
                for value, ids in shared_by_qualifier[q]:
                    for sid in ids:
                        s = by_id[sid]
                        writer.writerow([q, value, len(ids), sid, s.title, s.artist, s.length or ""])
        print()
        print(f"Wrote {args.csv}")


def add_duplicate_ids_args(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--qualifiers", default=",".join(SONG_QUALIFIERS),
                        help="comma-separated purchase qualifiers to check (default: %(default)s; "
                             "album-level ones are SA, IA, AA)")
    parser.add_argument("--top", type=int, default=10,
                        help="shared ids to list per qualifier, largest first (default: %(default)s)")
    parser.add_argument("--csv", type=Path, help="write every shared id and its songs to this CSV file")


# --- id-formats --------------------------------------------------------------------------

def query_id_formats(songs: list[Song], args: argparse.Namespace) -> None:
    """Checks the raw purchase values in the log rather than the replayed albums, since the
    point is to see what a reload would rewrite."""
    totals: dict[str, Counter] = defaultdict(Counter)
    samples: dict[tuple[str, str], list[str]] = defaultdict(list)
    affected: dict[str, set[str]] = defaultdict(set)
    for s in songs:
        for p in s.props:
            if p.base not in ("Purchase", "Purchase-") or not p.qualifier or not p.value.strip():
                continue
            q = p.qualifier
            totals[q]["total"] += 1
            normalized = normalize_id(q, p.value)
            pattern = CANONICAL.get(q[0])
            kind = None
            if normalized != p.value:
                kind = "non-canonical"
                affected[q].add(s.song_id)
            if pattern and not pattern.fullmatch(normalized):
                kind = "malformed"
            if kind:
                totals[q][kind] += 1
                if len(samples[(q, kind)]) < args.samples:
                    samples[(q, kind)].append(f"{p.value!r} in {s.song_id}")

    print(f"Snapshot: {args.snapshot.name}  ({len(songs):,} songs)")
    print()
    print(f"{'Qualifier':<10}{'Service':<16}{'Values':>10}{'Non-canonical':>15}{'Songs':>8}{'Malformed':>11}")
    for q in sorted(totals):
        t = totals[q]
        print(f"{q:<10}{qualifier_label(q):<16}{t['total']:>10,}{t['non-canonical']:>15,}"
              f"{len(affected[q]):>8,}{t['malformed']:>11,}")
    print()
    print("Non-canonical: a reload rewrites these to MusicService.NormalizeId's spelling.")
    print("Malformed: not a valid-looking id even once normalized; a reload leaves it as is.")
    for (q, kind), values in sorted(samples.items()):
        print()
        print(f"== {q} {kind} ==")
        for v in values:
            print(f"    {v}")


def add_id_formats_args(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--samples", type=int, default=5,
                        help="example values to show per qualifier and problem (default: %(default)s)")


# --- entry point -------------------------------------------------------------------------

QUERIES = {
    "summary": (query_summary, add_summary_args, "song count and purchase id coverage"),
    "duplicate-ids": (query_duplicate_ids, add_duplicate_ids_args,
                      "service ids shared by more than one song"),
    "id-formats": (query_id_formats, add_id_formats_args,
                   "purchase ids not in their service's canonical spelling"),
}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--snapshot", type=Path,
                        help="index backup to read (default: newest local/index-*.txt)")
    sub = parser.add_subparsers(dest="query", required=True)
    for name, (_, add_args, help_text) in QUERIES.items():
        add_args(sub.add_parser(name, help=help_text))
    args = parser.parse_args()

    args.snapshot = args.snapshot or latest_snapshot()
    sys.stdout.reconfigure(encoding="utf-8")
    print(f"Loading {args.snapshot} ...", file=sys.stderr)
    songs = load_songs(args.snapshot)
    QUERIES[args.query][0](songs, args)


if __name__ == "__main__":
    main()
