# music4dance.net — Claude Code Guide

## Project Overview

Web app matching music to competitive/social ballroom dance styles based on meter, tempo, and WDC/NDCA requirements.

## Stack

- **Backend**: ASP.NET Core (net10.0), C#
  - `DanceLib/` — domain models and business logic
  - `m4d/` — web app (controllers, views, config)
  - `m4dModels/` — data models and EF context
  - `SelfCrawler/` — manual-only web crawling tests (never run in CI)
- **Frontend**: Vue 3 + TypeScript, Vite, Vitest, Bootstrap 5 + SCSS
- **Package manager**: Yarn (not npm)

## Working Directory Conventions

`local/` at the project root is gitignored (except `local/.gitkeep`). Place all temporary files, scratch notes, PR drafts, and customer-supplied imports here — not in the project root or `architecture/`.

Architecture documents go in `architecture/`. Prefer updating existing docs over creating new ones.

## C# Standards

**Do NOT use nullable reference types** (`string?`, `object?`). The project has nullable reference types enabled at the compiler level but the convention is to avoid `?` annotations on reference types to prevent CS8632 warnings. Use regular types and check null explicitly.

- Prefer records for immutable data; use `readonly` fields
- Entity Framework code-first; use `JsonConstructor` for serialization
- Core domain objects (`Tempo`, `TempoRange`, etc.) are immutable classes

## TypeScript / Vue Standards

- Use `<script setup lang="ts">` Composition API throughout
- Avoid `any`; use `defineProps<Interface>()`
- **LF line endings required** in all `m4d/ClientApp/**` files (enforced by `.gitattributes`)
- **Icons**: Use `unplugin-icons` auto-imported components (`<IBiCheckCircleFill />`, etc.). **Never** use `<i class="bi bi-icon-name">`.
- **Bootstrap**: Always use `bootstrap-vue-next` components (`<BButton>`, `<BCard>`, etc.) over raw Bootstrap CSS/JS patterns.
- **PageFrame**: Every Vue page app (`src/pages/*/App.vue`) **must** wrap its template in `<PageFrame id="app" :title="...">`. Skipping it drops all site chrome (nav, footer, branding).
- **Functional style**: Prefer `map/filter/reduce/sort` over imperative loops.

### MPA Reactivity Pitfall

This is a Multi-Page Application. Many components load once and reuse instances via `:key="index"`. Values derived from props at setup time as plain variables become stale when the prop changes.

**Rule**: Any value derived from a prop must use `computed()`:

```ts
const foo = computed(() => props.bar.something); // correct
const foo = props.bar.something; // stale — wrong
```

When adding dynamic state to a page, audit all components in the render tree for this.

### Filter / Tag Construction

**Always** use the class library to build and parse filter strings. **Never** construct or parse them manually (string splits, template literals, etc.).

- Build dance filters: `new DanceQueryItem({ id, threshold, tags }).toString()`
- Build tags: `Tag.fromParts("International", "Style")`
- Parse dance queries: `DanceQueryItem.fromValue(str)`
- Parse tag strings: use `item.tagQuery.tagList.tags`

Format changes only need updating in one place; manual construction breaks silently.

## Build Commands

```txt
dotnet build                  # server (may fail if dev server holds file locks)
yarn install && yarn build    # client (includes type checking)
yarn lint                     # ESLint with auto-fix
yarn type-check               # Vue TSC
```

## Testing

### Run Targets

| Task                          | Command                                  |
| ----------------------------- | ---------------------------------------- |
| Server tests (no SelfCrawler) | VS Code task: `Server: Test`             |
| Full suite                    | VS Code task: `Test All`                 |
| SelfCrawler (manual only)     | VS Code task: `Server: Test SelfCrawler` |

**Warning**: Running `runTests` without specifying files uses VS Code test explorer and discovers SelfCrawler, causing spurious Selenium failures. Always use the tasks above, or specify explicit `.ts` file paths for client tests.

### Server Test Patterns

**Song creation** — use the serialized format, not direct property assignment:

```csharp
// Correct
var song = await Song.Create(".Create=\tUser=dwgray\tTitle=My Song\tTempo=180.0\tDanceRating=SLS+1", dms);
```

See `architecture/testing-patterns.md` for full format reference.

**Moq vs TestSongIndex**:

- **Moq** — unit tests needing full isolation; force return values/exceptions; default in `DanceMusicTester`
- **TestSongIndex** (spy) — integration tests where you need real behavior AND parameter verification (e.g., verifying `EditSong` was called with exact values)

### Client Test Patterns

- Run with `--run` flag (not watch mode)
- When testing components that use `bootstrap-vue-next`, register real components in `global.components` rather than stubbing them — stubs prevent slot content from rendering
- Icons render as inline SVGs; assert on `svg` presence or button attributes, not stub tag names
- Mock `getMenuContext()` via `vi.mock` (not `window.menuContext`) — it caches a module-level singleton

## Domain Notes

- Tempo is measured in **MPM** (Measures Per Minute) as the primary unit; BPM and BPS are secondary
- Each dance has a specific tempo range; same music can match multiple dances
- Organizations (NDCA, DanceSport) may define different tempo requirements for the same dance
- Dance IDs use lowercase with hyphens (e.g., `american-rhythm`)
