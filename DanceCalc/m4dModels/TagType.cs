using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace m4dModels
{
    public class TagType
    {
        #region Properties
        public string Key { get; set; }
        // The user visible tag
        public string Value => Key.Substring(0, Key.IndexOf(':'));

        // A single tag category/namespace
        public string Category => Key.Substring(Key.IndexOf(':')+1);

        // The total number of refernces to this tag
        public int Count { get; set; }

        // For tag rings, point to the 'primary' variation of the tag
        public string PrimaryId { get; set; }
        public virtual TagType Primary {get; set;}
        public virtual ICollection<TagType> Ring {get; set;}

        public string EncodedKey => TagEncode(Key);

        public bool IsNull => string.IsNullOrWhiteSpace(Key);

        #endregion

        #region Constructors
        public TagType() { }

        public TagType(string tag)
        {
            Key = !tag.Contains(':') ? tag + ":Other" : tag;
        }

        public TagType(TagType tt)
        {
            Copy(tt);
        }

        public void Copy(TagType tt)
        {
            Key = tt.Key;
            PrimaryId = tt.PrimaryId;
            Primary = tt.Primary;
            Count = tt.Count;
            Ring = tt.Ring != null ? new List<TagType>(tt.Ring) : new List<TagType>();
        }

        // TODO: Think TagType should be derived from tagcount?
        public static implicit operator TagCount(TagType tt)
        {
            return new TagCount(tt.Key,tt.Count);
        }

        public static IEnumerable<TagCount> ToTagCounts(IEnumerable<TagType> ttl)
        {
            var d = new Dictionary<string, TagCount>();
            foreach (var tt in ttl)
            {
                var p = tt.GetPrimary();
                TagCount tc;
                if (!d.TryGetValue(p.Key, out tc))
                {
                    tc = new TagCount(p.Key,0);
                    d[p.Key] = tc;
                }
                tc.Count += tt.Count;
            }

            return d.Values.OrderBy(tc => tc.Value);
        }

        #endregion

        #region Operations

        public override string ToString()
        {
            return Key;
        }

        public TagType GetPrimary()
        {
            var p = this;
            while (p.Primary != null)
            {
                p = p.Primary;
            }
            return p;
        }
        public static string BuildKey(string value, string category)
        {
            return $"{value}:{category}";
        }

        public static string TagEncode(string tag)
        {
            var sb = new StringBuilder();

            foreach (var c in tag)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                }
                else switch (c)
                {
                    case '-':
                        sb.Append("--");
                        break;
                    case ':':
                        sb.Append("-p"); //seParator
                        break;
                    case '&':
                        sb.Append("-m"); //aMpersand
                        break;
                    case '/':
                        sb.Append("-s"); //Slash
                        break;
                    case ' ':
                        sb.Append("-w"); //Slash
                        break;
                    default:
                        int i = c;
                        if (i > 256) 
                        {
                            throw new ArgumentOutOfRangeException($"Invalid tag character: {c}");
                        }
                        else
                        {
                            sb.AppendFormat("-{0:x2}",i);
                        }
                        break;
                }
            }

            return sb.ToString();
        }

        static private bool IsHexDigit(char c)
        {
            if (char.IsDigit(c))
            {
                return true;
            }
            c = char.ToLower(c);
            return c >= 'a' && c <= 'f';
        }

        static private int ConvertHexDigit(char c)
        {
            return char.IsDigit(c) ? c - '0' : (char.ToLower(c) - 'a') + 10;
        }

        public static string TagDecode(string tag)
        {
            var sb = new StringBuilder();

            var cch = tag.Length;
            for (var ich = 0; ich < cch; ich++)
            {
                var c = tag[ich];
                if (c == '-')
                {
                    ich += 1;
                    if (ich == cch)
                    {
                        throw new ArgumentOutOfRangeException($"Invalid tags: ends with escape: '{tag}'");
                    }

                    var c1 = tag[ich];
                    switch (c1)
                    {
                        case '-':
                            sb.Append('-');
                            break;
                        case 'p':
                            sb.Append(':'); //seParator
                            break;
                        case 'm':
                            sb.Append('&'); //aMpersand
                            break;
                        case 's':
                            sb.Append('/'); //Slash
                            break;
                        case 'w':
                            sb.Append(' '); //White
                            break;
                        default:
                            if (IsHexDigit(c1))
                            {
                                var i = ConvertHexDigit(c1) * 16;
                                ich += 1;
                                if (ich == cch)
                                {
                                    throw new ArgumentOutOfRangeException(
                                        $"Invalid tags: ends with escape + single digit: '{tag}'");
                                }

                                var c2 = tag[ich];
                                if (!IsHexDigit(c2))
                                {
                                    throw new ArgumentOutOfRangeException(
                                        $"Invalid tags: invalid escape at {ich}: '{tag}'");
                                }

                                i += ConvertHexDigit(c2);

                                sb.Append((char)i);
                            }
                            else
                            {
                                throw new ArgumentOutOfRangeException($"Invalid tags: invalid escape at {ich}: '{tag}'");
                            }
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        public static string ClassToName(string cls)
        {
            var name = "tag";
            switch (cls.ToLower())
            {
                case "style":
                    name = "dance";
                    break;
                case "tempo":
                    name = "tempo";
                    break;
                case "music":
                    name = "genre";
                    break;
            }
            return name;
        }
        #endregion
    }
}
