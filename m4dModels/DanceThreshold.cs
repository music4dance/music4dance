using DanceLibrary;
using System;
using System.Text.RegularExpressions;

namespace m4dModels
{
    public class DanceThreshold
    {
        public string Id { get; set; }
        public int Threshold { get; set; }

        public static DanceThreshold FromValue(string value)
        {
            var regex = new Regex(@"^([a-zA-Z0-9]+)([+-]?)(\d*)$");
            var match = regex.Match(value);
            if (!match.Success)
            {
                throw new Exception($"Invalid value format: {value}");
            }

            var dance = Dances.Instance.DanceFromId(match.Groups[1].Value);
            if (dance == null)
            {
                throw new Exception($"Couldn't find dance {match.Groups[1].Value}");
            }

            var weight = match.Groups[3].Success && !string.IsNullOrEmpty(match.Groups[3].Value) 
                ? int.Parse(match.Groups[3].Value) : 1;

            return new DanceThreshold
            {
                Id = dance.Id,
                Threshold = match.Groups[2].Success && match.Groups[2].Value == "-" ? -weight : weight
            };
        }

        public DanceObject Dance => Dances.Instance.DanceFromId(Id);

        public override string ToString()
        {
            return Description;
        }

        public string Description
        {
            get
            {
                return Threshold == 1 
                    ? Dance.Name
                    : $"{Dance.Name} (with {(Threshold > 0 ? "at least" : "at most")} {Math.Abs(Threshold)} votes)";
            }
        }

        public string ShortDescription
        {
            get
            {
                return Threshold == 1
                    ? Dance.Name
                    : $"{Dance.Name} {(Threshold > 0 ? ">=" : "<=")} {Math.Abs(Threshold)}";
            }
        }
    }
}
