# Package Updates — NuGet & npm (April 2026)

## Summary

Upgrades all NuGet and npm packages to latest versions, including several major version bumps. Adds a reusable upgrade script for future maintenance. All server and client builds pass cleanly; all 448 server tests and 486 client tests pass.

## New: `scripts/upgrade-packages.ps1`

A parameterised PowerShell script for future upgrades:

```powershell
# Stay within current major versions (safe, no breaking changes)
.\scripts\upgrade-packages.ps1

# Upgrade everything to absolute latest including major version bumps
.\scripts\upgrade-packages.ps1 -Mode major

# Skip one side or the other
.\scripts\upgrade-packages.ps1 -Mode minor -SkipYarn
.\scripts\upgrade-packages.ps1 -Mode major -SkipNuGet
```

- **Minor mode** — NuGet: `dotnet outdated --upgrade --version-lock Major`; Yarn: `yarn up '*' '@*/*'` (respects `^` ranges)
- **Major mode** — NuGet: `dotnet outdated --upgrade`; Yarn: `yarn dlx npm-check-updates --upgrade` + `yarn install`

## Major Version Changes & Code Fixes

### TypeScript 5 → 6

- **`baseUrl` deprecated**: TS 6 deprecates `baseUrl` as a path-mapping anchor. Removed `baseUrl: "."` and the `ignoreDeprecations: "6.0"` workaround from `tsconfig.app.json`. The `@/*` alias continues to work via `paths` alone under `moduleResolution: "Bundler"`.
- **Stricter DOM types**: `HTMLButtonElement` no longer overlaps with `HTMLInputElement` for a direct cast. Fixed in `AdminFooter.vue`: `as HTMLButtonElement` → `as unknown as HTMLButtonElement`.

### bootstrap-vue-next 0.43 → 0.44

- `BFormSelect` now infers `modelValue` type from the options array. Fixed in `advanced-search/App.vue`: explicitly typed `sortId` as `ref<SortOrder | null>`.
- Router-link-backed anchors now render with `to=""` and `replace="false"` attributes. No behavioural change — 24 component snapshots updated.

### jQuery 3 → 4 (npm)

- `$.parseJSON` was removed in jQuery 4 (it was always just `JSON.parse`). The vendor scripts (`jquery-validation-unobtrusive`) still reference it. Added a one-line polyfill in `_ValidationScriptsPartial.cshtml` immediately after jQuery loads: `jQuery.parseJSON = JSON.parse`.

### Other major bumps (no code changes required)

| Package                                                     | Change                                           |
| ----------------------------------------------------------- | ------------------------------------------------ |
| `uuid` 13 → 14                                              | API unchanged (`v4` import still works)          |
| `@types/jsdom` 27 → 28 / `jsdom` 28 → 29                    | Dev/test only                                    |
| `unplugin-vue-components` 31 → 32                           | Build works cleanly                              |
| `vite-plugin-mkcert` 1 → 2                                  | Dev server only                                  |
| `AutoMapper` 15 → 16                                        | No API changes encountered                       |
| `coverlet.collector` 8 → 10                                 | Test infrastructure only                         |
| `Microsoft.NET.Test.Sdk` 17 → 18                            | Test runner only                                 |
| `Microsoft.Build` / `Tasks.Core` / `Utilities.Core` 17 → 18 | Tooling only                                     |
| `AspNet.Security.OAuth.Spotify` 9 → 10                      | Targets .NET 10; verify Spotify OAuth in staging |

## Housekeeping

- Removed redundant `VersionOverride="10.0.7"` on `Microsoft.Extensions.Configuration.EnvironmentVariables` in `m4d.Tests.csproj` — the central `Directory.Packages.props` entry already pins the same version.
