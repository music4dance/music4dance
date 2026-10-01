# Link New Blog Posts and Help Articles

**Type:** Runbook
**Status:** Current
**Last verified:** 2026-10-01

## When to use

After publishing a post on `music4dance.blog`, or a help article under
`music4dance.blog/music4dance-help/`, so it shows up on the site: the home page rotation, the
site map, and the help links. The file formats and how the data is consumed are in
[blog-help-sitemap](../pages/blog-help-sitemap.md). Drafting and pushing the WordPress post itself
is handled by the `music4dance-wp-handoff` skill.

## Prerequisites / access

- A repo checkout with Node 22 (for the script).
- An admin account, to reload the site map.
- For the fast path only: the App Service's FTPS deployment credentials, or Kudu.

## Steps

1. **Blog post: add the row with the script** (from the repo root):
   ```sh
   node scripts/add-new-blog-posts.mjs --dry-run   # see what it would add
   node scripts/add-new-blog-posts.mjs             # add rows for new posts (default: 3 newest)
   ```
   It skips posts already in `blogmap.txt`, places each new post in the category matching its
   WordPress category, and drafts a description from the post body. A post whose category doesn't
   match exactly one existing category is reported and skipped; add that one by hand.
2. **Edit the row.** Review with `git diff` and rewrite the drafted `Description`, which is only a
   rough teaser. Put any text in `OneTime` to keep a post out of the home-page rotation.
3. **Help article: edit `helpmap.txt` by hand.** Add a tab-indented row under the right section
   (title and `Reference`; there's no description or date). `blog/...` references are rewritten to
   `https://music4dance.blog/...` at render time.
4. **Ship it.** Either:
   - **Normal path:** commit and deploy (see the deploy runbook, `runbooks/deploy.md`). The build copies
     `ClientApp/src/assets/content/*` into `wwwroot/content/`.
   - **Fast path (no deploy):** upload the edited `blogmap.txt` / `helpmap.txt` over the live
     copies in the deployed site's `wwwroot/content/` folder. Confirm the exact path in Kudu before
     overwriting, because it differs between deployment modes. **Also commit the same change**,
     or the next deploy silently reverts it.
5. **Reload the site map:** **Admin → Initialization Tasks → Update Sitemap**
   (`AdminController.UpdateSitemap`). The parsed tree is cached in memory, so neither path takes
   effect until this runs, or until the app restarts.

## Verification

The new post appears in its category on `/home/sitemap` and is eligible for the home-page
rotation. Help links resolve to `music4dance.blog`.

## Rollback

Revert the row, ship it the same way, and run **Update Sitemap** again.
