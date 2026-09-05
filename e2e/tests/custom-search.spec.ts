import { test } from "@playwright/test";

// TODO: implement once the advanced/custom-search UI has been inspected against a running
// m4d.Sandbox. Build a dance+tag filter through the UI and assert the resulting URL/query uses
// the expected DanceQueryItem/Tag shape - a manual-construction regression (see CLAUDE.md,
// "Filter / Tag Construction") would show up here as a broken query string, not just a failing
// unit test. See architecture/playwright-e2e-testing.md's Tier 1 table.
test.fixme("builds a dance+tag filter through the custom search UI", async ({ page }) => {
  void page;
});
