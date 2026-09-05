import { test } from "@playwright/test";

// TODO: implement once the song-index/search UI has been inspected against a running
// m4d.Sandbox. This is meant to regression-test SongIndexLocal.Search (L1f, Option A) through
// its real caller, SongSearch.Search() - filter by dance + tempo range, sort by title, page
// through results - see architecture/playwright-e2e-testing.md's Tier 1 table.
//
// Locate real songs through the search UI itself (e.g. a fixtures/songs.ts helper), never a
// hardcoded SongId copied from the m4d.Sandbox startup banner - the seed set can change shape.
test.fixme("filters the song list by dance and sorts by title", async ({ page }) => {
  void page;
});
