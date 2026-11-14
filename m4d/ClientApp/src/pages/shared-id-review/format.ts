import type { SharedIdReviewDance } from "./SharedIdReviewModel";

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
