using System.Collections.Generic;
using System.Linq;

namespace DanceLibrary
{
    public class DanceFilter(
        IEnumerable<string> styles = null, IEnumerable<string> organizations = null,
        IEnumerable<string> groups = null, Meter meter = null)
    {
        public List<string> Styles { get; } = styles != null ? [.. styles] : [];
        public List<string> Organizations { get; } = organizations != null ? [.. organizations] : [];
        public List<string> Groups { get; } = groups != null ? [.. groups] : [];
        public Meter Meter { get; } = meter;

        public DanceType Reduce(DanceType type)
        {
            if (!MatchMeter(type) || !MatchGroups(type) || !MatchOrganizations(type))
            {
                return null;
            }

            // Get the instances corresponding with the styles, return null if there are none
            var coversOrgs = CoversOrganizations(type);
            var instances = GetMatchingInstances(type)
                .Select(inst => inst.ReduceExceptions(coversOrgs ? null : Organizations))
                .ToList();
            return instances.Count > 0 ? type.Reduce(instances) : null;
        }

        public IEnumerable<DanceType> Filter(IEnumerable<DanceType> types)
        {
            return types.Select(Reduce).Where(type => type != null);
        }

        internal bool MatchMeter(DanceType type)
        {
            return Meter == null || Meter == type.Meter;
        }

        // TODO: These two function could share a common implementation
        internal bool MatchGroups(DanceType type)
        {
            return Groups.Count == 0 || type.Groups.Any(g => Groups.Contains(g.Name));
        }

        internal bool MatchOrganizations(DanceType type)
        {
            return Organizations.Count == 0 || type.Organizations.Any(o => Organizations.Contains(o));
        }

        private List<DanceInstance> GetMatchingInstances(DanceType type)
        {
            return Styles.Count > 0
                ? [.. type.Instances.Where(inst => Styles.Contains(inst.Style))]
                : type.Instances;
        }

        private bool CoversOrganizations(DanceType type)
        {
            return Organizations.Count == 0 || type.Organizations.All(o => Organizations.Contains(o));
        }
    }
}
