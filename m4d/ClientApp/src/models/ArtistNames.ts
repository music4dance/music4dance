/**
 * Client-side counterparts of m4dModels/ArtistSplitter.cs helpers for the Artists song property
 * (an ordered, pipe-delimited list of individual artists). The splitting heuristic itself only
 * runs on the server. See architecture/songs/individual-artists.md.
 */

import aliasTable from "./artist-aliases.json";

export const ARTISTS_DELIMITER = "|";
export const ARTIST_BOT_USER = "artist-bot";

/** Who supplied a song's explicit Artists list. Precedence is User > Service > Heuristic. */
export type ArtistsSource = "None" | "Heuristic" | "Service" | "User";

const edgePunctuation = /^[ ,;:-]+|[ ,;:-]+$/g;

/** Must match ArtistSplitter.CleanName. */
export function cleanArtistName(name?: string | null): string {
  if (!name || !name.trim()) {
    return "";
  }

  let s = name.replaceAll(ARTISTS_DELIMITER, "/").replace(/\s+/g, " ").replace(edgePunctuation, "");

  while (s.length > 1) {
    const first = s[0];
    const last = s[s.length - 1];
    if ((first === "(" && last === ")") || (first === "[" && last === "]")) {
      s = s.slice(1, -1).trim();
    } else if ((last === ")" || last === "]") && !s.includes(last === ")" ? "(" : "[")) {
      s = s.slice(0, -1).trim();
    } else if ((first === "(" || first === "[") && !s.includes(first === "(" ? ")" : "]")) {
      s = s.slice(1).trim();
    } else {
      break;
    }
  }

  return s.replace(edgePunctuation, "");
}

/** Must match ArtistSplitter.ArtistKey: case- and diacritic-insensitive artist identity. */
export function artistKey(name?: string | null): string {
  return cleanArtistName(name)
    .toLowerCase()
    .normalize("NFD")
    .replace(/\p{Mn}/gu, "");
}

// Must match ArtistAliases.Key: the artistKey's letters and digits alone
function aliasKey(name: string): string {
  return artistKey(name).replace(/[^\p{L}\p{Nd}]/gu, "");
}

// Each alias's key, to its row in artist-aliases.json (shared with m4dModels/ArtistAliases.cs);
// the first list to name a key wins, as on the server
const aliasRows = new Map<string, string[]>();
for (const names of aliasTable.aliases) {
  for (const name of names) {
    const key = aliasKey(name);
    if (key && !aliasRows.has(key)) {
      aliasRows.set(key, names);
    }
  }
}

/** Must match ArtistAliases.PreferredName: "Pink" is P!nk; a name without aliases comes back as is. */
export function preferredArtistName(name: string): string {
  return aliasRows.get(aliasKey(name))?.[0] ?? name;
}

/** Every name the artist is credited under, the one to use first; just the name without aliases. */
export function artistSpellings(name: string): string[] {
  return aliasRows.get(aliasKey(name)) ?? [name];
}

export interface ArtistCount {
  artist: string;
  count: number;
}

/**
 * Other individual artists credited alongside `artist` across `songs`, most frequent first.
 * Spelling variants are grouped by artistKey, keeping the most common spelling.
 */
export function collaborators(
  artist: string,
  songs: { effectiveArtists: string[] }[],
): ArtistCount[] {
  // The page's name may be an alias ("Pink"), while the songs read as the name to use
  const self = artistKey(preferredArtistName(artist));
  const groups = new Map<string, Map<string, number>>();
  for (const song of songs) {
    const keys = new Set(song.effectiveArtists.map(artistKey));
    if (!keys.has(self)) {
      continue;
    }
    for (const name of song.effectiveArtists) {
      const key = artistKey(name);
      if (key === self) {
        continue;
      }
      const spellings = groups.get(key) ?? new Map<string, number>();
      spellings.set(name, (spellings.get(name) ?? 0) + 1);
      groups.set(key, spellings);
    }
  }

  return [...groups.values()]
    .map((spellings) => {
      const sorted = [...spellings.entries()].sort((a, b) => b[1] - a[1]);
      return {
        artist: sorted[0]![0],
        count: sorted.reduce((sum, [, count]) => sum + count, 0),
      };
    })
    .sort((a, b) => b.count - a.count || a.artist.localeCompare(b.artist));
}

/** URL of the artist page for one individual artist (or a whole credit). */
export function artistPageUrl(name: string): string {
  return `/song/artist?name=${encodeURIComponent(name)}`;
}

export interface ArtistCreditSegment {
  text: string;
  /** Set when this segment of the credit is one of the song's individual artists */
  artist?: string;
}

export interface ArtistCreditLayout {
  segments: ArtistCreditSegment[];
  /** Individual artists that don't appear in the credit text (e.g. from a title "feat.") */
  extra: string[];
}

/**
 * Splits a credit ("Dolly Parton & Kenny Rogers") into text segments so each individual artist
 * that appears in it can be linked in place. Matching is case-insensitive, and an artist matches
 * any of their spellings (artistSpellings), so P!nk is linked on the "Pink" in "Pink feat. X".
 */
export function artistCreditLayout(credit: string, artists: string[]): ArtistCreditLayout {
  const lower = credit.toLowerCase();
  const matches: { start: number; end: number; artist: string }[] = [];
  const extra: string[] = [];

  const find = (needle: string): number => {
    let start = needle ? lower.indexOf(needle) : -1;
    while (start !== -1 && matches.some((m) => start < m.end && start + needle.length > m.start)) {
      start = lower.indexOf(needle, start + 1);
    }
    return start;
  };

  for (const artist of artists) {
    // The longest of its spellings in the credit, so "The New 101 Strings Orchestra" is linked
    // whole rather than on the "101 Strings Orchestra" inside it
    const needles = [...new Set([artist, ...artistSpellings(artist)])]
      .sort((a, b) => b.length - a.length)
      .map((s) => s.toLowerCase());
    const found = needles
      .map((needle) => ({ needle, start: find(needle) }))
      .find((f) => f.start !== -1);
    if (found) {
      matches.push({ start: found.start, end: found.start + found.needle.length, artist });
    } else {
      extra.push(artist);
    }
  }

  matches.sort((a, b) => a.start - b.start);
  const segments: ArtistCreditSegment[] = [];
  let cursor = 0;
  for (const m of matches) {
    if (m.start > cursor) {
      segments.push({ text: credit.slice(cursor, m.start) });
    }
    segments.push({ text: credit.slice(m.start, m.end), artist: m.artist });
    cursor = m.end;
  }
  if (cursor < credit.length) {
    segments.push({ text: credit.slice(cursor) });
  }

  return { segments, extra };
}

export function deserializeArtists(value?: string | null): string[] {
  return (value ?? "")
    .split(ARTISTS_DELIMITER)
    .map(cleanArtistName)
    .filter((a) => a.length > 0);
}

export function serializeArtists(artists: string[]): string {
  return artists
    .map(cleanArtistName)
    .filter((a) => a.length > 0)
    .join(ARTISTS_DELIMITER);
}
