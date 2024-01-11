using System.Collections.Generic;
using System.Linq;

namespace DanceLibrary
{
    public class DanceFilter
    {
        public List<string> Styles { get; }
        public List<string> Organizations { get; }
        public List<string> Groups { get; }
        public Meter Meter { get; }

        public DanceFilter(
            IEnumerable<string> styles = null, IEnumerable<string> organizations = null,
            IEnumerable<string> groups = null, Meter meter = null)
        {
            Styles = styles != null ? styles.ToList() : [];
            Organizations = organizations != null ? organizations.ToList() : [];
            Groups = groups != null ? groups.ToList() : [];
            Meter = meter;
        }

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
            return Groups.Count == 0 || type.Groups.Any(g => Groups.Contains(g.Id));
        }

        internal bool MatchOrganizations(DanceType type)
        {
            return Organizations.Count == 0 || type.Organizations.Any(o => Organizations.Contains(o));
        }

        private List<DanceInstance> GetMatchingInstances(DanceType type)
        {
            return Styles.Count > 0 
                ? type.Instances.Where(inst => Styles.Contains(inst.Style)).ToList()
                : type.Instances;
        }

        private bool CoversOrganizations(DanceType type)
        {
            return Organizations.Count == 0 || type.Organizations.All(o => Organizations.Contains(o));
        }
    }
}
