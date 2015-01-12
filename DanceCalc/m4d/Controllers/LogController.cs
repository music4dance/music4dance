using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Mvc;
using m4dModels;
using PagedList;

namespace m4d.Controllers
{
    public class LogController : DMController
    {
        //
        // GET: /Log/
        [AllowAnonymous]
        public ActionResult Index(int? page)
        {
            Trace.WriteLine("Entering Log.Index");
            var lines = from l in Database.Log
                        orderby l.Id descending
                        select l;

            int pageSize = 25;
            int pageNumber = (page ?? 1);
            Trace.WriteLine("Exiting Log.Index");
            return View(lines.ToPagedList(pageNumber, pageSize));
        }

        public FileResult Lines()
        {
            var lines = from l in Database.Log orderby l.Id
                        select l;

            StringBuilder sb = new StringBuilder();
            
            foreach (SongLog line in lines)
            {
                sb.AppendFormat("{0}\x1E{1}\x1E{2}\x1E{3}\x1E{4}\x1E{5}\r\n", line.User.UserName, line.Time, line.Action, line.SongReference, line.SongSignature, line.Data);
            }

            string s = sb.ToString();
            var bytes = Encoding.UTF8.GetBytes(s);
            MemoryStream stream = new MemoryStream(bytes);

            return File(stream, "text/plain", "log.txt");
        }

        [Authorize(Roles = "canEdit")]
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
                Stream stream = file.InputStream;

                TextReader tr = new StreamReader(stream);

                string s;
                while ((s = tr.ReadLine()) != null)
                {
                    lines.Add(s);
                }

                ViewBag.Lines = lines;

                Database.RestoreFromLog(lines);

                return View();
            }
            else
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "No File Uploaded");
            }
        }

        //
        // Merge: /Log/Undo
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "canEdit")]
        public ActionResult Undo(int[] undo)
        {
            var entries = from e in Database.Log
                            where undo.Contains(e.Id)
                            select e;

            ApplicationUser user = Database.FindUser(User.Identity.Name);

            IEnumerable<UndoResult> results = Database.UndoLog(user, entries.ToList());

            return View(results);
        }
	}
}