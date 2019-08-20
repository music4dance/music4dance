using System;
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

            var oldTag = Database.TagGroups.Find(tagGroup.Key);
            if (oldTag == null)
            {
                throw new ArgumentOutOfRangeException(nameof(newKey));
            }

            return Database.UpdateTag(oldTag, newKey, tagGroup.PrimaryId) ?
                RedirectToAction("Index") as ActionResult :            
                View(tagGroup);
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
