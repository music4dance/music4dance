#!/usr/bin/env node
// Looks at the most recent posts on music4dance.blog, skips any that already have a row in
// blogmap.txt, and inserts a new row (in the matching category block, in date order) for any
// that are missing. The Description is a best-effort ~100-word teaser pulled from the post's
// own content (links preserved) - review/tweak it before it goes live, same as before.
//
// A post is matched to a blogmap category by comparing its WordPress category slugs against
// blogmap.txt's `blog/category/<slug>` rows. A post whose category doesn't match any of those is
// reported and skipped rather than guessed at.
//
// See architecture/blog-help-sitemap.md for the full picture of how blogmap.txt is used.
//
// Usage:
//   node scripts/add-new-blog-posts.mjs                  # check the 3 newest posts, write blogmap.txt
//   node scripts/add-new-blog-posts.mjs --count 10        # check more posts
//   node scripts/add-new-blog-posts.mjs --dry-run         # report what would be added, don't write
//   node scripts/add-new-blog-posts.mjs --blogmap <path>  # override the blogmap.txt path (testing)

import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";

const POSTS_URL = "https://public-api.wordpress.com/rest/v1.1/sites/music4dance.blog/posts/";
const DESCRIPTION_WORD_LIMIT = 100;

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

function parseArgs(argv) {
  const args = { count: 3, dryRun: false, blogmap: null };
  for (let i = 0; i < argv.length; i++) {
    switch (argv[i]) {
      case "--count":
        args.count = Number(argv[++i]);
        break;
      case "--dry-run":
        args.dryRun = true;
        break;
      case "--blogmap":
        args.blogmap = argv[++i];
        break;
      default:
        throw new Error(`Unrecognized argument: ${argv[i]}`);
    }
  }
  return args;
}

function decodeEntities(str) {
  return str
    .replace(/&#(\d+);/g, (_, n) => String.fromCodePoint(Number(n)))
    .replace(/&amp;/g, "&")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&nbsp;/g, " ");
}

// Strip everything but <a href="...">...</a>, decode entities, collapse whitespace, then
// truncate to ~DESCRIPTION_WORD_LIMIT words (closing a truncated link) and append an ellipsis.
function makeTeaser(html) {
  const normalized = html
    .replace(/<a\s+[^>]*?href=["']([^"']*)["'][^>]*>/gi, "@@OPEN_A@@$1@@END_OPEN_A@@")
    .replace(/<\/a>/gi, "@@CLOSE_A@@")
    .replace(/<[^>]+>/g, " ")
    .replace(/@@OPEN_A@@([^@]*)@@END_OPEN_A@@/g, "<a href=\"$1\">")
    .replace(/@@CLOSE_A@@/g, "</a>");

  const tokens = normalized.split(/(<a href="[^"]*">|<\/a>)/g);

  let output = "";
  let wordCount = 0;
  let openAnchor = false;
  let truncated = false;
  let justOpenedAnchor = false; // no space between "<a href=...>" and the word right after it

  outer: for (const token of tokens) {
    if (/^<a href="[^"]*">$/.test(token)) {
      if (output.length > 0) output += " ";
      output += token;
      openAnchor = true;
      justOpenedAnchor = true;
      continue;
    }
    if (token === "</a>") {
      output += token;
      openAnchor = false;
      continue;
    }

    const words = decodeEntities(token).split(/\s+/).filter(Boolean);
    for (const word of words) {
      if (wordCount >= DESCRIPTION_WORD_LIMIT) {
        truncated = true;
        break outer;
      }
      if (output.length > 0 && !justOpenedAnchor && !/^[.,;:!?)]/.test(word)) output += " ";
      output += word;
      justOpenedAnchor = false;
      wordCount++;
    }
  }

  if (openAnchor) {
    output += "</a>";
  }
  return truncated ? `${output} ...` : output;
}

function slugFromReference(reference) {
  let slug = reference;
  if (slug.startsWith("blog/")) slug = slug.slice("blog/".length);
  if (slug.endsWith("/")) slug = slug.slice(0, -1);
  return slug;
}

function parseBlogmap(text) {
  const lines = text.split(/\r?\n/);
  const existingSlugs = new Set();
  const categories = []; // { name, slug, insertAfterLine }

  lines.forEach((line, idx) => {
    if (line === "") return;
    const parts = line.split("\t");
    let depth = 0;
    while (depth < parts.length && parts[depth] === "") depth++;
    const fields = parts.slice(depth);

    if (depth === 0) {
      const [name, reference] = fields;
      if (reference && reference.startsWith("blog/category/")) {
        categories.push({ name, slug: slugFromReference(reference).replace("category/", ""), insertAfterLine: idx });
      }
      return;
    }

    if (fields.length === 5) {
      existingSlugs.add(slugFromReference(fields[1]));
      // A post row belongs to whichever category block we most recently entered.
      if (categories.length > 0) {
        categories[categories.length - 1].insertAfterLine = idx;
      }
    }
  });

  return { lines, existingSlugs, categories };
}

async function fetchRecentPosts(count) {
  const url = `${POSTS_URL}?number=${count}&fields=slug,date,title,categories,content`;
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(`${url} -> HTTP ${response.status}`);
  }
  const data = await response.json();
  return data.posts;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const blogmapPath = args.blogmap ?? path.join(repoRoot, "m4d/ClientApp/src/assets/content/blogmap.txt");

  const posts = await fetchRecentPosts(args.count);

  const blogmapText = readFileSync(blogmapPath, "utf8");
  const { lines, existingSlugs, categories } = parseBlogmap(blogmapText);

  const added = [];
  const skippedExisting = [];
  const skippedNoCategory = [];

  // Oldest-first so repeated inserts into the same category block land in ascending date order.
  const oldestFirst = [...posts].sort((a, b) => (a.date < b.date ? -1 : 1));

  for (const post of oldestFirst) {
    if (existingSlugs.has(post.slug)) {
      skippedExisting.push(post.slug);
      continue;
    }

    const postCategorySlugs = Object.values(post.categories ?? {}).map((c) => c.slug);
    const category = categories.find((c) => postCategorySlugs.includes(c.slug));
    if (!category) {
      skippedNoCategory.push({ slug: post.slug, categories: postCategorySlugs });
      continue;
    }

    const title = decodeEntities(post.title);
    const reference = `blog/${post.slug}/`;
    const description = makeTeaser(post.content);
    const date = post.date.slice(0, 10);
    const row = ["", title, reference, description, "", date].join("\t");
    const insertIndex = category.insertAfterLine + 1;

    lines.splice(insertIndex, 0, row);
    for (const c of categories) {
      if (c === category) {
        c.insertAfterLine = insertIndex; // the row we just added is now this block's last line
      } else if (c.insertAfterLine >= insertIndex) {
        c.insertAfterLine += 1; // shifted down by the insertion
      }
    }

    added.push({ title, reference, date, category: category.name });
    existingSlugs.add(post.slug);
  }

  console.log(`Checked ${posts.length} most recent post(s) from music4dance.blog.\n`);

  if (added.length > 0) {
    if (args.dryRun) {
      console.log(`Dry run: would add ${added.length} new row(s) to ${blogmapPath}:`);
    } else {
      writeFileSync(blogmapPath, lines.join("\n"));
      console.log(`Added ${added.length} new row(s) to ${blogmapPath}:`);
    }
    for (const a of added) {
      console.log(`  [${a.category}] ${a.date}  "${a.title}"  (${a.reference})`);
    }
    if (!args.dryRun) {
      console.log("\nReview the descriptions (they're an unedited ~100-word excerpt) before publishing.");
      console.log(`\`git diff\` to review, \`git checkout -- ${blogmapPath}\` to revert.`);
    }
  } else {
    console.log("No new rows to add.");
  }

  if (skippedExisting.length > 0) {
    console.log(`\n${skippedExisting.length} already had a row: ${skippedExisting.join(", ")}`);
  }

  if (skippedNoCategory.length > 0) {
    console.log(`\n${skippedNoCategory.length} post(s) skipped - no blogmap category matched their WordPress category:`);
    for (const s of skippedNoCategory) {
      console.log(`  ${s.slug} (WP categories: ${s.categories.join(", ") || "none"})`);
    }
  }
}

main().catch((err) => {
  console.error(err);
  process.exitCode = 1;
});
