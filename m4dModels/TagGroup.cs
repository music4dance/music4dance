﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace m4dModels
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TagGroup
    {
        #region Properties

        [JsonProperty]
        public string Key { get; set; }

        // The user visible tag
        public string Value => Key[..Key.IndexOf(':')];

        [JsonProperty]
        public DateTime Modified { get; set; }

        // A single tag category/namespace
        public string Category => Key[(Key.IndexOf(':') + 1)..];

        // The total number of references to this tag
        [JsonProperty]
        public int Count { get; set; }

        // For tag rings, point to the 'primary' variation of the tag
        [JsonProperty]
        public string PrimaryId { get; set; }

        public virtual TagGroup Primary { get; set; }
        public virtual IList<TagGroup> Children { get; set; }

        public string EncodedKey => TagEncode(Key);

        public bool IsNull => string.IsNullOrWhiteSpace(Key);

        public bool IsConected => Primary != null 
            || Children != null && Children.Count > 0;
        #endregion

        #region Constructors

        public TagGroup()
        {
        }

        public TagGroup(string tag)
        {
            Key = !tag.Contains(':') ? tag + ":Other" : tag;
        }

        public TagGroup(TagGroup tt)
        {
            Copy(tt);
        }

        public void Copy(TagGroup tt)
        {
            Key = tt.Key;
            PrimaryId = tt.PrimaryId;
            Primary = tt.Primary;
            Count = tt.Count;
            Children = tt.Children != null ? new List<TagGroup>(tt.Children) : new List<TagGroup>();
        }

        // TODO: Think TagGroup should be derived from tagcount?
        public static implicit operator TagCount(TagGroup tt)
        {
            return new TagCount(tt.Key, tt.Count);
        }

        public static IEnumerable<TagCount> ToTagCounts(IEnumerable<TagGroup> ttl)
        {
            var d = new Dictionary<string, TagCount>();
            foreach (var tt in ttl)
            {
                var p = tt.GetPrimary();
                if (!d.TryGetValue(p.Key, out var tc))
                {
                    tc = new TagCount(p.Key, 0);
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

        public TagGroup GetPrimary()
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
                else
                {
                    switch (c)
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
                            sb.Append("-w"); //whitespace
                            break;
                        default:
                            int i = c;
                            if (i > 256)
                            {
                                throw new ArgumentOutOfRangeException(
                                    $"Invalid tag character: {c}");
                            }
                            else
                            {
                                sb.AppendFormat("-{0:x2}", i);
                            }

                            break;
                    }
                }
            }

            return sb.ToString();
        }

        private static bool IsHexDigit(char c)
        {
            if (char.IsDigit(c))
            {
                return true;
            }

            c = char.ToLower(c);
            return c >= 'a' && c <= 'f';
        }

        private static int ConvertHexDigit(char c)
        {
            return char.IsDigit(c) ? c - '0' : char.ToLower(c) - 'a' + 10;
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
                        throw new ArgumentOutOfRangeException(
                            $"Invalid tags: ends with escape: '{tag}'");
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
                                throw new ArgumentOutOfRangeException(
                                    $"Invalid tags: invalid escape at {ich}: '{tag}'");
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

        public void AddChild(TagGroup tagGroup)
        {
            if (Children == null)
            {
                Children = new List<TagGroup>();
            }

            Children.Add(tagGroup);
        }
    }
}
