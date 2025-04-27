using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace m4dModels
{
    [DataContract]
    public class JTag
    {
        [DataMember]
        public string Id { get; set; }

        [DataMember]
        public string Tags { get; set; }
    }

    [DataContract]
    public class JTags
    {
        [DataMember]
        public IEnumerable<JTag> Tags { get; set; }

        public IList<UserTag> ToUserTags()
        {
            return [.. Tags.Select(
                jtag => new UserTag
                { Id = jtag.Id ?? string.Empty, Tags = new TagList(jtag.Tags) })];
        }

        public static JTags FromJson(string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            var ser = new DataContractJsonSerializer(typeof(JTags));
            var stream = new MemoryStream(bytes);

            var jt = (JTags)ser.ReadObject(stream);
            return jt;
        }
    };

    [DataContract]
    public class UserTag
    {
        // This is a danceId or null for the song tags
        [DataMember]
        public string Id { get; set; }

        [DataMember]
        public TagList Tags { get; set; }
    }
}
