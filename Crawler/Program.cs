using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;

namespace Crawler
{
    class Program
    {
        static void Main(string[] args)
        {

            // Create a file for output named TestFile.txt.
            const string bf = @"c:\temp\crawler{0:yyyy-MM-dd}.txt";
            s_writer = new StreamWriter(string.Format(bf,DateTime.Now));

            const string root = "https://localhost:44301/";
            s_root = new Uri(root);
            s_queue.Enqueue(root);
            var lines = 0;
            while (s_queue.Count > 0)
            {
                HandleDocument(s_queue.Dequeue());
                lines += 1;
                if (lines%1000 == 0)
                {
                    s_writer.Flush();
                }
            }

            // Flush the output.
            s_writer.Flush();
            s_writer.Close();
        }

        static void HandleDocument(string url)
        {
            if (s_visited.Contains(url)) return;

            s_visited.Add(url);

            var uri = new Uri(url);
            if (!s_root.IsBaseOf(uri)) return;

            s_writer.WriteLine(url);

            var doc = LoadDocument(url);
            if (doc == null)
                return;

            foreach (var link in doc.DocumentNode.SelectNodes("//a"))
            {
                var attr = link.Attributes["href"];
                if (attr == null)
                    continue;

                var rfe = attr.Value;
                if (string.IsNullOrEmpty(rfe))
                    continue;

                if (s_ignore.Contains(rfe) || rfe.StartsWith("#"))
                    continue;

                var rf = HttpUtility.HtmlDecode(rfe);

                Uri urf;
                if (!Uri.TryCreate(rf, UriKind.Absolute, out urf))
                {
                    if (!Uri.TryCreate(uri, rf, out urf))
                    {
                        var s = string.Format("ERROR: {0}, {1}", rf, url);
                        Trace.WriteLine(s);
                        s_writer.WriteLine(s);
                        continue;
                    }
                }

                var add = urf.ToString();
                if (!s_visited.Contains(add))
                    s_queue.Enqueue(add);
            }
        }

        static HtmlDocument LoadDocument(string url)
        {
            var s = LoadUrl(url);
            if (s == null) return null;
            var doc = new HtmlDocument();
            doc.LoadHtml(s);
            return doc;
        }
        static string LoadUrl(string url)
        {
            if (url == null)
            {
                return null;
            }

            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = WebRequestMethods.Http.Get;
            req.Accept = "text/html";

            try
            {
                HttpWebResponse response;
                using (response = (HttpWebResponse)req.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        var s = string.Format("ERROR: {0} : {1}", response.StatusCode, response.StatusDescription);
                        Trace.WriteLine(s);
                        s_writer.WriteLine(s);
                    }

                    using (var sr = new StreamReader(response.GetResponseStream()))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
            catch ( Exception e) {
                var s = string.Format("ERROR: {0}", e.Message);
                Trace.WriteLine(s);
                s_writer.WriteLine(s);
            }

            return null;
        }

        private static Uri s_root;
        private static HashSet<string> s_visited = new HashSet<string>();
        private static HashSet<string> s_ignore = new HashSet<string> {"/","#"};
        private static Queue<string> s_queue = new Queue<string>();
        private static TextWriter s_writer;
        //private static bool s_quit = false;
    }
}
