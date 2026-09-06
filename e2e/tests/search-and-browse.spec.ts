import { expect, test } from "@playwright/test";

// Regression-tests SongIndexLocal.Search (L1f, Option A) through its real caller,
// SongSearch.Search() - build a dance+tag filter through the advanced-search UI, confirm the
// results list, then browse into one specific result's song-details page. See
// architecture/playwright-e2e-testing.md's Tier 1 table.
test("filters by dance and tag, then browses into a result", async ({ page }) => {
  await page.goto("/song/advancedsearch?dances=ATN&tags=Pop:Music");

  const results = page.locator('a[href*="/song/details/"]');
  await expect(results).toHaveCount(6);

  await page.getByRole("link", { name: "Ex's & Oh's" }).click();
  await page.waitForURL(/\/song\/details\//);

  await expect(page.getByRole("heading", { level: 1 })).toContainText("Ex's & Oh's");
});
