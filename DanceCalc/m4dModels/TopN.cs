using System;
using System.Runtime.Serialization;

namespace m4dModels
{
    [DataContract]
    public class TopN
    {
        [DataMember]
        public Guid SongId { get; set; }
        public virtual Song Song { get; set; }
        [DataMember]
        public string DanceId { get; set; }
        public virtual Dance Dance { get; set; }
        [DataMember]
        public int Rank { get; set; }
    }
}
