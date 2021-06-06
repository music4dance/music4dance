using System.Reflection;
using m4dModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace m4d.Utilities
{
    public class StatsContractResolver : DefaultContractResolver
    {
        public StatsContractResolver(bool camelCase, bool hideSongs)
        {
            if (camelCase) NamingStrategy = new CamelCaseNamingStrategy();

            _hideSongs = hideSongs;
        }

        protected override JsonProperty CreateProperty(MemberInfo member,
            MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            if (property.DeclaringType == typeof(DanceStats) && property.PropertyName == "topSongs")
                property.ShouldSerialize = instance => !_hideSongs;

            return property;
        }

        private readonly bool _hideSongs;
    }
}