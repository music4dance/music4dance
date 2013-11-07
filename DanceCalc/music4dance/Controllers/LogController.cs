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
                sb.AppendFormat("{0}|{1}|{2}|{3}|{4}\r\n",line.User.UserName,line.Time,line.Action,line.SongReference,line.Data);
            }

            string s = sb.ToString();
            var bytes = Encoding.UTF8.GetBytes(s);
            MemoryStream stream = new MemoryStream(bytes);

            return File(stream, "text/plain", "log.txt");
        }

        protected override void Dispose(bool disposing)
        {
            _db.Dispose();
            base.Dispose(disposing);
        }
	}
}