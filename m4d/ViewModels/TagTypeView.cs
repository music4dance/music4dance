using m4dModels;

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