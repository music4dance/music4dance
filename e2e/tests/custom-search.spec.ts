import { expect, type Page, test } from "@playwright/test";

// Builds a dance+tag filter through the real advanced-search UI (DanceSelector +
// TagQuerySelector, both backed by DanceQueryItem/Tag.fromParts - see CLAUDE.md's "Filter / Tag
// Construction") and asserts the resulting /song/filtersearch?filter= URL actually carries that
// shape. SongFilter.encodedQuery (m4d/ClientApp/src/models/SongFilter.ts) is a plain
// percent-encoded hyphen-delimited string, not an opaque blob, so a manual-construction
// regression in the UI would show up here as a literally wrong query string, not just a passing
// unit test on the class library itself.

// DanceSelector, TagQuerySelector's Include/Exclude, and TagListEditor's "Add Tags" are all thin
// wrappers around TagSelector.vue, whose root BDropdown carries a shared "tag-dropdown" class -
// closed instances stay in the DOM (Playwright's ":visible" can't tell them apart, since
// bootstrap-vue-next's own close animation doesn't reliably zero out offsetWidth/offsetHeight),
// so scope by the specific toggle button's own dropdown wrapper instead.
function tagDropdown(page: Page, buttonName: string) {
  return page.locator(".tag-dropdown", { has: page.getByRole("button", { name: buttonName }) });
}

test("builds a dance+tag filter through the custom search UI", async ({ page }) => {
  await page.goto("/song/advancedsearchform");
  // The fixed-position cookie-consent banner can overlap controls further down the form (the
  // Song Tags section, once a dance chip has been added above it) and silently swallow clicks.
  await page.getByRole("button", { name: "dismiss cookie message" }).click();

  const dances = tagDropdown(page, "Choose Dances");
  await dances.getByRole("button", { name: "Choose Dances" }).click();
  await dances.locator('input[type="search"]').fill("argentine tango");
  await dances.getByText("Argentine Tango (Tango Argentino)", { exact: true }).click();
  // Give the Dances dropdown's own close/focus-trap teardown a moment to finish before opening
  // another one - starting that teardown and this dropdown's open in the same tick has caused
  // the newly-opened menu to be immediately torn down too.
  await expect(dances.getByRole("button", { name: "Choose Dances" })).toHaveAttribute(
    "aria-expanded",
    "false",
  );
  // aria-expanded flips before the dropdown's own focus-trap teardown/animation actually
  // finishes - opening the next dropdown in that window has intermittently torn the new one
  // down again (see the ".tag-dropdown" comment above).
  await page.waitForTimeout(300);

  const includeTags = tagDropdown(page, "Choose Tags to Include");
  await includeTags.getByRole("button", { name: "Choose Tags to Include" }).click();
  await includeTags.locator('input[type="search"]').fill("pop");
  await includeTags.getByText("Pop (musical genre)", { exact: true }).click();
  await expect(includeTags.getByRole("button", { name: "Choose Tags to Include" })).toHaveAttribute(
    "aria-expanded",
    "false",
  );
  await page.waitForTimeout(300);

  // #sort is `required`, and its "Default: ..." option binds to a null value - HTML5 validation
  // treats that as unselected, so the form silently refuses to submit until a real option is
  // chosen.
  await page.locator("#sort").selectOption({ label: "Dance Rating (Argentine Tango)" });

  // The Submit click occasionally doesn't register as a real form submission (BForm's
  // :validated re-render appears to occasionally swap the button out from under the click) -
  // retry rather than fail the whole filter-construction test on that unrelated flake.
  const submit = page.getByRole("button", { name: "Submit", exact: true });
  await expect(async () => {
    await submit.click();
    await expect(page).toHaveURL(/\/song\/filtersearch\?filter=/, { timeout: 3_000 });
  }).toPass({ timeout: 20_000 });

  const url = new URL(page.url());
  const filter = decodeURIComponent(url.searchParams.get("filter") ?? "");
  expect(filter).toContain("ATN");
  expect(filter).toContain("+Pop:Music");

  await expect(page.getByRole("link", { name: "Ex's & Oh's" })).toBeVisible();
});
