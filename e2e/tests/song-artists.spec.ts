import { expect, test } from "@playwright/test";

import { login } from "../fixtures/auth";
import { artistLinks, contentHeading, findSplitSong } from "../fixtures/artists";

// Individual artists on the song-details page: the credit renders verbatim with each derived
// artist linked inside it (ArtistCredit.vue), and privileged users can correct the derived list
// (ArtistsEditor.vue). See architecture/individual-artists.md §8.
test("links each individual artist inside the credit", async ({ page }) => {
  const song = await findSplitSong(page);
  await page.goto(song);

  const links = artistLinks(page);
  await expect(links.first()).toBeVisible();
  expect(await links.count()).toBeGreaterThan(1);

  // Following one lands on that artist's page, not the whole credit's
  const name = (await links.first().textContent())!.trim();
  await links.first().click();
  await page.waitForURL(/\/song\/artist\?/);
  await expect(contentHeading(page, 1)).toContainText(name);
});

test("edits and restores the individual artist list as a canEdit account", async ({ page }) => {
  await login(page, "editor");

  const song = await findSplitSong(page);
  await page.goto(song);

  // Count in view mode: editing swaps the linked credit for a plain input (FieldEditor.vue)
  const before = await artistLinks(page).count();
  expect(before).toBeGreaterThan(1);

  await page.getByRole("button", { name: "Edit", exact: true }).click();
  const editor = page.locator('[data-edit-target="song-artists"]');
  await expect(editor).toBeVisible();

  // BFormTags renders one remove control per artist; dropping one should drop a link
  await editor.getByRole("button", { name: /Remove/ }).first().click();
  await page.getByRole("button", { name: "Save Changes" }).click();
  await expect(page.getByRole("button", { name: "Undo My Changes" })).toBeVisible();

  expect(await artistLinks(page).count()).toBeLessThan(before);

  // "Reset to automatic" hands the list back to the splitter (ArtistsEditor.vue) - the feature's
  // own restore path, and what returns this shared sandbox song to its seeded state. It only
  // appears once the list is User-sourced, so its presence also confirms the edit was recorded
  // as a human edit rather than a bot one.
  await page.getByRole("button", { name: "Edit", exact: true }).click();
  await editor.getByRole("button", { name: "Reset to automatic" }).click();
  await page.getByRole("button", { name: "Save Changes" }).click();

  // Reload rather than trusting the client's own state: re-splitting happens in the save hook on
  // the server (SongIndex.UpdateArtists), so only a round-trip shows what was actually stored.
  await page.reload();
  await expect(artistLinks(page)).toHaveCount(before);
});

// canEdit opens the Artist credit but not the Title, which is still dbAdmin-only - the seeded
// "editor" account holds canEdit and nothing else, so it tells those two apart.
test("lets a canEdit account edit the credit but not the title", async ({ page }) => {
  await login(page, "editor");

  const song = await findSplitSong(page);
  await page.goto(song);
  await page.getByRole("button", { name: "Edit", exact: true }).click();

  await expect(page.locator('input[data-field-name="Artist"]')).toBeVisible();
  await expect(page.locator('input[data-field-name="Title"]')).toBeHidden();
});

test("shows an ordinary user the artist links but no way to edit them", async ({ page }) => {
  await login(page, "tester");

  const song = await findSplitSong(page);
  await page.goto(song);

  await expect(artistLinks(page).first()).toBeVisible();
  await expect(page.getByRole("button", { name: "Edit", exact: true })).toBeHidden();
  await expect(page.locator('[data-edit-target="song-artists"]')).toBeHidden();
});
