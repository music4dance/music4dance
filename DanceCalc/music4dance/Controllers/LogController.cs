using SongDatabase.Models;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace music4dance.Controllers
{
    public class LogController : Controller
    {
        private DanceMusicContext _db = new DanceMusicContext();

        //
        // GET: /Log/
        public ActionResult Index()
        {
            var lines = from l in _db.Log
                        select l;

            return View(lines);
        }

        public FileResult Lines()
        {
            var lines = from l in _db.Log
                        select l;

            StringBuilder sb = new StringBuilder();
            
            foreach (SongLog line in lines)
            {               
                sb.AppendFormat("{0}|{1}|{2}|{3}|{4}|{5}\r\n",line.User.UserName,line.Time,line.Action,line.SongReference,line.SongSignature,line.Data);
            }

            string s = sb.ToString();
            var bytes = Encoding.UTF8.GetBytes(s);
            MemoryStream stream = new MemoryStream(bytes);

            return File(stream, "text/plain", "log.txt");
        }

        public ActionResult RestoreLines()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")]
        public ActionResult RestoreResults()
        {
            HttpFileCollectionBase files = Request.Files;
            if (files.Count == 1)
            {
                List<string> lines = new List<string>();

                string key = files.AllKeys[0];
                ViewBag.Key = key;
                ViewBag.Size = files[key].ContentLength;
                ViewBag.ContentType = files[key].ContentType;


                HttpPostedFileBase file = Request.Files.Get(0);
                System.IO.Stream stream = file.InputStream;

                TextReader tr = new StreamReader(stream);

                string s = null;
                while ((s = tr.ReadLine()) != null)
                {
                    lines.Add(s);
                }

                ViewBag.Lines = lines;

                _db.RestoreFromLog(lines);

                return View();
            }
            else
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "No File Uploaded");
            }
        }

        protected override void Dispose(bool disposing)
        {
            _db.Dispose();
            base.Dispose(disposing);
        }
	}
}