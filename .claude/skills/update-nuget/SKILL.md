---
name: update-nuget
description: >-
    Updates NuGet package references across the music4dance.net .NET solution
    (music4dance.sln) and the dotnet-ef local tool. Use when the user asks to
    update, bump, or upgrade NuGet packages or .NET dependencies.
---

# update-nuget

Updates NuGet packages across all projects in `music4dance.sln`, then verifies
the build and test suite still pass.

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

There is also a local tool manifest at `m4d/.config/dotnet-tools.json`
(`dotnet-ef`) — check it for updates separately, it isn't covered by
`dotnet list package`.

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

8. **Report**: list what was updated, what was flagged/skipped (framework-tied
   or major-version packages), and confirm build/test status.

## Notes

- Never run `dotnet list package --outdated` with `--include-prerelease`
  unless the user asks for prerelease packages.
- EF Core package bumps that touch `m4dModels` warrant a quick check that no
  new migration is required as a side effect of the version bump itself.
