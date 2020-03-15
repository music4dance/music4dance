using m4dModels;

namespace m4d.ViewModels
{
    public class ServiceInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Purchase { get; set; }
    }

    public class BonusInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? Cruft { get; set; }
    }


    public class UserOptions
    {
        public UserQuery Query { get; set; }
        public string UserName { get; set; }
        public bool? Include { get; set; }
        public bool? Like { get; set; }
    }

    public class SortOptions
    {
        public string Name { get; set; }
        public string Order { get; set; }
        public string Label { get; set; }
    }
}
