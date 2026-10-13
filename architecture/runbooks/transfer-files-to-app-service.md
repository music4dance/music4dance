# Transfer Files To and From the App Service

**Type:** Runbook
**Status:** Current
**Last verified:** 2026-10-13 (Option A confirmed against `msc4dnc`; Option B not yet exercised)

## When to use

You need to download a file from the live site, or upload one to it: a content file such as
`blogmap.txt`, a log, a dump, or anything under `/home`. FTP/FTPS (and the basic-auth publishing
credentials it used) is **turned off** on the App Services. Use Kudu (the SCM site) instead. It
signs you in with your Microsoft Entra account, so it doesn't need basic auth.

For logs specifically, [read-production-logs](read-production-logs.md) has shorter routes.

## Prerequisites / access

- An Azure account with Contributor (or Website Contributor) on the app, in resource group
  `m4d-Web`.
- For the scripted route: Azure CLI, signed in (`az login`).
- The apps and their Kudu hosts:

  | App | Kudu |
  | --- | --- |
  | `msc4dnc` (production) | `https://msc4dnc.scm.azurewebsites.net` |
  | `m4d-test` (test) | `https://m4d-test.scm.azurewebsites.net` |

- Where things live (Linux App Service): the site is in `/home/site/wwwroot`; logs are in
  `/home/LogFiles`. Only `/home` is persistent. **A deploy replaces `wwwroot`**, so anything you
  upload there is overwritten by the next deploy. Commit the same change to the repo too.

## Steps

### Option A: Browser (Kudu file manager) — the usual route

1. Portal → App Service `msc4dnc` → **Development Tools → Advanced Tools → Go**, or open
   `https://msc4dnc.scm.azurewebsites.net/newui` directly. Sign in with your Entra account.
2. Open **File Manager**.
3. Browse to the folder (e.g. `site/wwwroot/content/`).
   - **Download:** click the file's download action. A folder downloads as a zip.
   - **Upload:** drag the file onto the folder listing. A file with the same name is
     overwritten.

### Option B: Command line (Kudu VFS API with an Entra token)

Kudu accepts an Azure Resource Manager token as a bearer token. Paths after `/api/vfs/` are
relative to `/home`.

```bash
TOKEN=$(az account get-access-token --query accessToken -o tsv)
KUDU=https://msc4dnc.scm.azurewebsites.net        # or m4d-test

# List a folder (the trailing slash matters)
curl -s -H "Authorization: Bearer $TOKEN" "$KUDU/api/vfs/site/wwwroot/content/"

# Download a file
curl -s -H "Authorization: Bearer $TOKEN" -o local/blogmap.txt \
  "$KUDU/api/vfs/site/wwwroot/content/blogmap.txt"

# Upload (overwrite) a file; If-Match: * is required to replace an existing file
curl -s -X PUT -H "Authorization: Bearer $TOKEN" -H "If-Match: *" \
  --data-binary @m4d/ClientApp/src/assets/content/blogmap.txt \
  "$KUDU/api/vfs/site/wwwroot/content/blogmap.txt"

# Download a whole folder as a zip
curl -s -H "Authorization: Bearer $TOKEN" -o local/content.zip \
  "$KUDU/api/zip/site/wwwroot/content/"
```

Put downloads under `local/`, which is gitignored. The token lasts about an hour; re-run the
first line if you get a 401.

### Option C: Shell only (SSH)

`az webapp ssh --name msc4dnc --resource-group m4d-Web` (or Kudu → **SSH**) gives you a shell to
inspect or move files that are already on the box. It can't transfer files to or from your
machine; use A or B for that.

## Verification

- Download: the local file is non-empty and matches what you expected (`head`, or `diff`
  against the repo copy).
- Upload: list or download the file again and confirm the new content. If the app caches the
  file in memory, it also needs the app's reload action. For `blogmap.txt` / `helpmap.txt`, that's
  **Admin → Initialization Tasks → Update Sitemap** (see
  [link-new-blog-posts](link-new-blog-posts.md)).

## Rollback

Download the original **before** you overwrite it (Option B, or the file manager). To roll back,
upload that copy. Under `wwwroot`, a redeploy also restores the deployed version.

If Kudu returns 401/403 for an account that should have access, check the role assignment on the
app. Don't re-enable FTP or basic-auth publishing credentials to work around it.
