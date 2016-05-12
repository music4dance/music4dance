using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
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
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Entering Log.Index");
            var lines = from l in Database.Log
                        orderby l.Id descending
                        select l;

            var pageSize = 25;
            var pageNumber = (page ?? 1);
            Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Exiting Log.Index");
            return View(lines.ToPagedList(pageNumber, pageSize));
        }

        public FileResult Lines()
        {
            var lines = from l in Database.Log orderby l.Id
                        select l;

            var sb = new StringBuilder();
            
            foreach (var line in lines)
            {
                sb.AppendFormat("{0}\x1E{1}\x1E{2}\x1E{3}\x1E{4}\x1E{5}\r\n", line.User.UserName, line.Time, line.Action, line.SongReference, line.SongSignature, line.Data);
            }

            var s = sb.ToString();
            var bytes = Encoding.UTF8.GetBytes(s);
            var stream = new MemoryStream(bytes);

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
            var files = Request.Files;
            if (files.Count == 1)
            {
                var lines = new List<string>();

                var key = files.AllKeys[0];
                ViewBag.Key = key;
                var httpPostedFileBase = files[key];
                if (httpPostedFileBase != null)
                {
                    ViewBag.Size = httpPostedFileBase.ContentLength;
                    ViewBag.ContentType = httpPostedFileBase.ContentType;
                }

                var file = Request.Files.Get(0);
                if (file != null)
                {
                    var stream = file.InputStream;

                    TextReader tr = new StreamReader(stream);

                    string s;
                    while ((s = tr.ReadLine()) != null)
                    {
                        lines.Add(s);
                    }
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

            var user = Database.FindUser(User.Identity.Name);

            var results = Database.UndoLog(user, entries.ToList());

            return View(results);
        }
	}
}