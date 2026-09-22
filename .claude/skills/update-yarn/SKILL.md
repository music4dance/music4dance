---
name: update-yarn
description: >-
    Updates the Yarn-managed npm packages in m4d/ClientApp (the Vue/TypeScript
    frontend), verifies type-check, lint, and the full client test suite still
    pass, then branches, commits, and opens a PR. Use when the user asks to
    update, bump, or upgrade npm/yarn/frontend/client packages or
    dependencies.
---

# update-yarn

Updates dependencies in `m4d/ClientApp/package.json`, verifies the client
still type-checks, lints, and passes its full test suite, then lands the
change on a branch as a PR.

This project uses **Yarn (Berry, `yarn@4.18.0`)**, not npm — always use `yarn`
commands. `nodeLinker` is `pnpm` (see `m4d/ClientApp/.yarnrc.yml`), so there
is no traditional `node_modules` symlink layout to worry about, but always
run `yarn install` after editing `package.json` directly.

All commands below run from `m4d/ClientApp/`.

## Scope argument

This skill accepts an optional argument: `minor` or `major`.

- `/update-yarn` (no argument) — apply patch/minor bumps directly; list
  major bumps and ask before applying them (default behavior below).
- `/update-yarn minor` — apply **only** patch/minor bumps. Skip major bumps
  entirely (don't even ask) and mention them in the final report as
  available-but-not-applied.
- `/update-yarn major` — review **only** the packages with a major version
  available. Confirm with the user before applying each (or a related
  group), and leave patch/minor bumps untouched for a separate pass.

Pinned `resolutions` entries are never auto-applied under either mode — they
always require explicit confirmation regardless of argument.

## Procedure

1. **See what's outdated.** Yarn Berry has **no `yarn outdated` command**
   (it errors with `Couldn't find a script named "outdated"`), and
   `yarn upgrade-interactive` is interactive, so it's unusable here. Query
   the registry directly instead:

   ```sh
   node -e '
   const pkg = require("./package.json");
   const deps = { ...pkg.dependencies, ...pkg.devDependencies };
   Promise.all(Object.entries(deps).map(async ([n, cur]) => {
     const r = await fetch(`https://registry.npmjs.org/${n}/latest`);
     const latest = (await r.json()).version;
     return [n, cur, latest];
   })).then((rows) => rows
     .filter(([, cur, latest]) => cur.replace(/^[\^~]/, "") !== latest)
     .sort((a, b) => a[0].localeCompare(b[0]))
     .forEach((r) => console.log(r.join(" -> "))));
   '
   ```

   Occasional `fetch failed` rows are transient — just re-query those few
   packages rather than assuming they're current.

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

3. **Apply updates per the scope argument** with `yarn up` (Yarn Berry's
   upgrade command), one package (or a related group) at a time so failures
   are easy to bisect:

   ```sh
   yarn up <package>@<range>
   ```

   - No argument or `minor`: apply patch/minor bumps directly.
   - No argument: also ask about major bumps and apply confirmed ones.
   - `minor`: skip major bumps entirely, no need to ask.
   - `major`: apply only the confirmed major bumps; leave patch/minor alone.

   For a batch of same-risk patch/minor bumps you can pass multiple packages
   to one `yarn up` call.

   **Quarantined versions**: `yarn up` can fail the whole batch with
   `All versions satisfying "X" are quarantined` — npm holds some freshly
   published versions back. Nothing is applied when this happens, so drop
   to the previous stable release of that one package and re-run the batch
   (e.g. `@typescript-eslint/utils@8.70.1` quarantined → use `8.70.0`).
   Note it in the final report so the next pass can retry.

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

   **This repo has pre-existing lint errors** (~28, mostly
   `@typescript-eslint/no-explicit-any` and `no-unused-vars`). A non-zero
   exit is therefore *not* by itself a failure — what matters is whether the
   bumps made it worse. Compare the error count against the pre-update
   baseline before concluding anything:

   ```sh
   git stash push -- package.json yarn.lock
   yarn install && yarn eslint src   # baseline count
   git stash pop
   yarn install
   ```

   Same error count = pre-existing debt, proceed. A *higher* count means a
   bump introduced it — fix or revert that bump. New *warnings* from a
   plugin bump are fine to accept, but say so in the report.

   Note `yarn lint` runs `eslint --fix`, so it can rewrite source files.
   Check `git status` afterwards (step 8).

7. **Run the full client test suite** — always with `--run`, never bare
   `yarn test:unit` (which watches):

   ```sh
   yarn vitest --run
   ```

   Run the whole suite, not a subset: a dependency bump can break a page
   nowhere near the packages you touched. All tests must pass before you
   commit. If any fail, bisect to the offending bump and either revert it or
   confirm the fix with the user — **do not open a PR on a red suite.**

8. **Check what actually changed** before committing:

   ```sh
   git status --short
   ```

   Expect **only** `m4d/ClientApp/package.json` and
   `m4d/ClientApp/yarn.lock`. If `eslint --fix` also rewrote source files,
   review that diff on its own merits and mention it in the PR body rather
   than letting it ride along unexplained. Never hand-edit `yarn.lock`.

9. **Create a branch** — never commit dependency bumps straight to `main`:

   ```sh
   git checkout -b yarn-updates-<YYYY-MM-DD>
   ```

   Use the `major`/`minor` scope in the name when the run was scoped (e.g.
   `yarn-majors-2026-09-21`), so concurrent passes don't collide.

10. **Commit.** Every commit in this repo needs **both** the DCO sign-off and
    the Claude co-author trailer (see the repo `CLAUDE.md` — a missing
    sign-off fails the DCO check):

    Use repeated `-m` flags rather than a heredoc — the closing delimiter of
    an indented heredoc silently breaks:

    ```sh
    git commit \
      -m "Update client dependencies" \
      -m "<one line per notable bump, plus anything deferred>" \
      -m "Signed-off-by: David W. Gray <dwgray67@hotmail.com>
Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
    ```

    Both trailers must share the final `-m` so git parses them as trailers;
    the `Co-Authored-By` line sits at column 0 on purpose, since it's inside
    a quoted string and any leading whitespace would land in the message and
    break trailer parsing. Verify with `git log -1 --format='%(trailers)'`.

    `git commit -s` will generate the sign-off from your git identity if
    you'd rather not type it, but the co-author line still has to be added
    by hand.

11. **Push and open the PR:**

    ```sh
    git push -u origin <branch>
    gh pr create --base main --title "<title>" --body "<body>"
    ```

    PRs here are squash-merged, so **the PR title becomes the commit
    subject** — write it as a sentence-case description of the change
    (`Update client dependencies`, `Bump Vite and Vitest to current`), not
    as a bare branch name. The body should cover:

    - what was bumped, grouped patch/minor vs. major;
    - majors **deferred** and the concrete blocker (a conflicting peer
      range, a migration that needs new deps) — this is the most useful
      part of the PR for the reviewer;
    - any quarantined version you stepped back from;
    - build / lint / test status, stating explicitly that the lint error
      count matched baseline.

    End the body with:

    ```txt
    🤖 Generated with [Claude Code](https://claude.com/claude-code)
    ```

12. **Report**: the PR URL, what was updated, what was flagged/skipped
    (majors, pinned resolutions, quarantined versions), and build/lint/test
    status.

## Notes

- Don't touch `m4d/ClientApp/yarn.lock` by hand — only via `yarn up` /
  `yarn install`.
- LF line endings are enforced on `m4d/ClientApp/**` via `.gitattributes`;
  `yarn up` won't touch source files, so this only matters if a dependency
  bump requires you to hand-edit a config file.
- A major bump on a package that `bootstrap-vue-next` or `reka-ui` also
  depends on (notably `@vueuse/core`) resolves **two copies** into the
  bundle, since those pin older ranges. It builds and tests fine, but call
  out the duplication in the PR body so the reviewer can weigh the added
  weight against the bump.
- `yarn why <package>` is the quickest way to see who else pins a package
  and whether a bump will duplicate it.
