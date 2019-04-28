using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using m4d.ViewModels;
using m4dModels;
using Microsoft.Azure.Search.Models;

namespace m4d.Controllers
{
    public class TagController : DMController
    {
        public TagController()
        {
            HelpPage = "tag-cloud";
        }
        public override string DefaultTheme => MusicTheme;

        // GET: Tag
        [AllowAnonymous]
        public ActionResult Index()
        {
            var model = Database.OrderedTagGroups;
            return View(model);
        }

        // GET: Tag/Details/5
        public ActionResult Details(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var tagGroup = Database.TagGroups.Find(TagGroup.TagDecode(id));
            if (tagGroup == null)
            {
                return HttpNotFound();
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
        public ActionResult Create([Bind(Include = "Key,PrimaryId")] TagGroup tagGroup)
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

                return RedirectToAction("Index");
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
        public ActionResult Edit(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var tagGroup = Database.TagGroups.Find(TagGroup.TagDecode(id));
            if (tagGroup == null)
            {
                return HttpNotFound();
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
        public ActionResult Edit([Bind(Include = "Key,PrimaryId")] TagGroup tagGroup, string newKey)
        {
            if (!ModelState.IsValid)
            {
                var pid = tagGroup.PrimaryId ?? tagGroup.Key;

                ViewBag.PrimaryId = new SelectList(Database.TagGroups, "Key", "Key", pid);
                return View(tagGroup);
            }

            //  Rename an existing tag (change in place or add/delete? - do we chance all user instances) - rebuild all tag summaries if primary
            //  Change a tag to not be primary (rebuild all tag summaries - search on old primary)
            //  Change a tag to be primary (rebuild all tag summaries - search on old primary)

            var changed = false;

            var oldTag = Database.TagGroups.Find(tagGroup.Key);
            tagGroup.Key = newKey;

            if (string.Equals(oldTag.Key, tagGroup.PrimaryId))
            {
                tagGroup.PrimaryId = null;
                tagGroup.Primary = null;
            }

            if (!string.Equals(oldTag.Key, tagGroup.Key))
            {
                Database.DanceStats.TagManager.ChangeTagName(oldTag.Key, tagGroup.Key);
                Database.TagGroups.Remove(oldTag);
                var newTag = Database.TagGroups.Create();
                newTag.Key = tagGroup.Key;
                newTag.PrimaryId = tagGroup.PrimaryId;
                Database.TagGroups.Add(newTag);
                changed = true;
            }

            if (tagGroup.PrimaryId != oldTag.PrimaryId)
            {
                changed = true;
                string filter = null;
                // Removed this tag from an existing ring
                if (tagGroup.PrimaryId == null)
                {
                    var primary = oldTag.GetPrimary();
                    filter = FilterFromTag(primary.Key);
                }
                // Added this type to a ring
                else if (oldTag.PrimaryId == null)
                {
                    var primary = tagGroup.GetPrimary();
                    filter = FilterFromTag(primary.Key);
                }
                // Moved this from one ring to another
                else
                {
                    var primaryA = oldTag.GetPrimary();
                    var primaryB = tagGroup.GetPrimary();

                    filter = $"{FilterFromTag(primaryA.Key)} or {FilterFromTag(primaryB.Key)}";
                }

                oldTag.PrimaryId = tagGroup.PrimaryId;
                oldTag.Modified = DateTime.Now;
                Database.DanceStats.TagManager.UpdateTagRing(oldTag.Key, oldTag.PrimaryId);

                var parameters = new SearchParameters {Filter=filter};

                while (Database.UpdateAzureIndex(Database.TakeTail(parameters, 1000)) != 0)
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo,"Updated another batch of tags");
                };
            }

            if (changed)
                Database.SaveChanges();

            return RedirectToAction("Index");
        }

        private string FilterFromTag(string key)
        {
            var filter = SongFilter.Default;
            filter.Tags = key;
            var parameters = Database.AzureParmsFromFilter(filter);
            return parameters.Filter;
        }

        // GET: Tag/Delete/5
        public ActionResult Delete(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var tagGroup = Database.TagGroups.Find(TagGroup.TagDecode(id));
            if (tagGroup == null)
            {
                return HttpNotFound();
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

            return RedirectToAction("Index");
        }
    }
}
