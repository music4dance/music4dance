using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels
{
    public class TagType : DbObject
    {
        #region Properties
        public string Key { get; set; }
        // The user visible tag
        public string Value 
        {
            get
            {
                return Key.Substring(0, Key.IndexOf(':'));
            }
        }

        // A single tag category/namespace
        public string Category
        { 
            get
            {
                return Key.Substring(Key.IndexOf(':')+1);
            }
        }

        // The total number of refernces to this tag
        public int Count { get; set; }

        // For tag rings, point to the 'primary' variation of the tag
        public string PrimaryId { get; set; }
        public virtual TagType Primary {get; set;}
        public virtual ICollection<TagType> Ring {get; set;}
        #endregion

        #region Constructors
        public TagType() { }

        public TagType(string tag)
        {
            if (!tag.Contains(':'))
            {
                Key = tag + ":Other";
            }
            else
            {
                Key = tag;
            }
        }
        #endregion

        #region Operations

        public override string ToString()
        {
            return Key;
        }

        public static string BuildKey(string value, string category)
        {
            return string.Format("{0}:{1}", value, category);
        }

        public static string TagEncode(string tag)
        {
            StringBuilder sb = new StringBuilder();

            foreach (char c in tag)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                }
                else if (c == '-')
                {
                    sb.Append("--");
                }
                else if (c== ':')
                {
                    sb.Append("-p"); //seParator
                }
                else if (c=='&')
                {
                    sb.Append("-m"); //aMpersand
                }
                else if (c=='/')
                {
                    sb.Append("-s"); //Slash
                }
                else if (c==' ')
                {
                    sb.Append("-w"); //Slash
                }
                else
                {
                    int i = c;
                    if (i > 256) 
                    {
                        throw new ArgumentOutOfRangeException(string.Format("Invalid tag character: {0}",c));
                    }
                    else
                    {
                        sb.AppendFormat("-{0:x2}",i);
                    }
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
            else
            {
                c = char.ToLower(c);
                return c >= 'a' && c <= 'f';
            }
            
        }

        static private int ConvertHexDigit(char c)
        {
            int ret = 0;
            if (char.IsDigit(c))
            {
                ret = c - '0';
            }
            else
            {
                ret = (char.ToLower(c) - 'a') + 10;
            }
            return ret;
        }

        public static string TagDecode(string tag)
        {
            StringBuilder sb = new StringBuilder();

            int cch = tag.Length;
            for (int ich = 0; ich < cch; ich++)
            {
                char c = tag[ich];
                if (c == '-')
                {
                    ich += 1;
                    if (ich == cch)
                    {
                        throw new ArgumentOutOfRangeException(string.Format("Invalid tags: ends with escape: '{0}'", tag));
                    }

                    char c1 = tag[ich];
                    if (c1 == '-')
                    {
                        sb.Append('-');
                    }
                    else if (c1 == 'p')
                    {
                        sb.Append(':'); //seParator
                    }
                    else if (c1 == 'm')
                    {
                        sb.Append('&'); //aMpersand
                    }
                    else if (c1 == 's')
                    {
                        sb.Append('/'); //Slash
                    }
                    else if (c1 == 'w')
                    {
                        sb.Append(' '); //White
                    }
                    else if (IsHexDigit(c1))
                    {
                        int i = ConvertHexDigit(c1) * 16;
                        ich += 1;
                        if (ich == cch)
                        {
                            throw new ArgumentOutOfRangeException(string.Format("Invalid tags: ends with escape + single digit: '{0}'", tag));
                        }

                        char c2 = tag[ich];
                        if (!IsHexDigit(c2))
                        {
                            throw new ArgumentOutOfRangeException(string.Format("Invalid tags: invalid escape at {0}: '{1}'", ich, tag));
                        }

                        i += ConvertHexDigit(c2);

                        sb.Append((char)i);
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException(string.Format("Invalid tags: invalid escape at {0}: '{1}'", ich, tag));
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        #endregion
    }
}
