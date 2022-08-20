using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace m4dModels.Utilities
{
    public static class JsonHelpers
    {
        public static readonly DefaultContractResolver ContractResolver = new()
        {
            NamingStrategy = new CamelCaseNamingStrategy()
        };

        public static readonly JsonSerializerSettings CamelCaseSerializer = new()
        {
            ContractResolver = ContractResolver,
            NullValueHandling = NullValueHandling.Ignore
        };
    }
}
