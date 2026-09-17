/**
 * Client-side counterparts of m4dModels/ArtistSplitter.cs helpers for the Artists song property
 * (an ordered, pipe-delimited list of individual artists). The splitting heuristic itself only
 * runs on the server. See architecture/artist-index-plan.md.
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

/** URL of the artist page for one individual artist (or a whole credit). */
export function artistPageUrl(name: string): string {
  return `/song/artist?name=${encodeURIComponent(name)}`;
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
