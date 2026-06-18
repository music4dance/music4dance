---
name: update-yarn
description: >-
    Updates the Yarn-managed npm packages in m4d/ClientApp (the Vue/TypeScript
    frontend), then verifies type-check, lint, and tests still pass. Use when
    the user asks to update, bump, or upgrade npm/yarn/frontend/client
    packages or dependencies.
---

# update-yarn

Updates dependencies in `m4d/ClientApp/package.json`, then verifies the
client still type-checks, lints, and passes tests.

This project uses **Yarn (Berry, `yarn@4.9.2`)**, not npm — always use `yarn`
commands. `nodeLinker` is `pnpm` (see `m4d/ClientApp/.yarnrc.yml`), so there
is no traditional `node_modules` symlink layout to worry about, but always
run `yarn install` after editing `package.json` directly.

All commands below run from `m4d/ClientApp/`.

## Procedure

1. **See what's outdated:**

   ```sh
   yarn outdated
   ```

   This lists current/wanted/latest versions per package.

2. **Classify before applying anything:**

   - **Patch/minor bumps** — safe to apply directly with `yarn up`.
   - **Major bumps** — flag to the user before applying. Pay particular
     attention to `vue`, `vite`, `vue-tsc`, `typescript`, `eslint`, and the
     `@vue/*`/`@typescript-eslint/*` families — these are tightly
     interdependent and a major bump on one often requires bumping several
     together (check each package's peerDependencies/changelog before
     jumping).
   - **`resolutions` entries** (`strip-ansi`, `string-width`, `wrap-ansi` in
     `package.json`) — these are pinned overrides, likely for compatibility
     with a transitive dependency. Don't bump them without checking why
     they're pinned; ask the user if a bump seems warranted.

3. **Apply updates** with `yarn up` (Yarn Berry's upgrade command), one
   package (or a related group) at a time so failures are easy to bisect:

   ```sh
   yarn up <package>@<range>
   ```

   For a batch of same-risk patch/minor bumps you can pass multiple packages
   to one `yarn up` call.

4. **Install** to make sure the lockfile and resolved tree are consistent:

   ```sh
   yarn install
   ```

5. **Type-check and build:**

   ```sh
   yarn build
   ```

   (`build` runs `type-check` via `vue-tsc --build --force` then
   `build-only` via Vite — this is the most reliable signal that a major bump
   broke type compatibility.)

6. **Lint:**

   ```sh
   yarn lint
   ```

7. **Run client tests** — always with `--run`, never bare `yarn test:unit`
   (which watches):

   ```sh
   yarn vitest --run
   ```

8. **Report**: list what was updated, what was flagged/skipped (majors,
   pinned resolutions), and confirm build/lint/test status.

## Notes

- Don't touch `m4d/ClientApp/yarn.lock` by hand — only via `yarn up` /
  `yarn install`.
- LF line endings are enforced on `m4d/ClientApp/**` via `.gitattributes`;
  `yarn up` won't touch source files, so this only matters if a dependency
  bump requires you to hand-edit a config file.
