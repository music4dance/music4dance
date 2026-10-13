import { expect, type Page, test } from "@playwright/test";

import { login } from "../fixtures/auth";
import { findSeededSong } from "../fixtures/songs";

// As the plain "tester" account, likes a seeded song from its details page (SongLikeButton in
// m4d/ClientApp/src/pages/song/components/SongCore.vue), checking after each reload that the
// like still reads as tester's own, then "Undo My Changes" to leave the shared sandbox instance
// clean for later tests/runs - see architecture/dev-testing/playwright-e2e-testing.md,
// "Concurrency".
//
// The reloads are the point: the client finds "your" like by matching history users against
// its own user name, and tester is a private (Privacy == 0) account whose own history entries
// the server used to anonymize to their user id. A saved like then read as no like at all after
// a reload, so clicking again re-liked the song instead of moving it to the blocked list.
async function saveChanges(page: Page) {
  await page.getByRole("button", { name: "Save Changes" }).click();
  await expect(page.getByRole("button", { name: "Undo My Changes" })).toBeVisible();
}

test("likes, then blocks, a song as the plain tester account", async ({ page }) => {
  await login(page, "tester");

  const songId = await findSeededSong(page, "ATN");
  await page.goto(`/song/details/${songId}`);

  // LikeButton's aria-label is its tooltip, which spells out the current like state.
  const likeButton = page.locator(`#like-button-${songId}`);
  await expect(likeButton).toHaveAccessibleName(/^Click to add .* to favorites\.$/);

  await likeButton.click();
  await saveChanges(page);
  await page.reload();
  await expect(likeButton).toHaveAccessibleName(/ is in your favorites, /);

  await likeButton.click();
  await saveChanges(page);
  await page.reload();
  await expect(likeButton).toHaveAccessibleName(/ is in your blocked list, /);

  await page.getByRole("button", { name: "Undo My Changes" }).click();
  await page.getByRole("button", { name: "YES", exact: true }).click();
  await page.waitForURL(/\/song\/details\//);
  await expect(page.getByRole("button", { name: "Undo My Changes" })).toHaveCount(0);
  await expect(likeButton).toHaveAccessibleName(/^Click to add .* to favorites\.$/);
});
