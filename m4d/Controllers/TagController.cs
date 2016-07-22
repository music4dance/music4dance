using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using m4d.ViewModels;
using m4dModels;

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
            var model = Database.OrderedTagTypes;
            return View(model);
        }

        // GET: Tag/Details/5
        public ActionResult Details(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var tagType = Database.TagTypes.Find(TagType.TagDecode(id));
            if (tagType == null)
            {
                return HttpNotFound();
            }
            return View(tagType);
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
        public ActionResult Create([Bind(Include = "Key,Count,PrimaryId")] TagType tagType)
        {
            if (ModelState.IsValid)
            {
                var tt = Database.DanceStats.FindOrCreateTagType(tagType.Key);
                if (tagType.PrimaryId != null)
                {
                    tt.PrimaryId = tagType.PrimaryId;
                    tt.Primary = Database.DanceStats.TagMap[tt.PrimaryId];
                }

                Database.UpdateAzureIndex(null);

                return RedirectToAction("Index");
            }

            SetupPrimary();
            return View(tagType);
        }

        private void SetupPrimary()
        {
            var tagTypes = Database.TagTypes.ToList();
            var nullT = new TagType();
            tagTypes.Insert(0, nullT);
            ViewBag.PrimaryId = new SelectList(tagTypes, "Key", "Key", string.Empty);
        }

        // GET: Tag/Edit/5
        public ActionResult Edit(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var tagType = Database.TagTypes.Find(TagType.TagDecode(id));
            if (tagType == null)
            {
                return HttpNotFound();
            }
            var pid = tagType.PrimaryId ?? tagType.Key;

            ViewBag.PrimaryId = new SelectList(Database.TagTypes, "Key", "Key", pid);
            return View(new TagTypeView(tagType));
        }

        // POST: Tag/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Key,Count,PrimaryId")] TagType tagType, string newKey)
        {
            if (!ModelState.IsValid)
            {
                var pid = tagType.PrimaryId ?? tagType.Key;

                ViewBag.PrimaryId = new SelectList(Database.TagTypes, "Key", "Key", pid);
                return View(tagType);
            }

            // TODONEXT:Figure out how to get edit and delete working:  cases are 
            //  Delete an existing tag (do we want to check for usage?)
            //  Rename an existing tag (change in place or add/delete? - do we chance all user instances) - rebuild all tag summaries if primary
            //  Change a tag to not be primary (rebuild all tag summaries - search on old primary)
            //  Change a tag to be primary (rebuild all tag summaries - search on old primary)
            string oldTagkey = null;
            if (string.Equals(tagType.Key, newKey))
            {
                if (string.Equals(tagType.Key, tagType.PrimaryId))
                {
                    tagType.PrimaryId = null;
                    tagType.Primary = null;
                }
                else
                {
                    oldTagkey = tagType.Key;
                }
                Context.Entry(tagType).State = EntityState.Modified;
                //Database.DanceStats.
            }
            else
            {
                // Save off the parent
                if (string.Equals(tagType.Key, tagType.PrimaryId))
                {
                    tagType.PrimaryId = null;
                    tagType.Primary = null;
                    tagType.Modified = DateTime.Now;
                }

                // Save off the children
                var oldTagType = Database.TagTypes.Find(tagType.Key);
                var children = new List<string>();
                if (oldTagType.Ring != null)
                {
                    children.AddRange(oldTagType.Ring.Select(child => child.Key));
                    oldTagType.Ring.Clear();
                }

                // Delete the old type
                Database.TagTypes.Remove(oldTagType);
                Database.SaveChanges();

                // Add the new type and put back in the child references
                var temp = new TagType(newKey);
                var newTagType = Database.FindOrCreateTagType(tagType.Value, temp.Category, temp.PrimaryId);
                newTagType.Modified = DateTime.Now;

                foreach (var child in children.Select(c => Database.TagTypes.Find(c)))
                {
                    child.PrimaryId = newKey;
                    child.Primary = newTagType;
                    if (newTagType.Ring.All(tt => tt.Key != newKey))
                    {
                        newTagType.Ring.Add(child);
                    }
                    child.Modified = DateTime.Now;
                }
                Database.SaveChanges();

                var filter = SongFilter.Default;
                filter.Tags = oldTagType.Key;
                var parameters = Database.AzureParmsFromFilter(filter);

                var stats = Database.DanceStats;
                while (Database.UpdateAzureIndex(Database.TakeTail(parameters, 1000).Where(song => song.UpdateTagSummaries(stats))) != 0)
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo, $"Updated a new batch of songs");

                }
            }
            return RedirectToAction("Index");
        }

        // GET: Tag/Delete/5
        public ActionResult Delete(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var tagType = Database.TagTypes.Find(TagType.TagDecode(id));
            if (tagType == null)
            {
                return HttpNotFound();
            }
            return View(tagType);
        }

        // POST: Tag/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(string id)
        {
            DanceMusicService.BlowTagCache();

            var tagType = Database.TagTypes.Find(TagType.TagDecode(id));
            Database.TagTypes.Remove(tagType);
            Database.SaveChanges();
            return RedirectToAction("Index");
        }
    }
}
