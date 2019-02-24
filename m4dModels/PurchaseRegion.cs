using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace m4dModels
{
    public class PurchaseRegion
    {
        public PurchaseRegion(IEnumerable<string> regions)
        {
            _regions = new List<string>(regions);
        }

        public PurchaseRegion(string regions)
        {
            _regions = new List<string>(ParseRegionInfo(regions));
        }

        public string[] Regions => _regions?.ToArray();

        public override string ToString()
        {
            return _regions == null ? string.Empty : FormatRegionInfo(_regions);
        }

        private readonly List<string> _regions;

        public static string ParseIdAndRegionInfo(string value, out string[] regions)
        {
            regions = null;

            if (value == null || !value.EndsWith("]")) return value;

            var fields = value.Split('[');

            if (fields.Length == 2)
            {
                regions = ParseRegionInfo(fields[1].Substring(0, fields[1].Length - 1));
            }

            return fields[0];
        }

        public static string ParseId(string value)
        {
            if (value == null || !value.EndsWith("]")) return value;

            var fields = value.Split('[');

            return fields[0];
        }

        public static string FixRegionInfo(string value)
        {
            return value.Contains('[')?value.Substring(0, value.LastIndexOf('[')) : null;
        }

        private static string[] ParseRegionInfo(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (char.IsLetter(value[0]) || value[0] == ',')
                return value.Split(',');

            if (value.StartsWith("0-"))
            {
                return ExpandBaseDelta(value.Substring(2));
            }
            int idx;
            if (int.TryParse(value, out idx))
            {
                return s_commonRegions[idx].Split(',');
            }

            Debug.Assert(false);
            return null;
        }

        public static string FormatIdAndRegionInfo(string id, string[] regions)
        {
            if (regions == null) return id;

            if (id != null && id.EndsWith("]"))
            {
                id = id.Substring(0, id.LastIndexOf('['));
            }

            return id + FormatRegionInfo(regions);
        }

        public static string FormatRegionInfo(IEnumerable<string> regions)
        {
            var bare = string.Join(",", regions.Where(r => !string.IsNullOrWhiteSpace(r)));

            int idx;
            var mapped = s_commonRegionsMap.TryGetValue(bare, out idx);

            if (mapped)
            {
                bare = idx.ToString();
            }
            else
            {
                var delta = ComputeBaseDelta(bare);
                if (delta.Length < bare.Length)
                {
                    bare = delta;
                }
            }

            return $"[{bare}]";
        }

        public static string[] MergeRegions(string[] a, string[] b)
        {
            if (b == null) return a;
            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (a == null) return b;

            return new List<string>(a).Concat(b).Distinct().OrderBy(x => x).ToArray();
        }

        private static readonly string[] s_commonRegions =
        {
            "AD,AE,AR,AT,AU,BE,BG,BH,BO,BR,CA,CH,CL,CO,CR,CY,CZ,DE,DK,DO,DZ,EC,EE,EG,ES,FI,FR,GB,GR,GT,HK,HN,HU,ID,IE,IL,IS,IT,JO,JP,KW,LB,LI,LT,LU,LV,MA,MC,MT,MX,MY,NI,NL,NO,NZ,OM,PA,PE,PH,PL,PS,PT,PY,QA,RO,SA,SE,SG,SK,SV,TH,TN,TR,TW,US,UY,VN,ZA",
            "CA,MX,US",
            "CA,GB,IE,US",
            "AT,CA,CH,DE,MX,US",
            "CA,GB,IE,MX,US",
            "AD,AR,AT,AU,BE,BG,BO,BR,CH,CL,CO,CR,CY,CZ,DE,DK,DO,EC,EE,ES,FI,FR,GB,GR,GT,HK,HN,HU,IE,IS,IT,LI,LT,LU,LV,MC,MT,MX,MY,NI,NL,NO,NZ,PA,PE,PH,PL,PT,PY,RO,SE,SG,SI,SK,SV,TR,TW,US,UY",
            "AD,AR,AT,BE,BG,BO,BR,CA,CH,CL,CO,CR,CY,CZ,DE,DK,DO,EC,EE,ES,FI,FR,GB,GR,GT,HK,HN,HU,IE,IS,IT,LI,LT,LU,LV,MC,MT,MX,MY,NI,NL,NO,PA,PE,PH,PL,PT,PY,RO,SE,SG,SI,SK,SV,TR,TW,US,UY",
            "AD,AR,AT,AU,BE,BG,BO,BR,CA,CH,CL,CO,CR,CY,CZ,DE,DK,DO,EC,EE,ES,FI,FR,GB,GR,GT,HK,HN,HU,IE,IS,IT,LI,LT,LU,LV,MC,MT,MX,MY,NI,NL,NO,NZ,PA,PE,PH,PL,PT,PY,RO,SE,SG,SI,SK,SV,TR,TW,UY",
            "AD,AR,AT,AU,BE,BG,BO,BR,CA,CH,CL,CO,CR,CY,CZ,DE,DK,DO,EC,EE,ES,FI,FR,GB,GR,GT,HK,HN,HU,IE,IS,IT,LT,LU,LV,MC,MT,MX,MY,NI,NL,NO,NZ,PA,PE,PH,PL,PT,PY,RO,SE,SG,SI,SK,SV,TR,TW,US,UY",
            "AD,AR,AT,AU,BE,BG,BO,BR,CA,CH,CL,CO,CR,CY,CZ,DE,DK,DO,EC,EE,ES,FI,FR,GB,GR,GT,HK,HN,HU,IE,IS,IT,LI,LT,LU,LV,MC,MT,MY,NI,NL,NO,NZ,PA,PE,PH,PL,PT,PY,RO,SE,SG,SI,SK,SV,TR,TW,US,UY",
            "AD,AR,AT,AU,BE,BG,BO,BR,CA,CH,CL,CO,CR,CY,CZ,DE,DK,DO,EC,EE,ES,FI,FR,GR,GT,HK,HN,HU,IE,IS,IT,LI,LT,LU,LV,MC,MT,MX,MY,NI,NL,NO,NZ,PA,PE,PH,PL,PT,PY,RO,SE,SG,SI,SK,SV,TR,TW,US,UY",
            "AD,AR,AT,AU,BE,BG,BO,BR,CA,CH,CL,CO,CR,CY,CZ,DE,DK,DO,EC,EE,ES,FI,FR,GB,GR,GT,HK,HN,HU,IE,IS,IT,LI,LT,LU,LV,MC,MT,MX,MY,NI,NL,NO,NZ,PA,PE,PH,PL,PT,PY,RO,SE,SG,SI,SK,SV,TW,US,UY",
            "AD,AR,AT,BE,BG,BO,BR,CA,CH,CL,CO,CR,CY,CZ,DE,DK,DO,EC,EE,ES,FI,FR,GB,GR,GT,HK,HN,HU,IE,IS,IT,LI,LT,LU,LV,MC,MT,MX,MY,NI,NL,NO,NZ,PA,PE,PH,PL,PT,PY,RO,SE,SG,SI,SK,SV,TR,TW,US,UY",
            "AR,AU,BO,BR,CA,CL,CO,CR,DO,EC,GT,HK,HN,MX,MY,NI,NZ,PA,PE,PH,PY,SG,SV,TW,US,UY",
            "AD,AR,AT,AU,BE,BG,BO,BR,CA,CH,CL,CO,CR,CY,CZ,DE,DK,DO,EC,EE,ES,FI,FR,GR,GT,HK,HN,HU,IS,IT,LI,LT,LU,LV,MC,MT,MX,MY,NI,NL,NO,NZ,PA,PE,PH,PL,PT,PY,RO,SE,SG,SI,SK,SV,TR,TW,US,UY",
            "AD,AR,AT,AU,BE,BG,BO,BR,CA,CH,CL,CO,CR,CY,CZ,DK,DO,EC,EE,ES,FI,FR,GB,GR,GT,HK,HN,HU,IE,IS,IT,LI,LT,LU,LV,MC,MT,MX,MY,NI,NL,NO,NZ,PA,PE,PH,PL,PT,PY,RO,SE,SG,SI,SK,SV,TR,TW,US,UY",
            "AD,AR,AT,AU,BE,BG,BO,BR,CA,CH,CL,CO,CR,CY,CZ,DE,DK,DO,EC,EE,ES,FI,FR,GB,GR,GT,HK,HN,HU,IE,IS,LI,LT,LU,LV,MC,MX,MY,NI,NL,NO,NZ,PA,PE,PH,PL,PT,PY,RO,SE,SG,SI,SK,SV,TR,TW,US,UY",
            "AD,AR,AU,BE,BG,BO,BR,CA,CH,CL,CO,CR,CY,CZ,DK,DO,EC,EE,ES,FI,FR,GB,GR,GT,HK,HN,HU,IE,IS,IT,LI,LT,LU,LV,MC,MT,MX,MY,NI,NL,NO,NZ,PA,PE,PH,PL,PT,PY,RO,SE,SG,SI,SK,SV,TR,TW,US,UY",
            "AR,BO,BR,CA,CL,CO,CR,DO,EC,GT,HK,HN,MX,MY,NI,PA,PE,PH,PY,SG,SV,TW,US,UY",
            "AD,AR,AT,AU,BE,BG,BO,BR,CA,CH,CL,CO,CR,CY,CZ,DE,DK,DO,EC,EE,ES,FI,FR,GB,GR,GT,HN,HU,IE,IS,IT,LI,LT,LU,LV,MC,MT,MX,NI,NL,NO,NZ,PA,PE,PL,PT,PY,RO,SE,SI,SK,SV,TR,US,UY",
            "AD,AR,AT,BE,BG,BO,BR,CH,CL,CO,CR,CY,CZ,DE,DK,DO,EC,EE,ES,FI,FR,GB,GR,GT,HK,HN,HU,IE,IS,IT,LI,LT,LU,LV,MC,MT,MX,MY,NI,NL,NO,PA,PE,PH,PL,PT,PY,RO,SE,SG,SI,SK,SV,TR,TW,US,UY",
        };

        // Common Region
        private static readonly Dictionary<string,int> s_commonRegionsMap;

        private static readonly string[] s_baseRegions;

        private static string ComputeBaseDelta(string value)
        {
            // Assume valid, non-null regions list coming in
            var regions = value.Split(',');

            var rem = s_baseRegions.Except(regions).ToList();
            var add = regions.Except(s_baseRegions).ToList();

            // TOOD: If we start getting more than our core list of contries in our regions list, revisit this
            if (add.Count > 0 || rem.Count == 0)
                return value;

            rem.Sort();
            return "0-" + string.Join(",",rem);
        }

        private static string[] ExpandBaseDelta(string value)
        {
            return s_baseRegions.Except(value.Split(',')).ToArray();
        }

        static PurchaseRegion()
        {
            s_commonRegionsMap = new Dictionary<string, int>();
            for (var i = 0; i < s_commonRegions.Length; i++)
            {
                s_commonRegionsMap.Add(s_commonRegions[i], i);
            }

            s_baseRegions = s_commonRegions[0].Split(',');
            //    new HashSet<string>();
            //foreach (var r in s_commonRegions[0].Split(','))
            //{
            //    s_baseRegions.Add(r);
            //}
        }
    }
}
