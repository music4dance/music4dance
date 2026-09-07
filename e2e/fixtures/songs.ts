import { expect, type Page } from "@playwright/test";

// Reaches a real seeded song through the actual search/filter UI's own results page - never a
// hardcoded SongId copied from the m4d.Sandbox startup banner - so tests survive the seed set's
// composition changing. Uses SongController's AdvancedSearch action directly (the same endpoint
// advanced-search/App.vue's form submits to) rather than driving the form UI, since the goal
// here is just "a real song rated for this dance", not exercising the filter-construction UI
// itself (that's custom-search.spec.ts's job).
export async function findSeededSong(page: Page, danceId: string): Promise<string> {
  await page.goto(`/song/advancedsearch?dances=${danceId}`);

  const firstTitleLink = page.locator('a[href*="/song/details/"]').first();
  await expect(firstTitleLink).toBeVisible();

  const href = await firstTitleLink.getAttribute("href");
  const match = href?.match(/\/song\/details\/([^/?]+)/);
  if (!match) {
    throw new Error(`Couldn't find a seeded song for dance ${danceId} - no results link matched`);
  }

  return match[1];
}
