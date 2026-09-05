import type { Page } from "@playwright/test";

// TODO: implement once the song-index/search UI has been inspected against a running
// m4d.Sandbox (see tests/search-and-browse.spec.ts). Tests should reach a specific seeded song
// through the real search/filter UI - never a hardcoded SongId copied from the m4d.Sandbox
// startup banner - so they survive the seed set's composition changing. See
// architecture/playwright-e2e-testing.md, "Open Questions" for the fallback if that ever proves
// too fragile (a couple of dedicated fixture songs seeded specifically for e2e).
export async function findSeededSong(page: Page, danceId: string): Promise<string> {
  void page;
  void danceId;
  throw new Error("findSeededSong is not implemented yet - see the TODO in this file");
}
