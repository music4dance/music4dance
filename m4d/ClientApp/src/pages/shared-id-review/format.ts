import type { SharedIdReviewDance, SharedIdServiceTrack } from "./SharedIdReviewModel";

export function formatLength(seconds?: number): string {
  return seconds === undefined || seconds === null
    ? ""
    : `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, "0")}`;
}

// "SLS+3, CHA+1", heaviest first as the server sends them
export function formatDances(dances?: SharedIdReviewDance[]): string {
  return (dances ?? [])
    .map((d) => `${d.danceId}${d.weight > 0 ? `+${d.weight}` : d.weight}`)
    .join(", ");
}

export function formatArtists(artists?: string[]): string {
  return (artists ?? []).join(" | ");
}

// The date part of a server timestamp ("2020-01-02T10:00:00" → "2020-01-02")
export function formatDate(value?: string): string {
  return value ? value.substring(0, 10) : "";
}

export function formatDateTime(value?: string): string {
  if (!value) {
    return "";
  }
  const date = new Date(value);
  const pad = (n: number) => String(n).padStart(2, "0");
  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ` +
    `${pad(date.getHours())}:${pad(date.getMinutes())}`
  );
}

// What a service says one of its ids is, for the id's tooltip:
// "Title — Artist", then the album, then "3:05 · 2020-01-02 · R:USRC1", leaving out what the
// service didn't say
export function describeTrack(track: SharedIdServiceTrack): string {
  if (!track.found) {
    return "Not found";
  }
  const details = [
    formatLength(track.length),
    track.releaseDate,
    track.isrc ? `R:${track.isrc}` : "",
  ].filter((d) => !!d);
  return [`${track.title ?? ""} — ${track.artist ?? ""}`, track.album, details.join(" · ")]
    .filter((line) => !!line)
    .join("\n");
}
