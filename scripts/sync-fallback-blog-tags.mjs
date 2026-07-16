#!/usr/bin/env node
// Syncs the `blogTag` field inside dance-environment-fallback.json's `dances[]`/`groups[]` entries
// to match the current dances.json/dancegroups.json - the two are otherwise unrelated files (the
// fallback is a snapshot of computed song stats: songIds, songCount, tag aggregates, descriptions,
// links; see m4dModels/DanceStatsManager.cs/DanceBuilder.cs for how it's really generated, which
// needs a live Azure Search index + SQL DB and can only be triggered via an authenticated admin
// action - not something this script, or a build step, can regenerate). `blogTag` is the one field
// on that snapshot that's purely a mirror of dances.json/dancegroups.json: m4dModels/DanceStats.cs's
// `BlogTag` getter reads `DanceObject?.BlogTag` live off the static content, and its
// [JsonConstructor] doesn't even accept `blogTag` as a parameter - so the value sitting in the
// fallback JSON file is never actually read back in at runtime. Keeping it in sync is purely for
// the accuracy of the checked-in snapshot, not a functional fix.
//
// Like update-blog-tags.mjs, this edits the file with scoped per-entry regex insertion/replacement
// rather than a full JSON.parse/stringify round-trip, to avoid reformatting the ~17,000 unrelated
// lines in this file.
//
// Usage:
//   node scripts/sync-fallback-blog-tags.mjs             # sync and write
//   node scripts/sync-fallback-blog-tags.mjs --dry-run    # report what would change, don't write

import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const contentDir = path.join(repoRoot, "m4d/ClientApp/src/assets/content");

function parseArgs(argv) {
  const args = { dryRun: false };
  for (const arg of argv) {
    if (arg === "--dry-run") {
      args.dryRun = true;
    } else {
      throw new Error(`Unrecognized argument: ${arg}`);
    }
  }
  return args;
}

// Applies `entries` (id -> desired blogTag, merged from dances.json AND dancegroups.json - their
// id namespaces don't overlap) to every "danceId": "XXX" block in `text`. Both dances[] and
// groups[] entries share this exact shape:
//   "danceId": "XXX",
//   "danceName": "...",
//   "blogTag": "...",   <- present only if this id had one when the snapshot was generated
//   "songIds": [...
// blogTag, when present, is always the line immediately after danceName; insert/replace/report
// accordingly. A single combined map and one pass over the whole file (rather than one pass per
// source file) avoids each pass misreading the *other* array's entries as drift just because its
// own map doesn't have their ids.
function syncBlogTags(text, entries) {
  let added = 0;
  let replaced = 0;
  const driftWarnings = [];

  // Group 1: the "danceId"/"danceName" pair, always present, kept as-is.
  // Group 2: id, used to look up the desired value.
  // Group 3: the existing "blogTag" line, if this snapshot had one when generated.
  // Group 4: that line's current value.
  const idPattern =
    /("danceId": "([A-Z0-9]{3})",\n {6}"danceName": "[^"]*",\n)( {6}"blogTag": "([^"]*)",\n)?/g;
  let result = "";
  let cursor = 0;
  let match;

  while ((match = idPattern.exec(text))) {
    const [whole, idNameBlock, id, blogTagLine, currentValue] = match;
    const desired = entries.get(id);

    result += text.slice(cursor, match.index);

    if (desired && !blogTagLine) {
      result += idNameBlock + `      "blogTag": "${desired}",\n`;
      added++;
      console.log(`+ ${id}: blogTag = "${desired}"`);
    } else if (desired && currentValue !== desired) {
      result += idNameBlock + blogTagLine.replace(`"${currentValue}"`, `"${desired}"`);
      replaced++;
      console.log(`~ ${id}: blogTag "${currentValue}" -> "${desired}"`);
    } else {
      if (!desired && currentValue) {
        driftWarnings.push(
          `${id}: fallback has blogTag "${currentValue}" but the source file has none - left as-is`,
        );
      }
      result += whole;
    }

    cursor = match.index + whole.length;
  }

  result += text.slice(cursor);
  return { text: result, added, replaced, driftWarnings };
}

function loadIdToBlogTag(fileName, idKey) {
  const data = JSON.parse(readFileSync(path.join(contentDir, fileName), "utf8"));
  return data.filter((d) => d.blogTag).map((d) => [d[idKey], d.blogTag]);
}

function main() {
  const args = parseArgs(process.argv.slice(2));
  const fallbackPath = path.join(contentDir, "dance-environment-fallback.json");

  const entries = new Map([
    ...loadIdToBlogTag("dances.json", "id"),
    ...loadIdToBlogTag("dancegroups.json", "id"),
  ]);

  const text = readFileSync(fallbackPath, "utf8");
  const { text: newText, added, replaced, driftWarnings } = syncBlogTags(text, entries);

  if (driftWarnings.length > 0) {
    console.log(`\nPossible drift (${driftWarnings.length}) - review manually, not auto-changed:`);
    driftWarnings.forEach((line) => console.log(`  ${line}`));
  }

  if (added === 0 && replaced === 0) {
    console.log("\nNo changes needed.");
    return;
  }

  if (args.dryRun) {
    console.log(`\nDry run: would add ${added} and replace ${replaced} blogTag value(s). No file written.`);
    return;
  }

  writeFileSync(fallbackPath, newText);
  console.log(`\nAdded ${added} and replaced ${replaced} blogTag value(s) in ${fallbackPath}.`);
}

main();
