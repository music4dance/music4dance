//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace m4dModels.Tests
//{
//    public class MockTagLibrary :  ITagLibrary
//    {
//        static public MockTagLibrary Instance
//        {
//            get { return _instance; }
//        }

//        public TagType FindOrCreateType(string value, string category)
//        {
//            TagType type = _tagTypes.FirstOrDefault(t => string.Equals(t.Value, value, StringComparison.OrdinalIgnoreCase));
//            if (type == null)
//            {
//                type = new TagType() { Category = category, Value = value };
//            }
//            return type;
//        }

//        public IEnumerable<TagType> GetTypes(string category)
//        {
//            return _tagTypes.Where(t => t.Value == category);
//        }

//        private static List<TagType> _tagTypes = new List<TagType>() 
//        {
//            new TagType() {Value="Rock",Category="Genre"},
//            new TagType() {Value="Blues",Category="Genre"},
//            new TagType() {Value="Pop",Category="Genre"},
//            new TagType() {Value="Swing",Category="Dance"},
//            new TagType() {Value="Foxtrot",Category="Dance"},
//            new TagType() {Value="Waltz",Category="Dance"},
//            new TagType() {Value="Rumba",Category="Dance"},
//        };

//        private static MockTagLibrary _instance = new MockTagLibrary();
//    }
//}
