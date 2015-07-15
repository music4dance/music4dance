using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public string[] Regions
        {
            get { return _regions == null ? null : _regions.ToArray(); }
        }

        public override string ToString()
        {
            if (_regions == null) return string.Empty;

            return FormatRegionInfo(_regions);
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

        private static string[] ParseRegionInfo(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (Char.IsLetter(value[0]) || value[0] == ',')
                return value.Split(',');

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
            var sb = new StringBuilder();
            var sep = String.Empty;
            foreach (var r in regions)
            {
                if (string.IsNullOrWhiteSpace(r)) continue;
                sb.Append(sep);
                sb.Append(r);
                sep = ",";
            }

            var bare = sb.ToString();
            int idx;
            if (s_crMap.TryGetValue(bare, out idx))
            {
                bare = idx.ToString();
            }

            return string.Format("[{0}]", bare);
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
            "AD,AR,AT,AU,BE,BG,BO,BR,CA,CH,CL,CO,CR,CY,CZ,DE,DK,DO,EC,EE,ES,FI,FR,GB,GR,GT,HK,HN,HU,IE,IS,IT,LI,LT,LU,LV,MC,MT,MX,MY,NI,NL,NO,NZ,PA,PE,PH,PL,PT,PY,RO,SE,SG,SI,SK,SV,TR,TW,US,UY",
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
        };

        private static readonly Dictionary<string,int> s_crMap;

        static PurchaseRegion()
        {
            s_crMap = new Dictionary<string, int>();
            for (var i = 0; i < s_commonRegions.Length; i++)
            {
                s_crMap.Add(s_commonRegions[i], i);
            }
        }
    }
}
