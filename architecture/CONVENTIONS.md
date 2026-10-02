# Architecture Doc Conventions

How documents in `architecture/` are organized, so anything can be found from
[README.md](README.md) and nothing goes stale silently.

## Doc types

| Type          | Purpose                                                                       | Lives in                                                    |
| ------------- | ----------------------------------------------------------------------------- | ----------------------------------------------------------- |
| **Reference** | How the system works **today**. The default type.                             | An area folder (`songs/`, `search/`, `infrastructure/`, …); the system map is `overview.md` at the top level |
| **Runbook**   | Step-by-step operational procedure: something a person runs, in order.       | `runbooks/`                                                 |
| **Plan**      | A proposal or design for work that is **not yet implemented** (or only partly). | `plans/`                                                    |

Area folders: `dances/`, `songs/`, `search/`, `music-services/`, `users-admin/`, `pages/`, `security/`,
`infrastructure/`, `observability/`, `dev-testing/`. Add a new folder only when a new area
doesn't fit any of these, and add it to the README.

## Header block

Every doc starts with its title and then a short header block:

```markdown
# Title

**Type:** Reference | Runbook | Plan
**Status:** Current | Partially implemented | Proposed | On hold (reason)
**Last verified:** YYYY-MM-DD (the date someone last checked it against the code)
**Code:** `path/to/EntryPoint.cs`, `m4d/ClientApp/src/pages/foo/`
```

Use plain-text status. Don't use emoji, because they've been mangled into `?` before.

## Reference docs

- Describe the current state. Remove "we will…" language once the work ships.
- End with:
  - `## Future improvements`: short bullets. Anything with real design weight gets its own
    doc in `plans/` and is linked from here.
  - `## History`: one line per significant change, giving the PR or commit and what changed. This
    replaces keeping old plan documents and phase reports around.
  - `## Related`: links to neighboring docs.

## Runbooks

Sections, in order: **When to use** · **Prerequisites / access** · **Steps** · **Verification**
· **Rollback**. Link back to the reference doc that explains *why*. Runbooks are listed in the
README's Runbooks table.

## Plan lifecycle

1. A new proposal goes in `plans/` with `Type: Plan`, `Status: Proposed`, and a row in the
   README's Open Plans table.
2. When it ships, fold the as-built design into the relevant reference doc, add a History line,
   **delete the plan** (git history keeps it), and remove the README row, all in the same PR.
3. If only part of it ships, move the shipped part into the reference doc and leave the rest
   in `plans/` with `Status: Partially implemented`.

Don't keep phase completion reports. Fold their lasting content into the reference doc.

## Naming and links

- Filenames are lowercase kebab-case: `song-merge-algorithm.md`.
- Use relative Markdown links only (`[song-filter](../search/song-filter.md)`), so they resolve
  on GitHub and in editors. Don't use `[[wikilinks]]`.
- Code comments that cite a doc use the repo-relative path (`architecture/search/song-filter.md`).
  If you move or rename a doc, update those comments too: `git grep "architecture/<old-name>"`.
- Every doc is listed in [README.md](README.md). Adding, moving or deleting a doc updates the
  README in the same PR.
- Scratch notes, PR drafts and working files go in `local/`, not here.
