
using System.Runtime.Serialization;
namespace m4dModels
{

    [DataContract]
    public class PurchaseLink
    {
        [DataMember]
        public string Link { get; set; }
        [DataMember]
        public string Target { get; set; }
        [DataMember]
        public string Logo { get; set; }
        [DataMember]
        public string Charm { get; set; }
        [DataMember]
        public string AltText { get; set; }
        [DataMember]
        public string[] AvailableMarkets { get; set; }
    }
}