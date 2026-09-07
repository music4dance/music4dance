import { expect, test } from "@playwright/test";

import { credentialsFor, login, type SandboxTier } from "../fixtures/auth";

// MainMenu.vue renders `{{ context.userName }}` when MenuContext.userName is set (and a
// "Login" nav item otherwise), and gates a whole "Admin" nav dropdown (#admin-menu) on
// `context.isAdmin`. Both come from the server-provided MenuContext, so this is a real check
// that the seeded accounts' roles (architecture/contributor-test-environments.md, L1d) actually
// gate the UI, not just that the accounts exist.
const tiers: SandboxTier[] = ["admin", "editor", "tester"];

test.describe("auth", () => {
  for (const tier of tiers) {
    test(`logs in as the seeded ${tier} account`, async ({ page }) => {
      await login(page, tier);

      // Scoped to the account dropdown specifically (MainMenu.vue's
      // <BNavItemDropdown id="account-menu">), not the whole navbar - a bare #mainMenu-wide text
      // search for the username is ambiguous, e.g. "admin" also matches the "Admin" nav dropdown
      // and its "Admin Search" item via Playwright's case-insensitive substring match.
      const accountMenu = page.locator("#account-menu");
      const { userName } = credentialsFor(tier);
      await expect(accountMenu.getByText(userName, { exact: false })).toBeVisible();

      const adminMenu = page.locator("#admin-menu");
      if (tier === "admin") {
        await expect(adminMenu).toBeVisible();
      } else {
        await expect(adminMenu).toHaveCount(0);
      }
    });
  }
});
