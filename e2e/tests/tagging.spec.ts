import { test } from "@playwright/test";

// TODO: implement once TagListEditor.vue's markup has been inspected against a running
// m4d.Sandbox. Add and remove a tag on a seeded song and verify it round-trips through
// TagListEditor's state - see architecture/playwright-e2e-testing.md's Tier 1 table. Per
// CLAUDE.md, this indirectly guards that tag strings stay built via Tag.fromParts rather than
// hand-constructed, since a manual-construction regression would show up here as a tag that
// silently fails to apply.
test.fixme("adds and removes a tag on a seeded song", async ({ page }) => {
  void page;
});
