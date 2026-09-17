import { expect, test } from "@playwright/test";

import { contentHeading, openArtistIndex } from "../fixtures/artists";

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

test("searches for an artist and follows the result to their page", async ({ page }) => {
  await openArtistIndex(page);

  // Take a real artist off the index rather than naming one, so the seed set can change
  const first = page.locator(".artist-list a").first();
  await expect(first).toBeVisible();
  const name = (await first.textContent())!.trim();

  await page.getByRole("searchbox", { name: "Find an artist" }).fill(name);
  await page.getByRole("button", { name: "Search" }).click();
  await page.waitForURL(/[?&]q=/);

  await expect(contentHeading(page, 2)).toContainText(name);

  const match = page.locator(".artist-list a", { hasText: name }).first();
  await expect(match).toBeVisible();
  await match.click();

  await page.waitForURL(/\/song\/artist\?/);
  await expect(contentHeading(page, 1)).toContainText(name);
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
