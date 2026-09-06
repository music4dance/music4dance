import { expect, test } from "@playwright/test";

import { login } from "../fixtures/auth";
import { findSeededSong } from "../fixtures/songs";

// As the plain "tester" account, cast a dance-rating vote on a seeded song, verify the
// displayed total changes, then use the existing "Undo My Changes" button
// (m4d/ClientApp/src/pages/song/components/SongCore.vue) to leave the shared sandbox instance
// clean for later tests/runs - see architecture/playwright-e2e-testing.md, "Concurrency".
const danceName = "Argentine Tango";

test("casts and undoes a dance-rating vote as the plain tester account", async ({ page }) => {
  await login(page, "tester");

  const songId = await findSeededSong(page, "ATN");
  await page.goto(`/song/details/${songId}`);

  // BListGroupItem renders as a plain ".list-group-item" div (not an <li>), one per dance -
  // scope through that wrapper using the dance name, which (unlike the vote button's aria-label)
  // stays stable across the vote-state change this test causes.
  const danceRow = page.locator(".list-group-item", { hasText: danceName });
  const voteNumber = danceRow.locator(".vote-number");
  const before = Number((await voteNumber.textContent())?.trim());

  await danceRow
    .getByRole("button", { name: /^Click here to vote for this song being danceable/ })
    .click();

  // Some dances (e.g. Argentine Tango) are danced in more than one style family - a modal asks
  // which family the vote applies to. "Vote" (no family selected) keeps this test dance-agnostic.
  const voteButton = page.getByRole("button", { name: "Vote", exact: true });
  if (await voteButton.isVisible().catch(() => false)) {
    await voteButton.click();
  }

  await page.getByRole("button", { name: "Save Changes" }).click();
  await expect(page.getByRole("button", { name: "Undo My Changes" })).toBeVisible();
  await expect(voteNumber).toHaveText(String(before + 1));

  await page.getByRole("button", { name: "Undo My Changes" }).click();
  await page.getByRole("button", { name: "YES", exact: true }).click();
  await page.waitForURL(/\/song\/details\//);

  await expect(page.getByRole("button", { name: "Undo My Changes" })).toHaveCount(0);
  await expect(voteNumber).toHaveText(String(before));
});
