import type { Page } from "@playwright/test";

// The three seeded privilege tiers from architecture/contributor-test-environments.md (L1d):
// admin (canTag, canEdit, showDiagnostics, dbAdmin), editor (canEdit only), and tester (no
// roles at all - an ordinary authenticated user).
export type SandboxTier = "admin" | "editor" | "tester";

interface Credentials {
  userName: string;
  password: string;
}

// Defaults match the seeded values in m4d.Sandbox/appsettings.json. Override via env vars
// (same names m4d.Sandbox itself reads) if a contributor's sandbox is configured differently.
const CREDENTIALS: Record<SandboxTier, Credentials> = {
  admin: {
    userName: process.env.M4D_ADMIN_USER ?? "admin",
    password: process.env.M4D_ADMIN_PASSWORD ?? "Sandbox!Admin1",
  },
  editor: {
    userName: process.env.M4D_EDITOR_USER ?? "editor",
    password: process.env.M4D_EDITOR_PASSWORD ?? "Sandbox!Editor1",
  },
  tester: {
    userName: process.env.M4D_TEST_USER ?? "tester",
    password: process.env.M4D_TEST_PASSWORD ?? "Sandbox!Test1",
  },
};

export function credentialsFor(tier: SandboxTier): Credentials {
  return CREDENTIALS[tier];
}

// Logs in through the real Identity login page
// (m4d/Areas/Identity/Pages/Account/Login.cshtml[.cs]) - no API shortcut, so this exercises the
// same form a real user submits. Field labels and the submit button id are read directly from
// that page's source, not guessed:
//   - Input.UserName -> [Display(Name = "User Name or Email")]
//   - Input.Password -> label falls back to the property name, "Password"
//   - <button id="login-submit">Log in</button>
export async function login(page: Page, tier: SandboxTier): Promise<void> {
  const { userName, password } = CREDENTIALS[tier];

  await page.goto("/Identity/Account/Login");
  await page.getByLabel("User Name or Email").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.locator("#login-submit").click();
  await page.waitForURL((url) => !url.pathname.startsWith("/Identity/Account/Login"));
}
