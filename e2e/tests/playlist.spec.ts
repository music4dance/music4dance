import { test } from "@playwright/test";

// Deferred out of the first round (see architecture/playwright-e2e-testing.md's Coverage Plan):
// PlayListController's Create/Update actions are [Authorize(Roles = "dbAdmin")] and every
// playlist-mutating path (BulkCreateTopN, UpdateSpotifyFromSearch, etc.) calls
// SpotifyAuthorization() and MusicServiceManager against a real Spotify playlist - there's no
// user-facing "add these search results to a playlist" flow for an ordinary account, seeded or
// otherwise. The seeded sandbox accounts have no Spotify identity to authorize with, so this
// isn't testable here without real third-party credentials - already an explicit non-goal of
// this suite ("Anything touching live third-party services").
test.fixme("creates a playlist from search results", async ({ page }) => {
  void page;
});
