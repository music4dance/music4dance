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
        public override string DefaultTheme
        {
            get
            {
                return MusicTheme;
            }
        }

        // GET: Tag
        [AllowAnonymous]
        public ActionResult Index()
        {
            var tagTypes = Database.TagTypes.Include(t => t.Primary).OrderBy(t => t.Key);
            return View(tagTypes.ToList());
        }

        // GET: Tag/Details/5
        public ActionResult Details(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            TagType tagType = Database.TagTypes.Find(TagType.TagDecode(id));
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
                Database.TagTypes.Add(tagType);
                Database.SaveChanges();
                return RedirectToAction("Index");
            }

            SetupPrimary();
            return View(tagType);
        }

        private void SetupPrimary()
        {
            List<TagType> tagTypes = Database.TagTypes.ToList();
            TagType nullTT = new TagType();
            tagTypes.Insert(0, nullTT);
            ViewBag.PrimaryId = new SelectList(tagTypes, "Key", "Key", string.Empty);
        }

        // GET: Tag/Edit/5
        public ActionResult Edit(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            TagType tagType = Database.TagTypes.Find(TagType.TagDecode(id));
            if (tagType == null)
            {
                return HttpNotFound();
            }
            string pid = tagType.PrimaryId;
            if (pid == null)
            {
                pid = tagType.Key;
            }
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
            if (ModelState.IsValid)
            {
                if (string.Equals(tagType.Key,newKey))
                {
                    if (string.Equals(tagType.Key,tagType.PrimaryId))
                    {
                        tagType.PrimaryId = null;
                        tagType.Primary = null;
                    }
                    Context.Entry(tagType).State = EntityState.Modified;
                    Database.SaveChanges();
                }
                else
                {
                    // NOTE: This should work more smoothly because the only foreign
                    //  keys dependant on this as a primary key are in the TagType table itself

                    // Save off the parent
                    if (string.Equals(tagType.Key, tagType.PrimaryId))
                    {
                        tagType.PrimaryId = null;
                        tagType.Primary = null;
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

                    foreach (var c in children)
                    {
                        var child = Database.TagTypes.Find(c);
                        child.PrimaryId = newKey;
                        child.Primary = newTagType;
                        if (newTagType.Ring.All(tt => tt.Key != newKey))
                        {
                            newTagType.Ring.Add(child);
                        }
                    }
                    Database.SaveChanges();

                    // Go through the Tag table and fix up all of the references
                    var songIds = new List<Guid>();
                    var tags = from t in Database.Tags where t.Tags.Summary.Contains(oldTagType.Key) select t;
                    foreach (var tag in tags)
                    {
                        tag.Tags.Summary = tag.Tags.Summary.Replace(oldTagType.Key, newKey);
                        newTagType.Count += 1;
                        
                        // TODO: We're handling songs and dance references now, but need to generalize to other taggable objects
                        //  Also should figure out how to pull out id in a more general way
                        if (tag.Id.StartsWith("S:"))
                        {
                            songIds.Add(new Guid(tag.Id.Substring(2)));
                        }
                        else if (tag.Id.StartsWith("X:"))
                        {
                            songIds.Add(new Guid(tag.Id.Substring(5)));
                        }
                        else
                        {
                            Trace.WriteLine("When did we start supporting tags on non-songs/dance references");
                        }
                    }
                    Database.SaveChanges();

                    // Use the tag table to reference all the affected songs and fix them
                    //  Both the TagSummary and the Properties
                    foreach (var song in songIds.Select(songId => Database.FindSong(songId)).Where(song => song != null))
                    {
                        song.TagSummary.Summary = song.TagSummary.Summary.Replace(oldTagType.Key, newKey);

                        foreach (var prop in song.SongProperties.Where(prop => prop.Name == SongBase.AddedTags || prop.Name == SongBase.RemovedTags).Where(prop => prop.Value.Contains(oldTagType.Key)))
                        {
                            prop.Value = prop.Value.Replace(oldTagType.Key, newKey);
                        }

                        foreach (var dr in song.DanceRatings)
                        {
                            dr.TagSummary.Summary = dr.TagSummary.Summary.Replace(oldTagType.Key, newKey);
                        }
                    }
                    Database.SaveChanges();
                }
                return RedirectToAction("Index");
            }
            var pid = tagType.PrimaryId ?? tagType.Key;

            ViewBag.PrimaryId = new SelectList(Database.TagTypes, "Key", "Key", pid);
            return View(tagType);
        }

        // GET: Tag/Delete/5
        public ActionResult Delete(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            TagType tagType = Database.TagTypes.Find(TagType.TagDecode(id));
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
            TagType tagType = Database.TagTypes.Find(TagType.TagDecode(id));
            Database.TagTypes.Remove(tagType);
            Database.SaveChanges();
            return RedirectToAction("Index");
        }
    }
}
