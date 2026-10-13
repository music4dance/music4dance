import { expect, type Locator, type Page, test } from "@playwright/test";

import { login } from "../fixtures/auth";
import { findSeededSong } from "../fixtures/songs";

// Adds and removes tags on a seeded song via TagListEditor.vue, verifying they round-trip through
// the song's tag list. Uses fabricated tag names (via TagCategorySelector's add-a-new-tag
// affordance) rather than existing tags.json entries, so the tests can't collide with a tag the
// seeded song already carries - see architecture/dev-testing/playwright-e2e-testing.md's Tier 1
// table. Per CLAUDE.md, this indirectly guards that tag strings stay built via Tag.fromParts rather
// than hand-constructed, since a manual-construction regression would show up here as a tag that
// silently fails to apply.
//
// Each test uses its own unique tag name: m4d.Sandbox is one shared in-memory process (and is
// reused across local runs), so a fixed name would already be one of tester's own tags on the
// next test or run - TagCategorySelector then (correctly) stops offering it to add.
const uniqueTagName = () => `E2ETag${Date.now()}`;

// saveChanges() is an async AJAX round-trip before `edit` flips back to false and the pencil
// icon reappears - the Save Changes click has occasionally not registered as a real click (the
// same button-swapped-out-from-under-the-click flake seen on the advanced-search form's Submit
// button), so retry the click itself rather than just waiting longer for its effect.
async function saveAndWaitForViewMode(saveChanges: Locator, editTags: Locator) {
  await expect(async () => {
    await saveChanges.click();
    await expect(editTags).toBeVisible({ timeout: 3_000 });
  }).toPass({ timeout: 20_000 });
}

async function openSongDetails(page: Page, songId: string) {
  await page.goto(`/song/details/${songId}`);
  // The fixed-position cookie-consent banner can overlap controls further down this long page
  // and silently swallow clicks. It only shows until dismissed, so it may be gone on a reload.
  const dismiss = page.getByRole("button", { name: "dismiss cookie message" });
  if (await dismiss.isVisible()) {
    await dismiss.click();
  }

  const songTags = page.locator('[data-edit-target="song-tags"]');
  return {
    songTags,
    editTags: songTags.getByRole("button", { name: "Edit tags" }),
    saveChanges: page.getByRole("button", { name: "Save Changes" }),
    // Scoped to the song-tags block - a newly-added tag also shows up as a "button" in the
    // change-history log further down the page, which would otherwise make this ambiguous.
    tagButton: (tagName: string) => songTags.getByRole("button", { name: tagName, exact: true }),
  };
}

async function addTag(page: Page, songId: string, tagName: string) {
  const { songTags, editTags, saveChanges, tagButton } = await openSongDetails(page, songId);

  await editTags.click();
  await songTags.getByRole("button", { name: "Add Tags" }).click();
  await songTags.locator('input[type="search"]').fill(tagName);
  await songTags.getByText(`${tagName} (other)`, { exact: true }).click();
  await saveAndWaitForViewMode(saveChanges, editTags);

  await expect(tagButton(tagName)).toBeVisible();
}

test("adds a tag on a seeded song", async ({ page }) => {
  await login(page, "tester");

  const songId = await findSeededSong(page, "ATN");
  await addTag(page, songId, uniqueTagName());
});

// Removes the tag after a full page reload, which is the path that used to break: tester is a
// private (Privacy == 0) account, and the server anonymized the viewer's own history entries to
// their user id, so after a reload their own tag showed up under "Others' Tags" - not removable,
// and offered to add again (a second Tag+ that one Tag- then couldn't cancel out).
test("removes a previously-added tag from a seeded song", async ({ page }) => {
  await login(page, "tester");

  const songId = await findSeededSong(page, "ATN");
  const tagName = uniqueTagName();
  await addTag(page, songId, tagName);

  const { songTags, editTags, saveChanges, tagButton } = await openSongDetails(page, songId);
  await editTags.click();
  await songTags
    .locator(".list-inline-item", { hasText: tagName })
    .getByRole("button", { name: "Remove tag" })
    .click();
  await saveAndWaitForViewMode(saveChanges, editTags);
  await expect(tagButton(tagName)).toHaveCount(0);

  await page.reload();
  await expect(songTags.getByRole("button", { name: "Edit tags" })).toBeVisible();
  await expect(tagButton(tagName)).toHaveCount(0);
});
