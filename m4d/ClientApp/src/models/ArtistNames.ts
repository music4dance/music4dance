/**
 * Client-side counterparts of m4dModels/ArtistSplitter.cs helpers for the Artists song property
 * (an ordered, pipe-delimited list of individual artists). The splitting heuristic itself only
 * runs on the server. See architecture/individual-artists.md.
 */

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

export interface ArtistCount {
  artist: string;
  count: number;
}

/**
 * Other individual artists credited alongside `artist` across `songs`, most frequent first.
 * Spelling variants are grouped by artistKey, keeping the most common spelling.
 */
export function collaborators(artist: string, songs: { effectiveArtists: string[] }[]): ArtistCount[] {
  const self = artistKey(artist);
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
 * that appears in it can be linked in place. Matching is case-insensitive.
 */
export function artistCreditLayout(credit: string, artists: string[]): ArtistCreditLayout {
  const lower = credit.toLowerCase();
  const matches: { start: number; end: number; artist: string }[] = [];
  const extra: string[] = [];

  for (const artist of artists) {
    const needle = artist.toLowerCase();
    let start = needle ? lower.indexOf(needle) : -1;
    while (start !== -1 && matches.some((m) => start < m.end && start + needle.length > m.start)) {
      start = lower.indexOf(needle, start + 1);
    }
    if (start === -1) {
      extra.push(artist);
    } else {
      matches.push({ start, end: start + needle.length, artist });
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
