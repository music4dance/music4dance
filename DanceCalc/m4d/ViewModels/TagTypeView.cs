using m4dModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace m4d.ViewModels
{
    public class TagTypeView : TagType
    {
        public TagTypeView()
        {
        }
        public TagTypeView(TagType tt) : base(tt)
        {
            NewKey = tt.Key;
        }
        public string NewKey { get; set; }
    }
}