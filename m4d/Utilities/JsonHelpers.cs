using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace m4d.Utilities
{
    public static class JsonHelpers
    {
        public static readonly DefaultContractResolver ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy()
        };

        public static readonly JsonSerializerSettings CamelCaseSerializer = new JsonSerializerSettings
        {
            ContractResolver = ContractResolver
        };

    }
}
