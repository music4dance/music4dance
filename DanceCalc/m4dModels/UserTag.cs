using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels
{
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
