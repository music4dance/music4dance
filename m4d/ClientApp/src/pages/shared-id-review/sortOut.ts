import { getMenuContext } from "@/helpers/GetMenuContext";
import type { ClusterChoice } from "./ClusterChoice";
import type { SharedIdOwnersResponse } from "./SharedIdReviewModel";

/**
 * Sort out ids: asks the server which of the cluster's songs each id they share belongs to
 * (POST /api/sharedids/owners, which looks the ids up on Spotify and iTunes) and checks the
 * removals that carry it out.
 */
export async function sortOutIds(choice: ClusterChoice): Promise<void> {
  const response = await getMenuContext().axiosXsrf.post<SharedIdOwnersResponse>(
    "/api/sharedids/owners",
    { songIds: choice.cluster.songs.map((s) => s.songId) },
  );
  choice.applyOwners(response.data);
}
