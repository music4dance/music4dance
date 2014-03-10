using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace m4d.Controllers
{
    public class DanceController : Controller
    {
        //
        // GET: /Dance/

        public ActionResult Index()
        {
            return View();
        }

        //
        // GET: /Dance/Details/5

        public ActionResult Details(int id)
        {
            return View();
        }

        //
        // GET: /Dance/Create

        public ActionResult Create()
        {
            return View();
        }

        //
        // POST: /Dance/Create

        [HttpPost]
        public ActionResult Create(FormCollection collection)
        {
            try
            {
                // TODO: Add insert logic here

                return RedirectToAction("Index");
            }
            catch
            {
                return View();
            }
        }

        //
        // GET: /Dance/Edit/5

        public ActionResult Edit(int id)
        {
            return View();
        }

        //
        // POST: /Dance/Edit/5

        [HttpPost]
        public ActionResult Edit(int id, FormCollection collection)
        {
            try
            {
                // TODO: Add update logic here

                return RedirectToAction("Index");
            }
            catch
            {
                return View();
            }
        }

        //
        // GET: /Dance/Delete/5

        public ActionResult Delete(int id)
        {
            return View();
        }

        //
        // POST: /Dance/Delete/5

        [HttpPost]
        public ActionResult Delete(int id, FormCollection collection)
        {
            try
            {
                // TODO: Add delete logic here

                return RedirectToAction("Index");
            }
            catch
            {
                return View();
            }
        }
    }
}
