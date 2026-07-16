#!/usr/bin/env node
// Fills in missing DanceType.blogTag values in dances.json by matching each dance's slugified
// name against the current music4dance.blog tag cloud.
//
// A dance is only touched when it has NO blogTag today - existing values are never overwritten,
// since several of them are deliberately curated to point at a broader family tag rather than the
// dance's own literal name (e.g. "Slow Waltz"/"Cross-step Waltz"/"Viennese Waltz" all share the
// "waltz" tag). Existing blogTag values that no longer appear in the current tag cloud are only
// reported (possible drift - renamed/deleted blog tag), never auto-changed, since fixing those
// needs a human decision about which tag they should point at now.
//
// The file is edited with a scoped, per-dance regex insertion (not a full JSON.parse/stringify
// round-trip) specifically to avoid two things a round-trip would silently do: reformat every
// unrelated line to Prettier's/JSON.stringify's spacing, and truncate trailing-zero decimals like
// "120.0" to "120" (JS numbers don't preserve that).
//
// Usage:
//   node scripts/update-blog-tags.mjs                 # fetch the live tag cloud, write dances.json
//   node scripts/update-blog-tags.mjs --offline        # use local/tag-cloud.json instead of fetching
//   node scripts/update-blog-tags.mjs --dry-run         # report what would change, don't write
//   node scripts/update-blog-tags.mjs --tag-cloud <path>  # override the offline tag-cloud path
//   node scripts/update-blog-tags.mjs --dances <path>     # override the dances.json path (testing)

import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";

const TAG_CLOUD_URL = "https://public-api.wordpress.com/rest/v1.1/sites/music4dance.blog/tags?number=1000";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

function parseArgs(argv) {
  const args = { offline: false, dryRun: false, tagCloud: null, dances: null };
  for (let i = 0; i < argv.length; i++) {
    switch (argv[i]) {
      case "--offline":
        args.offline = true;
        break;
      case "--dry-run":
        args.dryRun = true;
        break;
      case "--tag-cloud":
        args.tagCloud = argv[++i];
        break;
      case "--dances":
        args.dances = argv[++i];
        break;
      default:
        throw new Error(`Unrecognized argument: ${argv[i]}`);
    }
  }
  return args;
}

// Mirrors m4d/ClientApp/src/helpers/StringHelpers.ts's wordsToKebab() - the same function
// NamedObject.seoName uses to build a dance's own URL slug.
function wordsToKebab(words) {
  return words.toLowerCase().replaceAll(" ", "-");
}

async function loadTagSlugs(args) {
  const tagCloudPath = args.tagCloud ?? path.join(repoRoot, "local/tag-cloud.json");

  if (!args.offline) {
    try {
      const response = await fetch(TAG_CLOUD_URL);
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }
      const data = await response.json();
      console.log(`Fetched ${data.tags.length} tags from the live tag cloud.`);
      return new Set(data.tags.map((t) => t.slug));
    } catch (err) {
      console.warn(`Could not fetch the live tag cloud (${err.message}); falling back to ${tagCloudPath}`);
    }
  }

  const data = JSON.parse(readFileSync(tagCloudPath, "utf8"));
  console.log(`Loaded ${data.tags.length} tags from ${tagCloudPath}.`);
  return new Set(data.tags.map((t) => t.slug));
}

function main() {
  const args = parseArgs(process.argv.slice(2));
  const dancesPath = args.dances ?? path.join(repoRoot, "m4d/ClientApp/src/assets/content/dances.json");

  return loadTagSlugs(args).then((tagSlugs) => {
    const originalText = readFileSync(dancesPath, "utf8");
    const dances = JSON.parse(originalText);

    let text = originalText;
    let addedCount = 0;
    const skipped = [];
    const drifted = [];

    for (const dance of dances) {
      if (dance.blogTag) {
        if (!tagSlugs.has(dance.blogTag)) {
          drifted.push(`${dance.id} (${dance.name}): blogTag "${dance.blogTag}" not found in current tag cloud`);
        }
        continue;
      }

      const slug = wordsToKebab(dance.name);
      if (!tagSlugs.has(slug)) {
        skipped.push(`${dance.id} (${dance.name}): no tag matching "${slug}"`);
        continue;
      }

      // Scoped to this dance's own id/name/meter block so the insertion can't drift into a
      // neighboring dance - every dance has "meter" immediately after "name", and whatever comes
      // next (blogTag, synonyms, searchonyms, or straight to instances) always starts on the next
      // line at the same 4-space indent. "meter" is sometimes multi-line and sometimes collapsed
      // to one line (e.g. Pattern's `{ "numerator": 1, "denominator": 1 }`), and never contains a
      // nested `{`, so matching up to its first `}` covers both.
      const blockPattern = new RegExp(
        `("id": "${dance.id}",\\n    "name": "[^"]*",\\n    "meter": \\{[\\s\\S]*?\\},\\n)`,
      );
      const match = blockPattern.exec(text);
      if (!match) {
        throw new Error(`Could not locate the id/name/meter block for ${dance.id} in ${dancesPath}`);
      }
      text = text.slice(0, match.index) + match[1] + `    "blogTag": "${slug}",\n` + text.slice(match.index + match[0].length);
      addedCount++;
      console.log(`+ ${dance.id} (${dance.name}): blogTag = "${slug}"`);
    }

    if (skipped.length > 0) {
      console.log(`\nNo matching tag found for ${skipped.length} dance(s):`);
      skipped.forEach((line) => console.log(`  ${line}`));
    }

    if (drifted.length > 0) {
      console.log(`\nPossible drift - existing blogTag not in the current tag cloud (${drifted.length}):`);
      drifted.forEach((line) => console.log(`  ${line}`));
    }

    if (addedCount === 0) {
      console.log("\nNo changes needed.");
      return;
    }

    if (args.dryRun) {
      console.log(`\nDry run: would add ${addedCount} blogTag value(s). No file written.`);
      return;
    }

    writeFileSync(dancesPath, text);
    console.log(`\nAdded ${addedCount} blogTag value(s) to ${dancesPath}.`);
  });
}

main().catch((err) => {
  console.error(err);
  process.exitCode = 1;
});
