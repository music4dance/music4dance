import { expect, test } from "@playwright/test";

// Cheapest possible regression guard for "the app boots and serves the real client build" -
// exactly the class of bug the wwwroot/vclient 500 and the missing Vite:Server config were
// (see architecture/contributor-test-environments.md, L1b). A blank or missing client build
// would still return 200 here but render no PageFrame chrome at all.
test.describe("smoke", () => {
  test("home page renders the real chrome", async ({ page }) => {
    const response = await page.goto("/");
    expect(response?.ok()).toBeTruthy();

    // PageFrame.vue's footer is present on every page. Exact match: the home page's own
    // content links to music4dance.net repeatedly (lowercase), so a substring match is
    // ambiguous - only the footer credit reads "Music4Dance.net" verbatim.
    await expect(page.getByRole("link", { name: "Music4Dance.net", exact: true })).toBeVisible();
    await expect(page.getByRole("link", { name: "Terms of Service" })).toBeVisible();
  });

  test("dance index lists the seeded dance catalog", async ({ page }) => {
    // Routed via "dances/{dance?}" -> DanceController.Index (M4dApplicationExtensions.cs);
    // dance-index/App.vue's own PageFrame sets title="Dance Styles", rendered as an <h1>.
    const response = await page.goto("/dances");
    expect(response?.ok()).toBeTruthy();

    await expect(page.getByRole("heading", { level: 1, name: "Dance Styles" })).toBeVisible();
  });
});
