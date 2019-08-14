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
            // The tagGroup coming in is the original tagGroup with a possibly edited Primary Key
            //  newKey is the key typed into the key field
            if (!ModelState.IsValid)
            {
                var pid = tagGroup.PrimaryId ?? tagGroup.Key;

                ViewBag.PrimaryId = new SelectList(Database.TagGroups, "Key", "Key", pid);
                return View(tagGroup);
            }

            //  Rename an existing tag: (oldTag.Key != tagGroup.Key), don't care about primary
            //   - Create tag of new name
            //   - Point this tag to new tag as primary
            //  Change a tag to not be primary: tagGroup.PrimaryId != tagGroup.Key
            //  Change a tag to be primary: tagGroup.PrimaryId == tagGroup.Key
            //
            //  In all cases we want to search on the old primary key for the songs to fix up

            var oldTag = Database.TagGroups.Find(tagGroup.Key);
            if (oldTag == null)
            {
                throw new ArgumentOutOfRangeException(nameof(newKey));
            }
            tagGroup.Key = newKey;

            // Before doing anything else, we're going to get the filter for the
            //  potentially affected songs
            var filter = FilterFromTag(oldTag.Key);

            // If the tagGroup's key is now the same as the primary key,
            //  we set the primary key to null (self-referenced)
            if (string.Equals(tagGroup.Key, tagGroup.PrimaryId))
            {
                tagGroup.PrimaryId = null;
                tagGroup.Primary = null;
            }

            // Nothing Changed, just return
            if (string.Equals(tagGroup.Key, oldTag.Key) && string.Equals(tagGroup.PrimaryId, oldTag.PrimaryId))
            {
                return View(tagGroup);
            }

            // Create tag group with new name and point old tag group to it
            if (!string.Equals(oldTag.Key, tagGroup.Key))
            {
                var newTag = Database.TagGroups.Create();
                newTag.Key = tagGroup.Key;
                newTag.PrimaryId = null;
                Database.TagGroups.Add(newTag);
                oldTag.PrimaryId = newTag.Key;
                oldTag.Primary = newTag;
                oldTag.Modified = DateTime.Now;

                Database.DanceStats.TagManager.AddTagGroup(newTag);
            }
            // Reset the primary key of the old tag group
            else 
            {
                oldTag.PrimaryId = tagGroup.PrimaryId;
                oldTag.Modified = DateTime.Now;
            }

            Database.DanceStats.TagManager.UpdateTagRing(oldTag.Key, oldTag.PrimaryId);

            var parameters = new SearchParameters { Filter = filter };

            SearchContinuationToken tok = null;
            do
            {
                Database.UpdateAzureIndex(Database.TakePage(parameters, 1000, ref tok));
                Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Updated another batch of tags");
            } while (tok != null);

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
