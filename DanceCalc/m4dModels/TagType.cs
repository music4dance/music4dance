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

        #endregion
    }
}
