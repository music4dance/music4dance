using System.Diagnostics;
using DanceLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DanceTests
{
    [TestClass]
    public class Converter
    {
        [TestMethod]
        [Ignore]
        public void ConvertToBpm()
        {
            var dances = Dances.Instance;

            foreach (var dance in dances.AllDanceTypes)
            {
                var numerator = dance.Meter.Numerator;
                dance.TempoRange = ConvertRange(dance.TempoRange, numerator);
                foreach (var instance in dance.Instances)
                {
                    instance.TempoRange = ConvertRange(instance.TempoRange, numerator);
                    foreach (var exception in instance.Exceptions)
                    {
                        exception.TempoRange = ConvertRange(exception.TempoRange, numerator);
                    }
                }
            }

            var json = JsonConvert.SerializeObject(dances.AllDanceTypes, CamelCaseSerializer);
            Trace.WriteLine(json);
        }

        private static TempoRange ConvertRange(TempoRange range, int numerator)
        {
            return new TempoRange(range.Min * numerator, range.Max * numerator);
        }

        private static readonly JsonSerializerSettings CamelCaseSerializer = new()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            },
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };
    }
}
