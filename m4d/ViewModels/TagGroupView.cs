using m4dModels;

namespace m4d.ViewModels
{
    public class TagGroupView : TagGroup
    {
        public TagGroupView()
        {
        }

        public TagGroupView(TagGroup tt) : base(tt)
        {
            NewKey = tt.Key;
        }

        public string NewKey { get; set; }
    }
}