using System;
using System.Linq;
using System.Net;
using AutoMapper;
using m4d.ViewModels;
using m4dModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;

namespace m4d.Controllers
{
    public class TagController : DanceMusicController
    {
        public TagController(DanceMusicContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService, IDanceStatsManager danceStatsManager, IConfiguration configuration) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
            HelpPage = "tag-cloud";
        }
        public override string DefaultTheme => MusicTheme;

        // GET: Tag
        [AllowAnonymous]
        public IActionResult Index([FromServices] IMapper mapper)
        {
            var model = Database.OrderedTagGroups
                .Where(t => 
                    !string.Equals(t.Category,"dance", StringComparison.InvariantCultureIgnoreCase) &&
                    !t.Value.StartsWith("!") &&
                    t.Count > 0)
                .Select(mapper.Map<TagModel>);
            return View(model);
        }

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

        // GET: Tag/Create
        public ActionResult Create()
        {
            SetupPrimary();
            return View();
        }

        // POST: Tag/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create([Bind("Key,PrimaryId")] TagGroup tagGroup)
        {
            if (ModelState.IsValid)
            {
                var tt = Database.DanceStats.TagManager.FindOrCreateTagGroup(tagGroup.Key);
                if (tagGroup.PrimaryId != null)
                {
                    tt.PrimaryId = tagGroup.PrimaryId;
                    tt.Primary = Database.DanceStats.TagManager.TagMap[tt.PrimaryId];
                }

                Database.UpdateAzureIndex(null);

                return RedirectToAction("List");
            }

            SetupPrimary();
            return View(tagGroup);
        }

        private void SetupPrimary()
        {
            var tagGroups = Database.TagGroups.ToList();
            var nullT = new TagGroup();
            tagGroups.Insert(0, nullT);
            ViewBag.PrimaryId = new SelectList(tagGroups, "Key", "Key", string.Empty);
        }

        // GET: Tag/Edit/5
        public IActionResult Edit(string id)
        {
            var code = GetTag(id, out var tagGroup);
            if (HttpStatusCode.OK != code)
            {
                return StatusCode((int)code);
            }

            var pid = tagGroup.PrimaryId ?? tagGroup.Key;

            ViewBag.PrimaryId = new SelectList(Database.TagGroups, "Key", "Key", pid);
            return View(new TagGroupView(tagGroup));
        }

        // POST: Tag/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind("Key,PrimaryId")] TagGroup tagGroup, string newKey)
        {
            // The tagGroup coming in is the original tagGroup with a possibly edited Primary Key
            //  newKey is the key typed into the key field
            if (!ModelState.IsValid)
            {
                var pid = tagGroup.PrimaryId ?? tagGroup.Key;

                ViewBag.PrimaryId = new SelectList(Database.TagGroups, "Key", "Key", pid);
                return View(tagGroup);
            }

            var oldTag = Database.TagGroups.Find(tagGroup.Key);
            if (oldTag == null)
            {
                throw new ArgumentOutOfRangeException(nameof(newKey));
            }

            return Database.UpdateTag(oldTag, newKey, tagGroup.PrimaryId) ?
                RedirectToAction("List") as ActionResult :            
                View(tagGroup);
        }


        // GET: Tag/Delete/5
        public ActionResult Delete(string id)
        {
            var code = GetTag(id, out var tagGroup);
            if (HttpStatusCode.OK != code)
            {
                return StatusCode((int)code);
            }

            return View(tagGroup);
        }

        // POST: Tag/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(string id)
        {
            // TODO: Should we consider guarding this?  If the tagtype is being used we may screw ourselves
            var tagGroup = Database.TagGroups.Find(TagGroup.TagDecode(id));
            Database.DanceStats.TagManager.DeleteTagGroup(tagGroup.Key);
            Database.TagGroups.Remove(tagGroup);
            Database.SaveChanges();

            DanceMusicService.BlowTagCache();

            return RedirectToAction("List");
        }

        private HttpStatusCode GetTag(string id, out TagGroup tag)
        {
            tag = null;

            if (id == null)
            {
                return HttpStatusCode.BadRequest;
            }
            tag = Database.TagGroups.Find(TagGroup.TagDecode(id));
            if (tag == null)
            {
                return HttpStatusCode.NotFound;
            }

            return HttpStatusCode.OK;
        }
    }
}
