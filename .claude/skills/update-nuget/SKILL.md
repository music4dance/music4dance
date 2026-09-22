---
name: update-nuget
description: >-
    Updates NuGet package references across the music4dance.net .NET solution
    (music4dance.sln) and the dotnet-ef local tool, verifies the build and the
    full server test suite still pass, then branches, commits, and opens a PR.
    Use when the user asks to update, bump, or upgrade NuGet packages or .NET
    dependencies.
---

# update-nuget

Updates NuGet packages across all projects in `music4dance.sln`, verifies the
build and test suite still pass, then lands the change on a branch as a PR.

## Scope argument

This skill accepts an optional argument: `minor` or `major`.

- `/update-nuget` (no argument) — apply minor/patch bumps directly; list
  major bumps and ask before applying them (default behavior below).
- `/update-nuget minor` — apply **only** minor/patch bumps. Skip major bumps
  entirely (don't even ask) and mention them in the final report as
  available-but-not-applied.
- `/update-nuget major` — review **only** the packages with a major version
  available. Confirm with the user before applying each (or the batch), and
  leave minor/patch bumps untouched for a separate pass.

Framework-tied packages (see classification below) are never auto-applied
under either mode — they always require explicit confirmation regardless of
argument, since they represent a major framework-version upgrade (e.g.
`net10.0` → `net11.0`-class).

## Projects in scope

`DanceLib/DanceLibrary.csproj`, `m4dModels/m4dModels.csproj`, `m4d/m4d.csproj`,
`SelfCrawler/SelfCrawler.csproj`, and the `*.Tests.csproj` projects
(`DanceTests`, `m4dModels.Tests`, `m4d.Tests`). All target `net10.0`.

There are also `m4dModels.Sandbox` and `m4d.Sandbox` in the solution, which
carry a few package references of their own — `--outdated` lists them, so
don't skip them just because they're absent from the list above.

There is also a local tool manifest at `m4d/.config/dotnet-tools.json`
(`dotnet-ef`) — check it for updates separately, it isn't covered by
`dotnet list package`. Keep it on the same version as the EF Core packages.

## Procedure

1. **Restore first** so the outdated check has accurate data:

   ```sh
   dotnet restore music4dance.sln
   ```

2. **List outdated packages** solution-wide:

   ```sh
   dotnet list music4dance.sln package --outdated
   ```

   This prints per-project tables of `Requested`/`Resolved`/`Latest` versions.

3. **Classify each outdated package before touching anything:**

   - **Framework-tied packages** — anything starting `Microsoft.AspNetCore.*`,
     `Microsoft.EntityFrameworkCore.*`, `Microsoft.Extensions.*`,
     `Microsoft.NETCore.*` — must stay on the `10.x` line matching the
     project's `net10.0` target. If `Latest` shows an `11.x` version, that's a
     framework upgrade, not a routine bump — flag it to the user and skip it
     unless they explicitly ask for a framework upgrade.
   - **Major version bumps** on any other package (e.g. `5.x` → `6.x`) — list
     these separately and ask the user before applying, since they can carry
     breaking API changes.
   - **Minor/patch bumps** — safe to apply directly.

4. **Apply updates per the scope argument** (the `--outdated` output tells
   you which project(s) reference each package):

   ```sh
   dotnet add <Project.csproj> package <PackageId> --version <Version>
   ```

   - No argument or `minor`: apply minor/patch bumps directly.
   - No argument: also ask about major bumps and apply confirmed ones.
   - `minor`: skip major bumps entirely, no need to ask.
   - `major`: apply only the confirmed major bumps; leave minor/patch alone.
   - Framework-tied packages are always confirmed individually, never bundled
     into a blanket "apply all majors" approval.

5. **Update the local tool manifest** if `dotnet-ef` (or any other tool) is
   outdated:

   ```sh
   dotnet tool update dotnet-ef --version <Version>
   ```

   (Run from `m4d/`, where `.config/dotnet-tools.json` lives, or pass
   `--tool-manifest m4d/.config/dotnet-tools.json`.)

6. **Build the whole solution:**

   ```sh
   dotnet build music4dance.sln
   ```

   Note `dotnet build` can fail with file locks if a dev server (`dotnet
   watch` / IIS Express) is holding the output assemblies — check for that
   before assuming a real build break.

7. **Run the test suite, excluding SelfCrawler** (per project convention —
   SelfCrawler is Selenium-based and manual-only):

   ```sh
   dotnet test DanceTests/DanceLibrary.Tests.csproj
   dotnet test m4dModels.Tests/m4dModels.Tests.csproj
   dotnet test m4d.Tests/m4d.Tests.csproj
   ```

   Run all three projects, not a subset: a package bump can break code
   nowhere near the package you touched. All tests must pass before you
   commit. If any fail, bisect to the offending bump and either revert it or
   confirm the fix with the user — **do not open a PR on a red suite.**

   If a dev server (`dotnet watch` / IIS Express) holds `bin/*.dll`, use the
   VS Code tasks `Server: Build (Unlocked)` / `Server: Test (Unlocked)`
   instead, which redirect output to `local/build-out`.

8. **Check what actually changed** before committing:

   ```sh
   git status --short
   ```

   This solution uses **Central Package Management**, so versions live in
   `Directory.Packages.props` at the repo root, not in the individual
   `.csproj` files — `dotnet add package` edits that file. Expect **only**
   `Directory.Packages.props` and, if the tool was bumped,
   `m4d/.config/dotnet-tools.json`. Anything else (a `.csproj`, a
   `packages.lock.json`, a source file) should be reviewed on its own merits
   and mentioned in the PR body rather than riding along unexplained.

   `dotnet add package` **strips the trailing newline** from
   `Directory.Packages.props`, which shows up as `\ No newline at end of file`
   in the diff. Restore it before committing:

   ```sh
   printf '\n' >> Directory.Packages.props
   ```

9. **Create a branch** — never commit dependency bumps straight to `main`:

   ```sh
   git checkout -b nuget-updates-<YYYY-MM-DD>
   ```

   Use the `major`/`minor` scope in the name when the run was scoped (e.g.
   `nuget-majors-2026-09-21`), so concurrent passes don't collide.

10. **Commit.** Every commit in this repo needs **both** the DCO sign-off and
    the Claude co-author trailer (see the repo `CLAUDE.md` — a missing
    sign-off fails the DCO check):

    Use repeated `-m` flags rather than a heredoc — the closing delimiter of
    an indented heredoc silently breaks:

    ```sh
    git commit \
      -m "Update NuGet packages" \
      -m "<one line per notable bump, plus anything deferred>" \
      -m "Signed-off-by: David W. Gray <dwgray67@hotmail.com>
Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
    ```

    Both trailers must share the final `-m` so git parses them as trailers;
    the `Co-Authored-By` line sits at column 0 on purpose, since it's inside
    a quoted string and any leading whitespace would land in the message and
    break trailer parsing. Verify with `git log -1 --format='%(trailers)'`.

11. **Push and open the PR:**

    ```sh
    git push -u origin <branch>
    gh pr create --base main --title "<title>" --body "<body>"
    ```

    PRs here are squash-merged, so **the PR title becomes the commit
    subject** — write it as a sentence-case description of the change
    (`Update NuGet packages`, `Bump EF Core tooling to current`), not as a
    bare branch name. The body should cover:

    - what was bumped, grouped minor/patch vs. major, and which projects;
    - majors or framework-tied bumps **deferred** and the concrete reason
      (an `11.x` release that doesn't match `net10.0`, a breaking API
      change) — this is the most useful part of the PR for the reviewer;
    - the `dotnet-ef` tool version, if it moved;
    - build and test status, stating that all three test projects passed and
      that SelfCrawler was intentionally excluded.

    End the body with:

    ```txt
    🤖 Generated with [Claude Code](https://claude.com/claude-code)
    ```

12. **Report**: the PR URL, what was updated, what was flagged/skipped
    (framework-tied or major-version packages), and build/test status.

## Notes

- Never run `dotnet list package --outdated` with `--include-prerelease`
  unless the user asks for prerelease packages.
- EF Core package bumps that touch `m4dModels` warrant a quick check that no
  new migration is required as a side effect of the version bump itself.
- **`Microsoft.Build.*` and `Microsoft.NET.Test.Sdk` (the `18.x` line) are
  tied to the SDK, not to the package's own semver.** A release can be built
  against `net11.0` and warn `doesn't support net10.0 ... consider upgrading
  your TargetFramework`, dragging `Microsoft.NET.StringTools` with it. Grep
  the build output for `doesn't support net10.0` after bumping these and
  step back to the last version that builds clean — as of 2026-09,
  `Microsoft.Build.Tasks.Core` / `Microsoft.Build.Utilities.Core` 18.10.1
  warn and 18.8.2 doesn't, while `Microsoft.NET.Test.Sdk` 18.10.1 is fine.
- `dotnet add package --no-restore` makes a large batch of bumps much faster;
  follow the batch with one `dotnet restore music4dance.sln`.
- The build emits three pre-existing `EF1001` internal-API warnings in
  `m4dModels/DanceMusicContext.cs`. They are baseline noise, not something a
  bump introduced.
