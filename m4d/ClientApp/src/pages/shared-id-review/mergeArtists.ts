import { getMenuContext } from "@/helpers/GetMenuContext";
import type { ClusterChoice } from "./ClusterChoice";
import type { SharedIdArtistsResponse } from "./SharedIdReviewModel";

/**
 * Merge artist (and, with force, Force merge artist): asks the server for the cluster's songs'
 * artists combined (POST /api/sharedids/artists) and, when a track the songs have credits them
 * all or force skips the check, types them into the If merged column and makes the cluster
 * Merge. Returns what happened, for the card to show.
 */
export async function mergeArtists(
  choice: ClusterChoice,
  force: boolean,
): Promise<{ ok: boolean; message: string }> {
  const response = await getMenuContext().axiosXsrf.post<SharedIdArtistsResponse>(
    "/api/sharedids/artists",
    { songIds: choice.cluster.songs.map((s) => s.songId), force },
  );
  const combined = response.data;
  if (!combined.artist) {
    return { ok: false, message: combined.reason ?? "No artists to merge" };
  }
  if (!force && !combined.confirmed) {
    return {
      ok: false,
      message:
        combined.missing.length > 0
          ? `${combined.via} doesn't credit ${combined.missing.join(", ")}: nothing changed`
          : `${combined.reason ?? "No track to check"}: nothing changed`,
    };
  }
  const failed = choice.typeArtists(combined.artist, combined.artists);
  if (failed) {
    return { ok: false, message: failed };
  }
  return {
    ok: true,
    message: force
      ? `Artists merged: ${combined.artist}`
      : `${combined.via} credits ${combined.artist}: artists merged`,
  };
}
