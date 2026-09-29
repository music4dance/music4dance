---
name: "music4dance-wp-handoff"
description: Draft a music4dance.blog blog post or help page from a short description, and package it for handoff to Claude Cowork, which pushes it to WordPress.com as a draft. Use when asked to write, draft, prep, package or hand off a post or help page for music4dance.blog.
argument-hint: <post | help page> <what it's about, angle, anything to include>
---

# music4dance.blog post / help page: draft and handoff

Claude Code writes the content and makes the screenshots. Claude Cowork (which has the WordPress.com
connector and Chrome) creates the **draft** on music4dance.blog. David edits and publishes by hand.
Your job is to turn David's description into a package Cowork can push mechanically, without asking
any questions.

## The request

$ARGUMENTS

(If this is empty, use David's message instead. If there's still no description, ask what the
piece is about and whether it's a blog post or a help page.)

## Phase 1: understand the request before writing

1. **Pin down the brief.** From the description, work out: blog post or help page; the subject (usually
   a feature, a change, or a question to readers); the angle or hook; anything David said to
   include or avoid; and any target length. Defaults: a feature post is about 800–1200 words; a help
   page is as long as the feature needs.
2. **Gather the facts from the source of truth, not from memory:**
   - the code and recent commits for the feature (`git log`, the relevant controllers/components),
   - design notes under `architecture/` and any existing notes under `local/`,
   - the live site (https://www.music4dance.net) for current numbers, labels, menu paths and URLs,
   - existing help pages on https://music4dance.blog/music4dance-help/ that should be linked or that
     the new piece shouldn't duplicate,
   - the most recent 2–3 blog posts (https://music4dance.blog/), for voice, for callbacks ("in my last
     post…"), and for the current Step of the Month.
3. **Check with David before drafting only if something is truly ambiguous.** Examples: which of two
   angles to take, or whether a half-finished feature should be mentioned. Ask everything in one short
   batch. Otherwise make a sensible choice and record it under "Open questions" in notes.md.
4. **Plan the images.** Decide what screenshots would help (usually 1–3 for a post, one per major UI
   area for a help page; annotated for help pages, clean for posts). Then capture them (see Screenshots).

## Phase 2: write, then package

Write post.md in David's voice (see House conventions), then produce the package described below.

## Output location

One folder per piece: `local/<slug>/` (e.g. `local/artist-index-post/`, `local/artist-help/`).

```
local/<slug>/
  post.md          # the content (help pages too; same name keeps things simple)
  notes.md         # metadata + image table + link table + numbers
  <image>.png      # each image, named exactly as referenced in post.md
  capture.mjs      # (if screenshots are captured) script to regenerate them
```

Image filenames matter: they become the WordPress media filenames and URLs. Use short kebab-case
names that describe the image (`artist-index.png`, not `screenshot-1.png`). Never reuse a filename
already used by an earlier post or help page (for example, append `-post` or the month).

At the end, tell David which folder to hand to Cowork: he connects `local/<slug>/` in a new Cowork task
in the "music4dance blog posts & documentation" project. (Connecting the folder keeps the image filenames;
attaching the files loses them.)

In notes.md, mark any tag that doesn't exist on the blog yet as "(new)", if you can tell from the tag
archive. Cowork will create it.

## post.md: allowed Markdown

Cowork converts this with a small script, so stick to this subset:

- `# Title` on the first line (used as the WordPress title, not repeated in the body)
- `## Section` and `### Subsection`
- paragraphs (a blank line between each; soft-wrapped lines are joined)
- `- ` bullet lists and `1. ` numbered lists (single level)
- `[text](https://absolute-url)`, `**bold**`, `_italic_`
- image placeholders, exactly this form, on their own lines:

  ```
  **[IMAGE: artist-index.png]**
  _Caption: The artist index, opened to the artists with the most songs in the catalog._
  ```

- simple pipe tables: a header row, a `| --- |` separator, then body rows. Links and bold text are fine
  inside cells, but no cells that span rows or columns, and no line breaks inside a cell. Use a table when
  the content is a reference (parameters, values, defaults); use lists for everything else.

Do not use nested lists, blockquotes, raw HTML, code blocks, footnotes, or relative links.
If a piece needs one of these, say so in notes.md under "Needs manual formatting".

## House conventions

- Blog posts usually end with a `## Step of the Month` section. The phrase "Step of the Month" in its
  first sentence links to
  `https://music4dance.blog/2026/03/27/updated-feature-search-history/#step-of-the-month`.
- Link music4dance features to the live site (`https://www.music4dance.net/...`) and related help
  pages to `https://music4dance.blog/music4dance-help/<page>/`.
- Feedback link: `https://music4dance.blog/feedback/`.
- Write in David's voice: first person, conversational, candid about limitations, and incremental
  in framing ("this is the incremental version").

## notes.md: required sections

```markdown
# Handoff notes: <title>

## Metadata
- **Type:** post | page
- **Title:** ...
- **Slug:** ... (optional for posts)
- **Category:** About music4dance   (posts only; name it exactly as on the site)
- **Tags:** Tag One, Tag Two, ...   (posts only; reuse existing tag names where possible)
- **Parent page:** music4dance-help (pages only; give the parent's slug)
- **Menu order:** ... (pages only, optional)
- **Excerpt:** ... (optional)
- **Featured image:** <file>.png or none
- **Related posts block:** none | list of post URLs to feature at the end

## Images
| File | Placement | Caption | Alt text |
| ---- | --------- | ------- | -------- |
| artist-index.png | Under "Browsing" | ... | ... |

## Links
| Anchor text | URL |
| ----------- | --- |
(All checked; state the date and that each returned 200.)

## Numbers to re-check before publishing
| Claim | Value | Source |
| ----- | ----- | ------ |

## Needs manual formatting
(anything Cowork can't do with the Markdown subset, or "none")

## Open questions for David
(or "none")
```

Captions and alt text in the image table must match post.md exactly. Alt text describes what is
visible, for someone who can't see it. The caption says why the image is there.

## Screenshots

- PNG, captured at 2x device scale, 1280 CSS px wide (2560 px actual).
- Hide the cookie banner and site nav; trim long lists or tables to a readable figure height.
- Keep **clean** screenshots (blog posts) separate from **annotated** ones (help pages). Never mix them.
- Save the capture script next to the images, and add the command to regenerate them to notes.md.
- Keep each file under about 500 KB.

## Before you finish

1. Every `[IMAGE: x]` in post.md has a matching file in the folder and a row in the image table.
2. Every link in post.md is in the link table and was checked live (HTTP 200).
3. Numbers pulled from production have a source and a timestamp.
4. Nothing in post.md is outside the allowed Markdown subset.
5. Report: the folder path, word count, a two-sentence summary of the angle you took, and any open
   questions.

## Example invocations

```
/music4dance-wp-handoff post: announce the new artist index; tie it back to the Dolly Parton post and the Prince/Prince Royce problem
/music4dance-wp-handoff help page for the artist index and artist pages, including how to fix an artist list on a song
/music4dance-wp-handoff post asking readers for their favorite Viennese Waltz songs; make it the October Step of the Month
```
