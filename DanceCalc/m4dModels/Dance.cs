using DanceLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace m4dModels
{
    public class Dance
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public virtual ICollection<DanceRating> DanceRatings { get; set; }
        public virtual ICollection<DanceLink> DanceLinks { get; set; }

        public string SmartLinks()
        {
            return SmartLinks(Description);
        }

        public static string SmartLinks(string s, bool byReference = false)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return @"<p>We're busy doing research and pulling together a general description for @Model.DanceName dance.  Please check back later for more info.</p>";
            }
            else if (byReference)
            {
                return LinkDancesReference(s);
            }
            else 
            {
                return LinkDances(s);
            }
        }

        private static Regex s_dex = new Regex(@"\[(?<dance>[^\]]*)\]", RegexOptions.Compiled);
        private static string LinkDances(string s)
        {
            StringBuilder sb = new StringBuilder();

            s = s.Replace("&lt;", "<");
            s = s.Replace("&gt;", ">");
            MatchCollection matches = s_dex.Matches(s);
            int i = 0;
            foreach (Match match in matches)
            {
                sb.Append(s.Substring(i, match.Index - i));
                string d = match.Groups["dance"].Value;
                if (!string.IsNullOrWhiteSpace(d))
                {
                    KeyValuePair<string,string>? l = FindLink(d);
                    if (l != null)
                    {
                        sb.AppendFormat("<a href='{0}'>{1}</a>", l.Value.Value, d);
                    }
                    else
                    {
                        sb.Append(d);
                    }
                }
                i = match.Index + match.Length;
            }
            sb.Append(s.Substring(i));

            return sb.ToString();
        }

        private static string LinkDancesReference(string s)
        {
            List<string> links = new List<string>();
            StringBuilder sb = new StringBuilder();

            MatchCollection matches = s_dex.Matches(s);
            int i = 0;
            foreach (Match match in matches)
            {
                sb.Append(s.Substring(i, match.Index - i));
                string d = match.Groups["dance"].Value;
                if (!string.IsNullOrWhiteSpace(d))
                {
                    KeyValuePair<string, string>? l = FindLink(d);
                    sb.AppendFormat(@"[{0}]", d);
                    if (l != null)
                    {
                        int idx = links.IndexOf(l.Value.Value);
                        if (idx < 0)
                        {
                            idx = links.Count;
                            links.Add(l.Value.Value);
                        }
                        sb.AppendFormat(@"[{0}]", idx);
                    }
                }
                i = match.Index + match.Length;
            }
            sb.Append(s.Substring(i));
            for (i = 0; i < links.Count; i++ )
            {
                sb.AppendFormat("\r\n[{0}]: {1}", i, links[i].Replace(" ","%20"));
            }

            return sb.ToString();
        }

        private static KeyValuePair<string,string>? FindLink(string d)
        {
            List<string> list = d.Split(new char[] { ' ', '\n', '\t', '\r', '\f', '\v' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            while (list.Count > 0)
            {
                KeyValuePair<string,string>? link = FindSpecificLink(string.Join(" ", list));
                if (link != null)
                {
                    return link;
                }
                list.RemoveAt(list.Count - 1);
            }

            return null;
        }

        private static KeyValuePair<string,string>? FindSpecificLink(string d)
        {
            DanceObject dance = Dances.Instance.AllDances.FirstOrDefault(dnc => string.Equals(dnc.Name, d, StringComparison.OrdinalIgnoreCase));
            if (dance != null)
            {
                return new KeyValuePair<string, string>(dance.Name.Replace(" ", "").ToLower(), string.Format("/Dances/{0}", dance.Name));
            }
            
            string link = null;
            d = d.ToLower();
            if (!s_links.TryGetValue(d,out link))
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError,String.Format("Link not found: {0}",d));
                return null;
            }

            return new KeyValuePair<string, string>(d.Replace(" ", ""), link);
        }

        static Dictionary<string, string> s_links = new Dictionary<string, string>()
        {
            {"swing music", "http://en.wikipedia.org/wiki/Swing_music"},
            {"tango music", "http://en.wikipedia.org/wiki/Tango"},
            {"international latin", "http://en.wikipedia.org/wiki/List_of_DanceSport_dances#Latin"},
            {"international standard", "http://en.wikipedia.org/wiki/List_of_DanceSport_dances#Ballroom"},
            {"american smooth", "http://en.wikipedia.org/wiki/List_of_DanceSport_dances#Smooth"},
            {"american rhythm", "http://en.wikipedia.org/wiki/List_of_DanceSport_dances#Rhythm"},
        };
        public bool Update(IList<string> cells)
        {
            bool modified = false;
            if (cells.Count > 0)
            {
                string desc = cells[0];
                cells.RemoveAt(0);
                if (string.IsNullOrWhiteSpace(desc))
                {
                    desc = null;
                }
                else
                {
                    desc = desc.Replace(@"\r", "\r").Replace(@"\n", "\n");
                }
                if (!string.Equals(desc,Description,StringComparison.Ordinal))
                {
                    modified = true;
                    Description = desc;
                }
            }

            if (cells.Count > 0)
            {
                if (DanceLinks == null)
                {
                    DanceLinks = new List<DanceLink>();
                }
            }

            for (int i = 0; i < cells.Count; i += 3)
            {
                Guid id = new Guid(cells[i]);
                DanceLink dl = DanceLinks.FirstOrDefault(l => l.Id == id);
                if (dl != null)
                {
                    if (!string.Equals(cells[i+1],dl.Description,StringComparison.Ordinal))
                    {
                        dl.Description = cells[i + 1];
                        modified = true;
                    }
                    if (!string.Equals(cells[i +2], dl.Link, StringComparison.Ordinal))
                    {
                        modified = true;
                        dl.Description = cells[i + 2];
                    }
                }
                else
                {
                    DanceLinks.Add(new DanceLink { Id = id, Description = cells[i + 1], Link = cells[i + 2] });
                }
            }

            return modified;
        }

        public DanceObject Info
        {
            get
            {
                if (_info == null)
                {
                    _info = DanceLibrary.DanceDictionary[Id];
                }

                return _info;
            }
        }

        public string Name
        {
            get
            {
                return Info.Name;
            }
        }

        public string Serialize()
        {
            string id = Id;
            string desc = Description ?? "";
            desc = desc.Replace("\r", @"\r").Replace("\n", @"\n").Replace('\t', ' ');
            StringBuilder sb = new StringBuilder();

            sb.AppendFormat("{0}\t{1}", id, desc);
            if (DanceLinks != null)
            {
                foreach (var dl in DanceLinks)
                {
                    sb.AppendFormat("\t{0}\t{1}\t{2}", dl.Id, dl.Description, dl.Link);
                }
            }

            return sb.ToString();
        }

        private DanceObject _info;

        public static Dances DanceLibrary
        {
            get
            {
                return _dances;
            }
        }

        private static Dances _dances = global::DanceLibrary.Dances.Instance;
    }
}
