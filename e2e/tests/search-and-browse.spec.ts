import { expect, test } from "@playwright/test";

// Regression-tests SongIndexLocal.Search (L1f, Option A) through its real caller,
// SongSearch.Search() - build a dance+tag filter through the advanced-search UI, confirm the
// results list, then browse into one specific result's song-details page. See
// architecture/playwright-e2e-testing.md's Tier 1 table.
test("filters by dance and tag, then browses into a result", async ({ page }) => {
  await page.goto("/song/advancedsearch?dances=ATN&tags=Pop:Music");

  // Assert there's at least one match, not an exact count - the seed dataset (and this filter's
  // match against it) can reasonably change without this test's purpose (filter works, browsing
  // into a result works) being affected.
  const results = page.locator('a[href*="/song/details/"]');
  await expect(results.first()).toBeVisible();

  // Browse into whichever result comes first, rather than a hardcoded title - self-consistent
  // (the details page should show the exact title we clicked) instead of coupled to one specific
  // seeded song.
  const title = await results.first().textContent();
  await results.first().click();
  await page.waitForURL(/\/song\/details\//);

  await expect(page.getByRole("heading", { level: 1 })).toContainText(title!.trim());
});
