import { expect, test } from "@playwright/test";

import { artistSearchBox, contentHeading, openArtistIndex } from "../fixtures/artists";

// The browsable index of individual artists (/song/artists), behind the ArtistIndex feature
// flag, which m4d.Sandbox/appsettings.json turns on. See architecture/individual-artists.md §9.
test("browses artists by letter", async ({ page }) => {
  await openArtistIndex(page);

  // No letter picked yet, so the page opens on the most popular artists
  await expect(contentHeading(page, 2)).toHaveText("Most popular artists");

  const letters = page.getByRole("navigation", { name: "Artists by letter" }).getByRole("link");
  // Buckets with no artists render disabled, so pick one that's actually navigable rather than
  // assuming the seed set covers any particular letter.
  const enabled = letters.filter({ hasNot: page.locator(".disabled") });
  const letter = (await enabled.first().textContent())!.trim();

  await enabled.first().click();
  await page.waitForURL(/letter=/);

  await expect(contentHeading(page, 2)).toContainText(letter);
  await expect(page.locator(".artist-list a").first()).toBeVisible();
});

test("searches for an artist and lands on their page", async ({ page }) => {
  await openArtistIndex(page);

  // Take a real artist off the index rather than naming one, so the seed set can change
  const first = page.locator(".artist-list a").first();
  await expect(first).toBeVisible();
  const name = (await first.textContent())!.trim();

  await artistSearchBox(page).fill(name);
  await page.getByRole("button", { name: "Search" }).click();
  // Either the search results or, when the name matched one artist, that artist's page
  await page.waitForURL(/\/song\/artists?\?/);

  // A search with a single match redirects instead of listing it, and whether a name is unique in
  // the catalog is the seed set's business - so accept either page and assert what they share.
  if (!/\/song\/artist\?/.test(page.url())) {
    await expect(contentHeading(page, 2)).toContainText(name);
    // Whatever is still listed was ambiguous, so it is more than the one match
    expect(await page.locator(".artist-list li").count()).toBeGreaterThan(1);

    const match = page.locator(".artist-list a", { hasText: name }).first();
    await expect(match).toBeVisible();
    await match.click();
    await page.waitForURL(/\/song\/artist\?/);
  }

  await expect(contentHeading(page, 1)).toContainText(name);
});

// Type-ahead comes from the same in-memory snapshot the page browses (not an Azure suggester),
// so a suggestion is always something the search can actually find - individual-artists.md §9.4.
test("suggests artists as you type", async ({ page }) => {
  await openArtistIndex(page);

  const first = page.locator(".artist-list a").first();
  await expect(first).toBeVisible();
  const name = (await first.textContent())!.trim();

  const options = page.locator("#artist-search-suggestions option");
  await expect(options).toHaveCount(0);

  await artistSearchBox(page).fill(name.slice(0, 4));

  // Suggestions are capped at 10 and this artist is one of the most popular, so it should be among
  // them for its own opening characters.
  await expect
    .poll(async () =>
      options.evaluateAll((els) => els.map((el) => (el as HTMLOptionElement).value)),
    )
    .toContain(name);
});

test("toggles single-song artists in and out of the listing", async ({ page }) => {
  await openArtistIndex(page, "letter=B");

  const listed = page.locator(".artist-list li");
  await expect(listed.first()).toBeVisible();
  const withoutSingles = await listed.count();

  await page.getByRole("link", { name: "Include artists with only one song" }).click();
  await page.waitForURL(/all=true/);

  await expect(page.getByRole("link", { name: "Hide artists with only one song" })).toBeVisible();
  // Relaxing the threshold can only add artists, never remove them
  expect(await listed.count()).toBeGreaterThanOrEqual(withoutSingles);
});
