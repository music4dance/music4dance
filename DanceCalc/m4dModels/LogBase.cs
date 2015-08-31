using System.Collections.Generic;
using System.Linq;

namespace m4dModels
{
    public class LogBase
    {
        public const char RecordSeparator = '\u001E';
        public const char UnitSeparator = '\u001F';

        public const string RecordString = "\u001E";
        public const string UnitString = "\u001F";

        public string Data { get; set; }

        public void UpdateData(string name, string value, string oldValue = null)
        {
            var rec = MakeRecord(name, value, oldValue);
            if (string.IsNullOrWhiteSpace(Data))
            {
                Data = rec;
            }
            Data = string.Join(RecordString, Data, rec);
        }

        public int? GetIntData(string name)
        {
            var s = GetData(name);

            if (s == null) return null;

            int r;
            if (int.TryParse(s, out r))
            {
                return r;
            }

            return null;
        }

        public string GetData(string name)
        {
            var lv = FindCell(name);

            return lv?.Value;
        }

        public string GetOld(string name)
        {
            var lv = FindCell(name);

            return lv?.Old;
        }

        public LogValue FindCell(string name)
        {
            var values = GetValues();

            return values?.FirstOrDefault(lv => lv.Name.Equals(name));
        }

        public IList<LogValue> GetValues()
        {
            if (string.IsNullOrWhiteSpace(Data)) return null;

            var values = new List<LogValue>();

            var entries = Data.Split(RecordSeparator);
            foreach (var entry in entries)
            {
                string name = null;
                string value = null;
                string old = null;

                var cells = entry.Split(UnitSeparator);

                if (cells.Length > 0)
                {
                    name = cells[0];

                    if (cells.Length > 1)
                    {
                        value = cells[1];
                        if (cells.Length > 2)
                        {
                            old = cells[2];
                        }
                    }
                }

                values.Add(new LogValue { Name = name, Value = value, Old = old });
            }

            return values;
        }

        public static string MakeRecord(string name, string value, string old = null)
        {
            var ret = (old == null) ? string.Join(UnitString, name, value) : string.Join(UnitString, name, value, old);

            return ret;
        }
    }
}