import { expect, type Page, test } from "@playwright/test";

import { login } from "../fixtures/auth";
import { findSeededSong } from "../fixtures/songs";

// As the plain "tester" account, cast a dance-rating vote on a seeded song, verify the
// displayed total changes, then use the existing "Undo My Changes" button
// (m4d/ClientApp/src/pages/song/components/SongCore.vue) to leave the shared sandbox instance
// clean for later tests/runs - see architecture/dev-testing/playwright-e2e-testing.md,
// "Concurrency".
const danceName = "Argentine Tango";

// BListGroupItem renders as a plain ".list-group-item" div (not an <li>), one per dance -
// scope through that wrapper using the dance name, which (unlike the vote button's aria-label)
// stays stable across the vote-state change these tests cause.
const danceRow = (page: Page) => page.locator(".list-group-item", { hasText: danceName });

async function castUpVote(page: Page) {
  await danceRow(page)
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
}

async function undoMyChanges(page: Page) {
  await page.getByRole("button", { name: "Undo My Changes" }).click();
  await page.getByRole("button", { name: "YES", exact: true }).click();
  await page.waitForURL(/\/song\/details\//);
  await expect(page.getByRole("button", { name: "Undo My Changes" })).toHaveCount(0);
}

test("casts and undoes a dance-rating vote as the plain tester account", async ({ page }) => {
  await login(page, "tester");

  const songId = await findSeededSong(page, "ATN");
  await page.goto(`/song/details/${songId}`);

  const voteNumber = danceRow(page).locator(".vote-number");
  const before = Number((await voteNumber.textContent())?.trim());

  await castUpVote(page);
  await expect(voteNumber).toHaveText(String(before + 1));

  await undoMyChanges(page);
  await expect(voteNumber).toHaveText(String(before));
});

// The client finds "your" vote by matching history users against its own user name, so this
// reloads and checks the vote still reads as tester's own. tester is a private (Privacy == 0)
// account, whose own history entries the server used to anonymize to their user id - which made
// a saved vote look like someone else's after a reload.
test("shows a saved dance-rating vote as the user's own after a reload", async ({ page }) => {
  await login(page, "tester");

  const songId = await findSeededSong(page, "ATN");
  await page.goto(`/song/details/${songId}`);

  const voteNumber = danceRow(page).locator(".vote-number");
  const before = Number((await voteNumber.textContent())?.trim());

  await castUpVote(page);

  await page.reload();
  await expect(
    danceRow(page).getByRole("button", {
      name: `You have voted that this song is danceable to ${danceName}`,
    }),
  ).toBeVisible();
  await expect(voteNumber).toHaveText(String(before + 1));

  await undoMyChanges(page);
  await expect(voteNumber).toHaveText(String(before));
});
