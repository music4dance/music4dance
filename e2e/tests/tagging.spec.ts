import { expect, type Locator, test } from "@playwright/test";

import { login } from "../fixtures/auth";
import { findSeededSong } from "../fixtures/songs";

// Adds a tag on a seeded song via TagListEditor.vue, verifying it round-trips through the
// song's tag list. Uses a fabricated tag name (via TagCategorySelector's add-a-new-tag
// affordance) rather than an existing tags.json entry, so the test can't collide with a tag the
// seeded song already carries - see architecture/playwright-e2e-testing.md's Tier 1 table. Per
// CLAUDE.md, this indirectly guards that tag strings stay built via Tag.fromParts rather than
// hand-constructed, since a manual-construction regression would show up here as a tag that
// silently fails to apply.
//
// Removal is a separate test below, deliberately - see its comment for why.
const tagName = "E2ETaggingTest";

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

test("adds a tag on a seeded song", async ({ page }) => {
  await login(page, "tester");

  const songId = await findSeededSong(page, "ATN");
  await page.goto(`/song/details/${songId}`);
  // The fixed-position cookie-consent banner can overlap controls further down this long page
  // and silently swallow clicks.
  await page.getByRole("button", { name: "dismiss cookie message" }).click();

  const songTags = page.locator('[data-edit-target="song-tags"]');
  // Scoped to the song-tags block - a newly-added tag also shows up as a "button" in the
  // change-history log further down the page, which would otherwise make this ambiguous.
  const tagButton = songTags.getByRole("button", { name: tagName, exact: true });
  const editTags = songTags.getByRole("button", { name: "Edit tags" });
  const saveChanges = page.getByRole("button", { name: "Save Changes" });

  await editTags.click();
  await songTags.getByRole("button", { name: "Add Tags" }).click();
  await songTags.locator('input[type="search"]').fill(tagName);
  await songTags.getByText(`${tagName} (other)`, { exact: true }).click();
  await saveAndWaitForViewMode(saveChanges, editTags);

  await expect(tagButton).toBeVisible();
});

// KNOWN BUG, not test flakiness: removing a song-level tag doesn't persist. Confirmed by
// capturing network traffic - clicking "Remove tag" then Save Changes sends a real
// PATCH /api/song/{id} with {"name":"Tag-","value":"<tag>"} and gets back 200, but the tag is
// still present after a full page reload, so the server accepts the removal and then loses it.
//
// Traced the path (SongController.Patch -> SongIndex/Song.AppendHistory -> Song.Load ->
// LoadProperties -> Song.RemoveObjectTags -> TaggableObject.RemoveTags(string,
// DanceStatsInstance) -> ConvertToRing -> TagSummary.ChangeTags, in m4dModels/Song.cs and
// TaggableObject.cs) a good distance without finding a conclusive root cause by reading alone -
// ClearValues/TagSummary.Clean() rules out simple double-counting across requests, and
// TagManager's TagMap looks like a stable shared instance so a mismatched canonical key between
// add and remove looks unlikely too, but wasn't verified either way. Pinning this down further
// needs runtime tracing (temporary server-side logging + rerunning against this same repro), not
// more static reading.
//
// test.fail(): this test asserts the CORRECT behavior (tag actually gone) and is expected to
// fail today - if someone fixes the underlying bug, this test starts unexpectedly passing, which
// Playwright reports as a failure, flagging that this annotation (and comment) should come out.
test("removes a previously-added tag from a seeded song", async ({ page }) => {
  test.fail();

  await login(page, "tester");

  const songId = await findSeededSong(page, "ATN");
  await page.goto(`/song/details/${songId}`);
  await page.getByRole("button", { name: "dismiss cookie message" }).click();

  const songTags = page.locator('[data-edit-target="song-tags"]');
  const tagButton = songTags.getByRole("button", { name: tagName, exact: true });
  const editTags = songTags.getByRole("button", { name: "Edit tags" });
  const saveChanges = page.getByRole("button", { name: "Save Changes" });

  await editTags.click();
  await songTags.getByRole("button", { name: "Add Tags" }).click();
  await songTags.locator('input[type="search"]').fill(tagName);
  await songTags.getByText(`${tagName} (other)`, { exact: true }).click();
  await saveAndWaitForViewMode(saveChanges, editTags);
  await expect(tagButton).toBeVisible();

  await editTags.click();
  await songTags
    .locator(".list-inline-item", { hasText: tagName })
    .getByRole("button", { name: "Remove tag" })
    .click();
  await saveAndWaitForViewMode(saveChanges, editTags);

  await expect(tagButton).toHaveCount(0);
});
