# Replace showdown as the Markdown Renderer

**Type:** Plan
**Status:** Proposed
**Last verified:** 2026-11-12
**Code:** `m4d/ClientApp/src/components/MarkDownEditor.vue`, `m4d/ClientApp/src/models/DanceText.ts`,
`m4d/ClientApp/vite.config.ts`, `m4d/ClientApp/package.json`

The client renders Markdown with [showdown](https://github.com/showdownjs/showdown) through the
`vue-showdown` wrapper. showdown has three open Dependabot alerts and no fixed version. Its last
release (2.1.0) shipped in 2022, and `vue-showdown` was last published in August 2023, so a fix is
unlikely to come. This plan replaces both with `marked` and `DOMPurify`.

## Current state

### Where Markdown is rendered

There is one call site. `MarkDownEditor.vue` renders `<VueShowdown :markdown="...">` for the dance
description on the dance-details page (`DanceDescription.vue`). An admin can switch it to a
textarea, and the preview below the textarea updates as they type.

The input is the dance's `Description` after `DanceText.expanded()` has rewritten bare
`[Dance Name]` references into `[Dance Name](/dances/seo-name)` links.

The plugin is registered per page through `AutoEndpoints` in `vite.config.ts`. Only
`dance-details` gets `VueShowdownPlugin` (flavor `vanilla`). `manualChunks` puts showdown in its own
`showdown` chunk.

### What the descriptions use

A survey of the 128 descriptions in `dance-environment-fallback.json` (2026-11-12) found:

| Feature | Descriptions using it |
| --- | --- |
| Paragraphs, inline `[text](url)` links | most |
| Emphasis (`*x*`, `**x**`) | 39 |
| Hard line break (two trailing spaces) | 3 |
| Raw HTML, entities, headings, lists, tables, code, images, reference links | 0 |

Any CommonMark renderer will produce the same output for this subset. The only differences to
expect are in the edge cases (see Verification).

### Risk today

All three advisories are in features we don't use or input we don't accept:

| Alert | Advisory | Why it doesn't reach us |
| --- | --- | --- |
| 126 | XSS through metadata title handling | The `metadata` option is off in the `vanilla` flavor |
| 125 | Stored XSS through table header ID injection | `tables` is off in `vanilla`, and no description has a table |
| 79 | ReDoS in link/anchor parsing | Input is admin-authored descriptions only. A slow regex would hang only that admin's tab |

Only admins (`menuContext.isAdmin`) can edit a description. Others only see what's already saved.
So the alerts aren't urgent, but they won't close until showdown is gone.

## Options considered

| Option | Maintained | Size (min) | Notes |
| --- | --- | --- | --- |
| **`marked` + `DOMPurify`** (recommended) | Yes, frequent releases | ~40 KB + ~22 KB | Synchronous `marked.parse()`, ships its own TypeScript types. Doesn't sanitize, so DOMPurify sanitizes the output |
| `markdown-it` | Yes | ~100 KB with linkify/entities | `html: false` escapes raw HTML. Big plugin ecosystem we don't need |
| `micromark` | Yes (unified) | small core | Strict CommonMark, safe by default (no raw HTML). Lower-level API. Good fallback if we want zero sanitizer |
| Render on the server (Markdig) | Yes | 0 client | Removes the client dependency, but the live admin preview still needs a client renderer. Two renderers would need to agree |

showdown 2.1.0 is about 75 KB minified, so `marked` + `DOMPurify` is no bigger than what we ship now.

DOMPurify is defense in depth. The content is trusted, but `v-html` would otherwise pass any
raw HTML or `javascript:` link straight through. It costs little and means the next advisory in
the Markdown library isn't an XSS.

## Plan

One PR, client only.

1. **Dependencies.** `yarn add marked dompurify`, then `yarn remove vue-showdown`. Both packages
   ship their own types. Check that `yarn why showdown` comes back empty.
2. **Component.** Add `src/components/MarkdownView.vue`:

   ```vue
   <script setup lang="ts">
   import DOMPurify from "dompurify";
   import { marked } from "marked";
   import { computed } from "vue";

   const props = defineProps<{ markdown: string }>();
   const html = computed(() =>
     DOMPurify.sanitize(marked.parse(props.markdown, { async: false, gfm: true })),
   );
   </script>

   <template>
     <!-- eslint-disable-next-line vue/no-v-html -- output is sanitized by DOMPurify -->
     <div v-html="html" />
   </template>
   ```

   `html` must be a `computed`, not a plain variable. The editor changes `markdown` as the admin
   types (see the MPA reactivity rule in `CLAUDE.md`).
3. **Call site.** In `MarkDownEditor.vue`, replace `<VueShowdown id="description" ...>` with
   `<MarkdownView id="description" :markdown="descriptionExpanded" />`. Keep the `id`, because
   styles or tests may target `#description`.
4. **Build config.** In `vite.config.ts`:
   - Remove the `dance-details` entry from `AutoEndpoints`. If the map is empty, check that the
     generated `main.ts` template handles a page with no plugins (it should, since 33 of 34 pages
     already have none).
   - Change the `showdown` `manualChunks` rule to match `marked` and `dompurify` (chunk
     `markdown`), or drop it. Only one page uses them, so a separate chunk doesn't buy much.
5. **Fix the empty-description text.** `DanceText.expanded()` returns a placeholder containing the
   literal Razor text `@Model.DanceName`, left over from the server-rendered page. Pass the dance
   name in, or reword the sentence so it doesn't need one.
6. **Tests.**
   - New `MarkdownView.test.ts`: emphasis, inline links, hard line breaks, a `<script>` tag
     stripped, and a `[x](javascript:alert(1))` link neutralized.
   - `dance-details.test.ts` currently snapshots an unresolved `<vueshowdown markdown="...">`
     stub, and the run logs `Failed to resolve component: VueShowdown`. After the swap the
     snapshot holds real HTML, which is better coverage. Review the diff and update it.
7. **Docs.** Update `architecture/pages/frontend-architecture.md` (the `AutoEndpoints` per-page
   plugin note and the `manualChunks` table row) and add a History line. Delete this plan and
   remove its README row, as `CONVENTIONS.md` asks.

### Verification

- Before switching, run a one-off script (in `local/`, not committed) that renders all 128
  fallback descriptions with both showdown and the new component, and diffs the normalized HTML.
  Expected differences:
  - showdown's `vanilla` flavor doesn't autolink bare URLs. GFM `marked` does. One description
    contains a URL with parentheses (`.../Bachata_(dance)`) that needs a look.
  - showdown adds `id` attributes to headings. No description has headings, so this shouldn't
    show up.
- `yarn build`, `yarn lint:ci`, and the full client test suite.
- Manually, as an admin on a dance-details page: edit a description, watch the preview update,
  save, and reload.
- Once merged, Dependabot alerts 79, 125 and 126 should close by themselves when showdown leaves
  `yarn.lock`.

### If this slips

If the swap won't happen soon, dismiss alerts 79, 125 and 126 in GitHub as "Vulnerable code is
not actually used" / "Risk is tolerable", with the reasons from the Risk table, so they stop
hiding new alerts. Reopen them if the replacement is abandoned.

## Related cleanup (separate PR)

The legacy MarkdownDeep pieces look unused. No `.cs`, `.cshtml`, `.ts`, `.js` or `.vue` file
references them as of 2026-11-12:

- the `markdowndeep` npm dependency in `m4d/ClientApp/package.json`
- the `MarkdownDeepVendor` copy to `wwwroot/vendor/markdowndeep` in `m4d/m4d.csproj`
- `MarkdownDeep.NET.Core` in `Directory.Packages.props` and `m4d/m4d.csproj`

Confirm with a production page check (no requests for `/vendor/markdowndeep/`) and remove all three.

## Related

- [frontend-architecture](../pages/frontend-architecture.md): page entries, `AutoEndpoints`, chunking
- [ci-cd-and-release](../infrastructure/ci-cd-and-release.md): how Dependabot is configured
