using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace m4dModels
{
    public class SongSort
    {
        public const string Dances = SongIndex.DancesField;
        public const string Modified = SongIndex.ModifiedField;
        public const string Edited = SongIndex.EditedField;
        public const string Created = SongIndex.CreatedField;
        public const string Closest = "Closest";
        public const string Comments = SongIndex.CommentsField;
        public const string Tempo = Song.TempoField;
        public const string Length = Song.LengthField;
        public const string Beat = SongIndex.BeatField;
        public const string Mood = SongIndex.MoodField;
        public const string Energy = Song.EnergyField;

        private static readonly string[] s_directional =
        {
            Song.TitleField, Song.ArtistField, Song.TempoField, Song.LengthField,
            SongIndex.ModifiedField, SongIndex.CreatedField, SongIndex.EditedField,
            Song.EnergyField, SongIndex.MoodField, SongIndex.BeatField, SongIndex.CommentsField
        };

        private static readonly string[] s_numerical =
            { Song.TempoField, SongIndex.MoodField, SongIndex.CreatedField };

        private static readonly string[] s_intrinsic =
            { Song.EnergyField, SongIndex.MoodField, SongIndex.BeatField };

        public SongSort(string sort, bool hasQuery = false)
        {
            if (string.IsNullOrWhiteSpace(sort))
            {
                return;
            }

            var list = sort.Split('_').ToList();

            _id = list[0];
            list.RemoveAt(0);

            if (!string.IsNullOrEmpty(Id))
            {
                _id = $"{char.ToUpper(Id[0])}{Id[1..].ToLower()}";

                if (!(s_directional.Contains(Id) || s_intrinsic.Contains(Id) ||
                    s_numerical.Contains(Id) || string.Equals(Id, Dances)))
                {
                    _id = null;
                    return;
                }
            }

            if (list.Count > 0)
            {
                if (string.Equals(list[0], "desc", StringComparison.OrdinalIgnoreCase))
                {
                    Descending = true;
                    list.RemoveAt(0);
                }
                else if (string.Equals(list[0], "asc", StringComparison.OrdinalIgnoreCase))
                {
                    list.RemoveAt(0);
                }
            }

            if (list.Count > 0)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError, $"Bad Sort: {sort}");
            }
        }

        public string Id => String.IsNullOrEmpty(_id) && !HasQuery ? Dances : _id;
        public bool Descending { get; private set; }
        public bool HasQuery { get; private set; }

        private string _id { get; set; }

        public bool Numeric => s_intrinsic.Contains(Id) || Id == "Tempo";
        public bool Directional => s_directional.Contains(Id);

        public string FriendlyName
        {
            get
            {
                const string dr = "Dance Rating";
                const string cm = "Closest Match";

                switch (Id)
                {
                    case Dances:
                        return dr;
                    case Modified:
                        return "Last Modified";
                    case Edited:
                        return "Last Edited";
                    case Created:
                        return "When Added";
                    case Closest:
                        return cm;
                    case null:
                    case "":
                        return HasQuery ? cm : dr;
                    default:
                        return Id;
                }
            }
        }

        public IList<string> OData
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Id) || Id == Dances)
                {
                    return null;
                }

                var desc = Descending;
                if (Id == Modified || Id == Created || Id == Edited)
                {
                    desc = !desc;
                }

                var order = desc ? "desc" : "asc";
                return [$"{Id} {order}"];
            }
        }

        public string Description
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Id))
                {
                    return string.Empty;
                }

                var ret = new System.Text.StringBuilder();
                ret.AppendFormat(" Sorted by {0} from ", FriendlyName);

                if (!Descending)
                {
                    switch (Id)
                    {
                        case Tempo:
                            ret.Append("slowest to fastest");
                            break;
                        case Length:
                            ret.Append("shortest to longest");
                            break;
                        case Modified:
                        case Created:
                        case Comments:
                            ret.Append("newest to oldest");
                            break;
                        case Dances:
                            ret.Append("most popular to least popular");
                            break;
                        case Beat:
                            ret.Append("weakest to strongest");
                            break;
                        case Mood:
                            ret.Append("saddest to happiest");
                            break;
                        case Energy:
                            ret.Append("lowest to highest");
                            break;
                        default:
                            ret.Append("A to Z");
                            break;
                    }
                }
                else
                {
                    switch (Id)
                    {
                        case Tempo:
                            ret.Append("fastest to slowest");
                            break;
                        case Length:
                            ret.Append("longest to shortest");
                            break;
                        case Modified:
                        case Created:
                        case Comments:
                            ret.Append("oldest to newest");
                            break;
                        case Dances:
                            ret.Append("most popular to least popular");
                            break;
                        case Beat:
                            ret.Append("strongest to weakest");
                            break;
                        case Mood:
                            ret.Append("happiest to saddest");
                            break;
                        case Energy:
                            ret.Append("highest to lowest");
                            break;
                        default:
                            ret.Append("Z to A");
                            break;
                    }
                }

                ret.Append('.');
                return ret.ToString();
            }
        }

        public override string ToString()
        {
            return $"{Id}{(Descending ? "_desc" : string.Empty)}";
        }
    }
}
