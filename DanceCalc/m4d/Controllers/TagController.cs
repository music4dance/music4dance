using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using m4d.Context;
using m4dModels;

namespace m4d.Controllers
{
    public class TagController : Controller
    {
        private DanceMusicContext db = new DanceMusicContext();

        // GET: Tag
        public ActionResult Index()
        {
            var tagTypes = db.TagTypes.Include(t => t.Primary);
            return View(tagTypes.ToList());
        }

        // GET: Tag/Details/5
        public ActionResult Details(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            TagType tagType = db.TagTypes.Find(id);
            if (tagType == null)
            {
                return HttpNotFound();
            }
            return View(tagType);
        }

        // GET: Tag/Create
        public ActionResult Create()
        {
            ViewBag.PrimaryId = new SelectList(db.TagTypes, "Key", "PrimaryId");
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
                db.TagTypes.Add(tagType);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.PrimaryId = new SelectList(db.TagTypes, "Key", "PrimaryId", tagType.PrimaryId);
            return View(tagType);
        }

        // GET: Tag/Edit/5
        public ActionResult Edit(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            TagType tagType = db.TagTypes.Find(id);
            if (tagType == null)
            {
                return HttpNotFound();
            }
            ViewBag.PrimaryId = new SelectList(db.TagTypes, "Key", "PrimaryId", tagType.PrimaryId);
            return View(tagType);
        }

        // POST: Tag/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Key,Count,PrimaryId")] TagType tagType)
        {
            if (ModelState.IsValid)
            {
                db.Entry(tagType).State = System.Data.Entity.EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.PrimaryId = new SelectList(db.TagTypes, "Key", "PrimaryId", tagType.PrimaryId);
            return View(tagType);
        }

        // GET: Tag/Delete/5
        public ActionResult Delete(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            TagType tagType = db.TagTypes.Find(id);
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
            TagType tagType = db.TagTypes.Find(id);
            db.TagTypes.Remove(tagType);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
