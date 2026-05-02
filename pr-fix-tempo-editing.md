# PR: Fix Tempo Editing Permissions

## Summary

Any authenticated user can now edit the tempo when it has only ever been set algorithmically (e.g., by EchoNest `batch-e` or Tempo Bot). Previously, `canTag` role was required. Adds tests for both this change and the existing system-tag removal permission, and updates the architecture permission table.

## Changes

### Permission fixes

- **`SongStats.vue`** — `canOverrideTempo` no longer requires `context.canTag`; any logged-in user may edit an algo-set tempo. Removed the now-unused `getMenuContext` import.
- **`FieldEditor.vue`** — `overridePermission=true` unconditionally grants edit access (previously still checked role, defeating the override).

### New tests

- **`SongStats.test.ts`** (11 tests) — covers `isSystemTempo` detection (algo-only vs human-set vs human-override), pencil button visibility for all four cases, the `edit` emit, and `overridePermission` prop forwarding to `FieldEditor`.
- **`TagListEditor.test.ts`** (8 tests) — verifies that authenticated users (without `canEdit`) see "Remove System Tags" for tags last set by batch/algorithmic users, that `canEdit` users see the full "Remove Tags" section instead, and that anonymous users see neither.

### Documentation

- **`architecture/song-details-viewing-editing.md`** — added two new rows to the permission table:
  - "Edit algo-set Tempo ¹" (any authenticated user); footnote documents `isSystemTempo`/`canOverrideTempo` computed props and `FieldEditor`'s `overridePermission` prop.
  - "Remove system-only tags ²" (any authenticated user); footnote documents `SongHistory.systemTagKeys` and `TagListEditor.vue` implementation.

### Dev tooling

- **`.github/copilot-instructions.md`** — added explicit warning that calling `runTests` without `files` discovers all test projects including SelfCrawler; documents the three correct alternatives (`run_task "Server: Test"`, `run_task "Test All"`, or explicit file paths).
