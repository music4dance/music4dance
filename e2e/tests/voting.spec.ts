import { test } from "@playwright/test";

// TODO: implement once a reliable way to reach a specific seeded song via the UI exists
// (see search-and-browse.spec.ts). As the plain "tester" account, cast a dance-rating vote on a
// song, verify the displayed total changes, then use the existing "Undo My Changes" button
// (m4d/ClientApp/src/pages/song/components/SongCore.vue) to leave the shared sandbox instance
// clean for later tests/runs - see architecture/playwright-e2e-testing.md, "Concurrency".
test.fixme("casts and undoes a dance-rating vote as the plain tester account", async ({ page }) => {
  void page;
});
