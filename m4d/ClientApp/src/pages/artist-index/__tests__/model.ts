const letters = [..."ABCDEFGHIJKLMNOPQRSTUVWXYZ", "#"];

export const model = {
  letter: "B",
  query: null,
  minSongs: 2,
  totalArtists: 4,
  built: "2026-09-16T12:00:00Z",
  buckets: letters.map((bucket) => ({ bucket, artists: bucket === "B" ? 2 : bucket === "D" ? 1 : 0 })),
  artists: [
    { name: "The Beatles", songs: 12 },
    { name: "Michael Bublé", songs: 7 },
  ],
};
