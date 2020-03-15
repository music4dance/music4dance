using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DanceLibrary;

namespace m4dModels
{
    public class Dance
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public DateTime Modified { get; set; }

        public List<DanceLink> DanceLinks { get; set; }

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
            return byReference ? LinkDancesReference(s) : LinkDances(s);
        }

        private static readonly Regex Dex = new Regex(@"\[(?<dance>[^\]]*)\]", RegexOptions.Compiled);
        private static string LinkDances(string s)
        {
            var sb = new StringBuilder();

            s = s.Replace("&lt;", "<");
            s = s.Replace("&gt;", ">");
            var matches = Dex.Matches(s);
            var i = 0;
            foreach (Match match in matches)
            {
                sb.Append(s.Substring(i, match.Index - i));
                var d = match.Groups["dance"].Value;
                if (IsDanceMatch(match, s))
                {
                    var l = FindLink(d);
                    if (l != null)
                    {
                        sb.AppendFormat("<a href='{0}'>{1}</a>", l.Value.Value, d);
                    }
                    else
                    {
                        sb.Append(d);
                    }
                }
                else
                {
                    sb.AppendFormat(@"[{0}]", d);
                }
                i = match.Index + match.Length;
            }
            sb.Append(s.Substring(i));

            return sb.ToString();
        }

        private static bool IsDanceMatch(Match match, string s)
        {
            return !string.IsNullOrWhiteSpace(match.Groups["dance"].Value) &&
                   (match.Index + match.Length == s.Length || s[match.Index + match.Length] != '(');
        }

        private static string LinkDancesReference(string s)
        {
            var links = new List<string>();
            var sb = new StringBuilder();

            var matches = Dex.Matches(s);
            var i = 0;
            foreach (Match match in matches)
            {
                sb.Append(s.Substring(i, match.Index - i));
                var d = match.Groups["dance"].Value;
                if (IsDanceMatch(match,s))
                {
                    var l = FindLink(d);
                    sb.AppendFormat(@"[{0}]", d);
                    if (l != null)
                    {
                        var idx = links.IndexOf(l.Value.Value);
                        if (idx < 0)
                        {
                            idx = links.Count;
                            links.Add(l.Value.Value);
                        }
                        sb.AppendFormat(@"[{0}]", idx);
                    }
                }
                else
                {
                    sb.AppendFormat(@"[{0}]", d);
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
            var list = d.Split(new[] { ' ', '\n', '\t', '\r', '\f', '\v' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            while (list.Count > 0)
            {
                var link = FindSpecificLink(string.Join(" ", list));
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
            var dance = Dances.Instance.AllDances.FirstOrDefault(dnc => string.Equals(dnc.Name, d, StringComparison.OrdinalIgnoreCase));
            if (dance != null)
            {
                return new KeyValuePair<string, string>(dance.Name.Replace(" ", "").ToLower(),
                    $"/dances/{dance.CleanName}");
            }

            var cat = Categories.FirstOrDefault(c => string.Equals(d, c, StringComparison.OrdinalIgnoreCase));
            if (cat != null)
            {
                return new KeyValuePair<string, string>(cat.Replace(" ", "").ToLower(),
                    $"/dances/{DanceObject.SeoFriendly(cat)}");
            }

            d = d.ToLower();
            if (Links.TryGetValue(d, out var link)) return new KeyValuePair<string, string>(d.Replace(" ", ""), link);

            Trace.WriteLineIf(TraceLevels.General.TraceError, $"Link not found: {d}");
            return null;
        }

        public static readonly string[] Categories = {"International Standard", "International Latin", "American Smooth", "American Rhythm"};

        static readonly Dictionary<string, string> Links = new Dictionary<string, string>()
        {
            {"swing music", "http://en.wikipedia.org/wiki/Swing_music"},
            {"tango music", "http://en.wikipedia.org/wiki/Tango"},
        };

        public bool Update(IList<string> cells)
        {
            var modified = false;
            if (cells.Count > 0)
            {
                var desc = cells[0];
                cells.RemoveAt(0);
                desc = string.IsNullOrWhiteSpace(desc) ? null : desc.Replace(@"\r", "\r").Replace(@"\n", "\n");
                if (!string.Equals(desc,Description,StringComparison.Ordinal))
                {
                    modified = true;
                    Description = desc;
                }
            }

            if (cells.Count > 0)
            {
                if (!string.IsNullOrWhiteSpace(cells[0]) &&
                    DateTime.TryParse(cells[0], out var modTime))
                {
                    Modified = modTime;
                }
                cells.RemoveAt(0);
            }

            if (cells.Count > 0 && DanceLinks == null)
            {
                DanceLinks = new List<DanceLink>();
            }

            for (var i = 0; i < cells.Count; i += 3)
            {
                var id = new Guid(cells[i]);
                var dl = DanceLinks.FirstOrDefault(l => l.Id == id);
                if (dl != null)
                {
                    if (!string.Equals(cells[i + 1], dl.Description, StringComparison.Ordinal))
                    {
                        dl.Description = cells[i + 1];
                        modified = true;
                    }

                    if (string.Equals(cells[i + 2], dl.Link, StringComparison.Ordinal)) continue;

                    modified = true;
                    dl.Description = cells[i + 2];
                }
                else
                {
                    DanceLinks.Add(new DanceLink { Id = id, DanceId = Id, Description = cells[i + 1], Link = cells[i + 2] });
                    modified = true;
                }
            }

            return modified;
        }

        public DanceObject Info => _info ??= DanceLibrary.DanceFromId(Id);

        public string Name => Info.Name;

        public string Serialize()
        {
            var id = Id;
            var desc = Description ?? "";
            desc = desc.Replace("\r", @"\r").Replace("\n", @"\n").Replace('\t', ' ');
            var sb = new StringBuilder();

            sb.AppendFormat("{0}\t{1}\t{2}", id, desc, Modified.ToString("g"));
            if (DanceLinks != null)
            {
                foreach (var dl in DanceLinks)
                {
                    sb.AppendFormat("\t{0}\t{1}\t{2}", dl.Id, dl.Description, dl.Link );
                }
            }

            return sb.ToString();
        }

        private DanceObject _info;

        public static Dances DanceLibrary { get; } = Dances.Instance;
    }
}
