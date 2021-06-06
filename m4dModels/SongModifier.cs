using System.Collections.Generic;

namespace m4dModels
{
    public class SongModifier
    {
        public List<string> ExcludeUsers { get; set; }
        public List<PropertyModifier> Properties { get; set; }
    }
}