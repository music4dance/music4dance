import { expect, type Page } from "@playwright/test";

// The artist index is a 6-hour in-memory snapshot built in the background on first request
// (m4d/Services/ArtistIndexCache.cs), so a cold sandbox serves "we're putting the artist index
// together" for the first few seconds. Reload until it's ready rather than sleeping.
export async function openArtistIndex(page: Page, query = ""): Promise<void> {
  const url = query ? `/song/artists?${query}` : "/song/artists";
  await page.goto(url);

  await expect(async () => {
    if (await page.getByText("We're putting the artist index together").isVisible()) {
      await page.reload();
      throw new Error("artist index still building");
    }
    await expect(page.getByRole("navigation", { name: "Artists by letter" })).toBeVisible();
  }).toPass({ timeout: 30_000 });
}

// A song whose credit names more than one artist, found through the search UI rather than by
// naming a seeded artist: "feat." is the marker the splitter keys on, so searching for it finds
// exactly the songs that should render several linked artists. Returns the details-page URL.
export async function findSplitSong(page: Page): Promise<string> {
  await page.goto("/song/advancedsearch?searchString=feat.");

  const results = page.locator('a[href*="/song/details/"]');
  await expect(results.first()).toBeVisible();

  const hrefs = (await results.evaluateAll((links) =>
    links.map((link) => link.getAttribute("href")),
  )) as string[];

  for (const href of [...new Set(hrefs)].slice(0, 10)) {
    await page.goto(href);
    if ((await artistLinks(page).count()) > 1) {
      return href;
    }
  }

  throw new Error(
    "No seeded song rendered more than one linked artist - has the splitter or the seed set changed?",
  );
}

// The sandbox reports its stand-in services as unavailable, so ServiceStatusBanner renders an
// accordion whose header is also an <h2>. It sits outside #body-content (PageFrame.vue), so
// scoping there picks out the page's own heading without depending on Bootstrap classes.
export function contentHeading(page: Page, level: 1 | 2) {
  return page.locator("#body-content").getByRole("heading", { level });
}

// Individual-artist links in the song's credit (m4d/ClientApp/src/components/ArtistCredit.vue),
// scoped to the title heading so the dance/tag panels below can't contribute matches.
export function artistLinks(page: Page) {
  return page.locator("#body-content").getByRole("heading", { level: 1 }).locator('a[href*="/song/artist?name="]');
}
