using m4d.Services;
using m4d.ViewModels;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;

using System.Net;

namespace m4d.Controllers;

public class TagController : DanceMusicController
{
    public TagController(
        DanceMusicContext context, UserManager<ApplicationUser> userManager,
        ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
        IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backroundTaskQueue,
        IFeatureManagerSnapshot featureManager, ILogger<ActivityLogController> logger) :
        base(context, userManager, searchService, danceStatsManager, configuration,
            fileProvider, backroundTaskQueue, featureManager, logger)
    {
        HelpPage = "tag-cloud";
    }

    // GET: Tag
    [AllowAnonymous]
    public IActionResult Index()
    {
        return Vue3(
            "Tag Cloud",
            "Explore songs based on musical genre, tempo, style and other tags.",
            "tag-index",
            danceEnvironment: true,
            tagEnvironment: true);
    }

    [Authorize(Roles = "dbAdmin")]
    public IActionResult List()
    {
        var model = Database.OrderedTagGroups;
        return View(model);
    }

    // GET: Tag/Details/5
    public IActionResult Details(string id)
    {
        var code = GetTag(id, out var tagGroup);
        if (HttpStatusCode.OK != code)
        {
            return StatusCode((int)code);
        }

        return View(tagGroup);
    }

    private void SetupPrimary()
    {
        var tagGroups = Database.OrderedTagGroups.ToList();
        var nullT = new TagGroup();
        tagGroups.Insert(0, nullT);
        ViewBag.PrimaryId = new SelectList(tagGroups, "Key", "Key", string.Empty);
    }

    // GET: Tag/Edit/5
    [Authorize(Roles = "dbAdmin")]
    public IActionResult Edit(string id)
    {
        var code = GetTag(id, out var tagGroup);
        if (HttpStatusCode.OK != code)
        {
            return StatusCode((int)code);
        }

        return GetEditor(tagGroup);
    }

    private IActionResult GetEditor(TagGroup tagGroup)
    {
        ViewBag.PrimaryId = new SelectList(Database.OrderedTagGroups,
            "Key", "Key", tagGroup.PrimaryId ?? tagGroup.Key);
        return View("Edit", new TagGroupView(tagGroup));
    }

    // POST: Tag/Edit/5
    // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
    // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
    [Authorize(Roles = "dbAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([Bind("Key,PrimaryId")] TagGroup tagGroup,
        string newKey)
    {
        // The tagGroup coming in is the original tagGroup with a possibly edited Primary Key
        //  newKey is the key typed into the key field
        if (!ModelState.IsValid)
        {
            return GetEditor(tagGroup);
        }

        if (!Database.TagMap.TryGetValue(tagGroup.Key, out var oldTag))
        {
            throw new ArgumentOutOfRangeException(nameof(newKey));
        }

        if (tagGroup.Key != newKey && oldTag.PrimaryId != null && tagGroup.PrimaryId != oldTag.PrimaryId)
        {
            ModelState.AddModelError("PrimaryId", "Can't change both the Key and the PrimaryKey at the same time");
            return GetEditor(tagGroup);
        }
        else if (tagGroup.Key != newKey)
        {
            if (await Database.RenameTag(tagGroup, newKey))
            {
                return RedirectToAction("List");
            }
        }
        else if (tagGroup.PrimaryId != oldTag.PrimaryId)
        {
            if (await Database.SetPrimaryTag(tagGroup, tagGroup.PrimaryId))
            {
                return RedirectToAction("List");
            }
        }

        return GetEditor(tagGroup);
    }

    // GET: Tag/Delete/5
    [Authorize(Roles = "dbAdmin")]
    public ActionResult Delete(string id)
    {
        var code = GetTag(id, out var tagGroup);
        if (HttpStatusCode.OK != code)
        {
            return StatusCode((int)code);
        }

        if (string.IsNullOrWhiteSpace(tagGroup.PrimaryId))
        {
            return StatusCode((int)HttpStatusCode.NotAcceptable);
        }

        return View(tagGroup);
    }

    // POST: Tag/Delete/5
    [HttpPost]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "dbAdmin")]
    public async Task<ActionResult> DeleteConfirmed(string id)
    {
        var tagGroup = await Database.TagGroups.FindAsync(TagGroup.TagDecode(id));
        if (tagGroup == null || string.IsNullOrWhiteSpace(tagGroup.PrimaryId))
        {
            return StatusCode((int)HttpStatusCode.NotAcceptable);
        }
        Database.DanceStats.TagManager.DeleteTagGroup(tagGroup.Key);
        _ = Database.TagGroups.Remove(tagGroup);
        _ = await Database.SaveChanges();

        return RedirectToAction("List");
    }

    [Authorize(Roles = "dbAdmin")]
    public async Task<ActionResult> CleanupTags()
    {
        var tagMap = Database.DanceStats.TagManager.TagMap;
        var delete = Database.TagGroups.AsEnumerable()
            .Where(t => !tagMap.ContainsKey(t.Key) || !tagMap[t.Key].IsConected)
            .ToList();

        foreach (var tag in delete)
        {
            Database.Context.RemoveRange(delete);
        }
        _ = await Database.SaveChanges();

        return RedirectToAction("List");
    }

    private HttpStatusCode GetTag(string id, out TagGroup tag)
    {
        tag = null;

        if (id == null)
        {
            return HttpStatusCode.BadRequest;
        }

        var decoded = TagGroup.TagDecode(id);
        tag = Database.TagGroups.Find(decoded);

        if (tag == null && !Database.DanceStats.TagManager.TagMap.TryGetValue(decoded, out tag))
        {
            return HttpStatusCode.NotFound;
        }

        return HttpStatusCode.OK;
    }
}
