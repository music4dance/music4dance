/**
 * Test fixture for the shared-id-review page: a SharedIdReviewModel as the server serializes it
 * (camel case, enum names, nulls left out).
 */

export const songA = "00000000-0000-0000-0000-00000000000a";
export const songB = "00000000-0000-0000-0000-00000000000b";
export const songC = "00000000-0000-0000-0000-00000000000c";
export const songD = "00000000-0000-0000-0000-00000000000d";
export const songE = "00000000-0000-0000-0000-00000000000e";

function song(
  songId: string,
  title: string,
  artist: string,
  albums: { index: number; name: string; ids: { id: string; shared: boolean; link?: string }[] }[],
) {
  return {
    songId,
    title,
    artist,
    artists: [artist],
    length: 185,
    tempo: 120,
    dances: [
      { danceId: "SLS", weight: 3 },
      { danceId: "CHA", weight: 1 },
    ],
    created: "2020-01-02T10:00:00",
    albums,
  };
}

// Two songs that differ only by feat., with a proposal that cleans it up
export const featCluster = {
  key: `${songA},${songB}`,
  serviceIds: ["R:USRC1"],
  match: "WithoutFeaturing",
  hasRejectedPair: false,
  hasNoMergeSong: false,
  cleansFeaturing: true,
  defaultAction: "Merge",
  candidates: [
    {
      id: 11,
      source: "Seed",
      created: "2026-11-01T08:00:00+00:00",
      serviceIds: "R:USRC1",
      detail: "Spotify",
    },
  ],
  songs: [
    song(songA, "Probe feat. Crazy Man", "Probe Artist", [
      {
        index: 0,
        name: "First Album",
        ids: [
          { id: "S:sp1", shared: false, link: "https://open.spotify.com/track/sp1" },
          { id: "R:USRC1", shared: true },
        ],
      },
    ]),
    song(songB, "Probe", "Probe Artist", [
      { index: 0, name: "Second Album", ids: [{ id: "R:USRC1", shared: true }] },
      { index: 2, name: "Third Album", ids: [{ id: "I:111", shared: false }] },
    ]),
  ],
  merged: {
    ...song(songA, "Probe feat. Crazy Man", "Probe Artist", [
      { index: 0, name: "First Album", ids: [{ id: "R:USRC1", shared: true }] },
    ]),
  },
  proposal: { title: "Probe", artist: "Probe Artist", artists: ["Probe Artist", "Crazy Man"] },
};

// Three songs with a rejected pair and lengths too far apart, so it starts as Leave; the merge
// couldn't be previewed
export const rejectedCluster = {
  key: `${songC},${songD},${songE}`,
  serviceIds: ["S:sp9"],
  match: "Different",
  hasRejectedPair: true,
  hasNoMergeSong: false,
  cleansFeaturing: false,
  lengthSpread: 42,
  lengthsDiffer: true,
  defaultAction: "Leave",
  candidates: [
    { id: 21, source: "Enrich", created: "2026-11-02T08:00:00+00:00", serviceIds: "S:sp9" },
    { id: 22, source: "Enrich", created: "2026-11-02T08:00:00+00:00", serviceIds: "S:sp9" },
  ],
  songs: [
    song(songC, "Alpha", "One", [{ index: 0, name: "A1", ids: [{ id: "S:sp9", shared: true }] }]),
    song(songD, "Beta", "Two", [{ index: 0, name: "B1", ids: [{ id: "S:sp9", shared: true }] }]),
    song(songE, "Gamma", "Three", [{ index: 1, name: "C1", ids: [{ id: "S:sp9", shared: true }] }]),
  ],
};

// A cluster whose songs changed while the page loaded
export const staleCluster = {
  key: "stale",
  serviceIds: ["I:999"],
  match: "Different",
  hasRejectedPair: false,
  hasNoMergeSong: false,
  cleansFeaturing: false,
  defaultAction: "Merge",
  candidates: [{ id: 31, source: "Seed", created: "2026-11-03T08:00:00+00:00" }],
  songs: [],
};

export const model = {
  page: 2,
  pageCount: 3,
  pendingCandidates: 51,
  clusterCount: 45,
  withoutFeaturingCount: 4,
  closeCount: 7,
  wouldMergeCount: 812,
  wouldMergeFeaturingCount: 140,
  wouldMergeCreditsCount: 25,
  deferredCount: 3,
  // No offset, so it reads as local time and the snapshot is the same in every time zone
  sortedBy: "2026-11-10T09:30:00",
  message: "Merged 1, marked 0 not the same, left 2.",
  clusters: [featCluster, rejectedCluster, staleCluster],
};

export const emptyModel = {
  page: 1,
  pageCount: 1,
  pendingCandidates: 0,
  clusterCount: 0,
  withoutFeaturingCount: 0,
  closeCount: 0,
  clusters: [],
};
