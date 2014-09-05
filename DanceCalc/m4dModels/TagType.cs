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
        // The user visible tag
        public string Value { get; set; }

        // A | separated list of categories (generally this will be one or maybe two)
        public string Categories { get; set; }
        public int Count { get; set; }

        // Helper property to get categories in list form (this is a pseudo-property
        //  backed by the Categories physical property)
        public IList<string> CategoryList
        {
            get
            {
                List<string> list = null;
                if (string.IsNullOrWhiteSpace(Categories))
                {
                    list = new List<string>();
                }
                else
                {
                    list = Categories.Split(new char[] { '|' }).ToList();
                }
                return list;
            }
            set
            {
                Categories = string.Join("|",value);
            }
        }
        #endregion

        public void AddCategory(string categories)
        {
            if (string.IsNullOrWhiteSpace(categories))
            {
                return;
            }

            string[] list = categories.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string item in list)
            {
                if (!CategoryList.Contains(item))
                {
                    if (string.IsNullOrWhiteSpace(Categories))
                    {
                        Categories = item;
                    }
                    else
                    {
                        Categories = Categories + "|" + item;
                    }
                }
            }
        }
    }
}
