import { expect, type Page, test } from "@playwright/test";

import { login } from "../fixtures/auth";

// Merging songs from a song list (AdminFooter's Multi-Edit → Merge): SongIndex.SimpleMergeSongs
// weaves both histories together, then the merged song opens in the editor, where each key field
// offers the other values its history holds (FieldEditor's alternatives). See
// architecture/songs/song-merge-algorithm.md § Settling the fields.
//
// A merge deletes the songs it merges, so the test makes two songs of its own, under a title
// unique to the run, instead of merging seeded ones, and deletes the merged song at the end.

interface TestSong {
  artist: string;
  tempo: number;
  album: string;
  time: string;
}

// The search drops songs with no purchase id or no confirmed dance rating (SongIndexLocal's
// PassesCruft), so each song carries both. The purchase ids differ, or the second add would be
// merged into the first as the same Spotify track.
async function createSong(page: Page, title: string, song: TestSong, spotifyId: string) {
  const properties = [
    [".Create", ""],
    ["User", "admin"],
    ["Time", song.time],
    ["Title", title],
    ["Artist", song.artist],
    ["Tempo", song.tempo.toFixed(1)],
    ["Album:00", song.album],
    ["Purchase:00:SS", spotifyId],
    ["Tag+", "Salsa:Dance"],
    ["DanceRating", "SLS+1"],
  ].map(([name, value]) => ({ name, value }));

  // The API validates antiforgery tokens; the client fetches one the same way
  // (ClientApp/src/helpers/AntiforgeryToken.ts).
  const { token } = await (await page.request.get("/api/antiforgery/token")).json();
  const response = await page.request.post("/api/song", {
    data: { id: crypto.randomUUID(), properties },
    headers: { RequestVerificationToken: token },
  });
  expect(response.status(), await response.text()).toBe(200);
}

test("merges two songs and picks the merged song's fields from either one", async ({ page }) => {
  await login(page, "admin");

  const run = Date.now().toString(36);
  const title = `Merge Test ${run}`;
  // The later song's values win the merge, so the test picks the earlier song's values back.
  const first: TestSong = {
    artist: "First Artist",
    tempo: 117,
    album: "First Album",
    time: "01/01/2020 10:00:00 AM",
  };
  const second: TestSong = {
    artist: "Second Artist",
    tempo: 123,
    album: "Second Album",
    time: "01/02/2020 10:00:00 AM",
  };
  await createSong(page, title, first, `e2e${run}a`);
  await createSong(page, title, second, `e2e${run}b`);

  await page.goto(`/song/advancedsearch?searchString=${encodeURIComponent(title)}`);
  for (const song of [first, second]) {
    await page.getByRole("row").filter({ hasText: song.artist }).getByRole("checkbox").check();
  }
  await page.getByRole("button", { name: "Multi-Edit" }).click();
  await page.getByRole("menuitem", { name: "Merge", exact: true }).click();

  // The merge lands on the merged song, already in the editor (RedirectToAction spells it
  // /Song/Details/{id}?edit=True)
  await page.waitForURL(/\/song\/details\/[^?]+\?edit=true/i);
  const titleInput = page.locator('input[data-field-name="Title"]');
  await expect(titleInput).toHaveValue(title);
  await expect(page.locator('input[data-field-name="Artist"]')).toHaveValue(second.artist);

  await page.getByRole("button", { name: first.artist, exact: true }).click();
  await page.getByRole("button", { name: String(first.tempo), exact: true }).click();
  await page.getByRole("button", { name: "Save Changes" }).click();
  await expect(titleInput).toHaveCount(0);

  // Reload, without ?edit, so the checks read what the server stored, not the editor's own state
  const songUrl = new URL(page.url());
  await page.goto(songUrl.pathname);
  const heading = page.getByRole("heading", { level: 1 });
  await expect(heading).toContainText(title);
  await expect(heading).toContainText(first.artist);
  await expect(page.getByRole("row").filter({ hasText: "Tempo" })).toContainText(
    String(first.tempo),
  );
  // Each song had its own album at index 00; the merge keeps both
  await expect(page.getByText(first.album, { exact: true })).toBeVisible();
  await expect(page.getByText(second.album, { exact: true })).toBeVisible();

  // Leave the shared sandbox as it was
  const songId = songUrl.pathname.split("/").pop();
  await page.goto(`/song/delete?id=${songId}`);
  await page.getByRole("button", { name: "Delete" }).click();
  await page.goto(`/song/advancedsearch?searchString=${encodeURIComponent(title)}`);
  await expect(page.getByRole("row").filter({ hasText: title })).toHaveCount(0);
});
