using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using DanceLibrary;

namespace m4dModels
{
    public class DanceCore
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public List<DanceLink> DanceLinks { get; set; }
    }

    public class Dance : DanceCore
    {
        public static readonly string[] Categories =
        [
            "International Standard", "International Latin", "American Smooth", "American Rhythm"
        ];

        private static readonly Dictionary<string, string> Links = new()
        {
            { "swing music", "http://en.wikipedia.org/wiki/Swing_music" },
            { "tango music", "http://en.wikipedia.org/wiki/Tango" }
        };

        private DanceObject _info;
        public DateTime Modified { get; set; }

        public DanceObject Info => _info ??= DanceLibrary.DanceFromId(Id);

        public string Name => Info.Name;

        public static Dances DanceLibrary { get; } = Dances.Instance;

        public bool Update(IList<string> cells)
        {
            var modified = false;
            if (cells.Count > 0)
            {
                var desc = cells[0];
                cells.RemoveAt(0);
                desc = string.IsNullOrWhiteSpace(desc)
                    ? null
                    : desc.Replace(@"\r", "\r").Replace(@"\n", "\n");
                if (!string.Equals(desc, Description, StringComparison.Ordinal))
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
                DanceLinks = [];
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

                    if (string.Equals(cells[i + 2], dl.Link, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    modified = true;
                    dl.Description = cells[i + 2];
                }
                else
                {
                    DanceLinks.Add(
                        new DanceLink
                        {
                            Id = id, DanceId = Id, Description = cells[i + 1], Link = cells[i + 2]
                        });
                    modified = true;
                }
            }

            return modified;
        }

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
                    sb.AppendFormat("\t{0}\t{1}\t{2}", dl.Id, dl.Description, dl.Link);
                }
            }

            return sb.ToString();
        }
    }
}
