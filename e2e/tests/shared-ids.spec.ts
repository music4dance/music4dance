import { expect, type Locator, type Page, test } from "@playwright/test";

import { login } from "../fixtures/auth";

// The shared-id review (/SharedIds, the shared-id-review page app): songs that hold the same
// service id are queued in clusters, and the reviewer merges them or marks them not the same.
// See architecture/songs/shared-id-review.md.
//
// Each test makes its own pair of songs that share an ISRC, under a tag unique to the run:
// POST /api/song creates the songs with different Spotify ids, then PATCH /api/song gives the
// second the first's ISRC. The edit doesn't pass through the enrichment guard (only enrichment
// does), so nothing is queued until Queue existing shared ids scans the index.

interface Pair {
  run: string;
  isrc: string;
  first: string;
  second: string;
}

async function antiforgeryToken(page: Page): Promise<string> {
  const { token } = await (await page.request.get("/api/antiforgery/token")).json();
  return token;
}

function properties(entries: [string, string][]) {
  return entries.map(([name, value]) => ({ name, value }));
}

// The search drops songs with no purchase id or no confirmed dance rating (SongIndexLocal's
// PassesCruft), so each song carries both
async function createSong(page: Page, title: string, spotifyId: string, time: string, isrc?: string) {
  const response = await page.request.post("/api/song", {
    data: {
      id: crypto.randomUUID(),
      properties: properties([
        [".Create", ""],
        ["User", "admin"],
        ["Time", time],
        ["Title", title],
        ["Artist", "Shared Id Artist"],
        ["Album:00", `${title} Album`],
        ["Purchase:00:SS", spotifyId],
        ...(isrc ? ([["Purchase:00:RS", isrc]] as [string, string][]) : []),
        ["Tag+", "Salsa:Dance"],
        ["DanceRating", "SLS+1"],
      ]),
    },
    headers: { RequestVerificationToken: await antiforgeryToken(page) },
  });
  expect(response.status(), await response.text()).toBe(200);
}

// POST /api/song assigns its own song id, so the song is found through the search
async function findSongId(page: Page, title: string): Promise<string> {
  await page.goto(`/song/advancedsearch?searchString=${encodeURIComponent(title)}`);
  const link = page.locator('a[href*="/song/details/"]').first();
  await expect(link).toBeVisible();
  const match = (await link.getAttribute("href"))?.match(/\/song\/details\/([^/?]+)/i);
  if (!match) {
    throw new Error(`No song found for "${title}"`);
  }
  return match[1]!;
}

async function makePair(page: Page, name: string): Promise<Pair> {
  const run = `${Date.now()}`;
  const isrc = `USE2E${run}`;
  const pair = { run, isrc, first: `${name} One ${run}`, second: `${name} Two ${run}` };
  await createSong(page, pair.first, `e2e${run}a`, "01/01/2020 10:00:00 AM", isrc);
  await createSong(page, pair.second, `e2e${run}b`, "01/02/2020 10:00:00 AM");

  const second = await findSongId(page, pair.second);
  const response = await page.request.patch(`/api/song/${second}`, {
    data: {
      id: second,
      properties: properties([
        [".Edit", ""],
        ["User", "admin"],
        ["Time", "01/03/2020 10:00:00 AM"],
        ["Purchase:00:RS", isrc],
      ]),
    },
    headers: { RequestVerificationToken: await antiforgeryToken(page) },
  });
  expect(response.status(), await response.text()).toBe(200);
  return pair;
}

// Queue existing shared ids, which runs in the background and lands on /Admin/AdminStatus
async function seed(page: Page): Promise<void> {
  await page.goto("/SharedIds");
  page.once("dialog", (dialog) => dialog.accept());
  await page.getByRole("button", { name: "Queue existing shared ids" }).click();
  await page.waitForURL(/\/Admin\/AdminStatus/i);
}

function clusterCard(page: Page, isrc: string): Locator {
  return page.locator(".card").filter({ has: page.locator(".card-header", { hasText: isrc }) });
}

// The cluster's card, on whichever page of the queue it's on. The seed runs in the background,
// so this waits for it to show up
async function openCluster(page: Page, isrc: string): Promise<Locator> {
  const card = clusterCard(page, isrc);
  await expect(async () => {
    await page.goto("/SharedIds");
    const pages = await page.locator(".pagination .page-item").count();
    for (let p = 1; p <= Math.max(1, pages); p++) {
      if (p > 1) {
        await page.goto(`/SharedIds?page=${p}`);
      }
      if ((await card.count()) > 0) {
        return;
      }
    }
    throw new Error(`The cluster for ${isrc} isn't queued yet`);
  }).toPass({ timeout: 30_000 });
  return card;
}

async function apply(page: Page): Promise<void> {
  await page.getByRole("button", { name: "Apply this page" }).first().click();
  await page.waitForURL(/\/SharedIds\?page=\d+&message=/);
}

async function isQueued(page: Page, isrc: string): Promise<boolean> {
  await page.goto("/SharedIds");
  const pages = await page.locator(".pagination .page-item").count();
  for (let p = 1; p <= Math.max(1, pages); p++) {
    if (p > 1) {
      await page.goto(`/SharedIds?page=${p}`);
    }
    if ((await clusterCard(page, isrc).count()) > 0) {
      return true;
    }
  }
  return false;
}

test.beforeEach(async ({ page }) => {
  await login(page, "admin");
});

test("Not the same takes the pair out of the queue for good", async ({ page }) => {
  const pair = await makePair(page, "Shared Reject");
  await seed(page);

  const card = await openCluster(page, pair.isrc);
  await expect(card).toContainText(pair.first);
  await expect(card).toContainText(pair.second);
  await card.getByLabel("Not the same").check();
  await apply(page);

  await expect(page.getByText(/marked 1 not the same/)).toBeVisible();
  expect(await isQueued(page, pair.isrc)).toBe(false);

  // A rejected pair isn't queued again
  await seed(page);
  await expect(async () => {
    expect(await isQueued(page, pair.isrc)).toBe(false);
  }).toPass({ timeout: 10_000 });
});

test("Merge groups merges the pair under the title picked from the first song", async ({
  page,
}) => {
  const pair = await makePair(page, "Shared Merge");
  await seed(page);

  const card = await openCluster(page, pair.isrc);
  // No rejected pair, so the cluster starts as a merge. The later song's title wins a merge, so
  // picking the first song's makes Apply set it after the merge
  await expect(card.getByLabel("Merge groups")).toBeChecked();
  await card.getByRole("row", { name: /^Title/ }).getByLabel(pair.first).check();
  await apply(page);

  await expect(page.getByText(/Merged 1,/)).toBeVisible();
  expect(await isQueued(page, pair.isrc)).toBe(false);

  // One song is left, holding both songs' albums, under the picked title
  await page.goto(`/song/advancedsearch?searchString=${pair.run}`);
  const links = page.locator('a[href*="/song/details/"]');
  await expect(links.first()).toBeVisible();
  const hrefs = await links.evaluateAll((all) => all.map((a) => a.getAttribute("href")));
  expect(new Set(hrefs).size).toBe(1);
  await page.goto(hrefs[0]!);
  await expect(page.getByRole("heading", { level: 1 })).toContainText(pair.first);
});
