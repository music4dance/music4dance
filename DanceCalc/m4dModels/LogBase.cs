using System;
using System.Collections.Generic;

namespace m4dModels
{
    public class LogBase
    {
        protected const char RecordSeparator = '\x1E';
        protected const char UnitSeparator = '\x1F';
        public string Data { get; set; }

        public void UpdateData(string name, string value, string oldValue = null)
        {
            if (string.IsNullOrWhiteSpace(Data))
            {
                Data = string.Empty;
            }
            else
            {
                Data += RecordSeparator;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                value = string.Empty;
            }

            Data += string.Format("{0}{1}{2}", name, UnitSeparator, value);
            if (oldValue != null)
            {
                Data += string.Format("{0}{1}", UnitSeparator,oldValue);
            }
        }

        public int? GetIntData(string name)
        {
            int? ret = null;

            string s = GetData(name);

            if (s != null)
            {
                int r;
                if (int.TryParse(s, out r))
                {
                    ret = r;
                }
            }

            return ret;
        }

        public string GetData(string name)
        {
            LogValue lv = FindCell(name);

            if (lv != null)
            {
                return lv.Value;
            }
            else
            {
                return null;
            }
        }
        public string GetOld(string name)
        {
            LogValue lv = FindCell(name);

            if (lv != null)
            {
                return lv.Old;
            }
            else
            {
                return null;
            }
        }

        public LogValue FindCell(string name)
        {
            IList<LogValue> values = GetValues();

            if (values != null)
            {
                foreach (LogValue lv in values)
                {

                    if (lv.Name.Equals(name))
                    {
                        return lv;
                    }
                }
            }

            return null;
        }

        public IList<LogValue> GetValues()
        {
            List<LogValue> values = null;
            if (!string.IsNullOrWhiteSpace(Data))
            {
                values = new List<LogValue>();

                string[] entries = Data.Split(new char[] { RecordSeparator }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string entry in entries)
                {
                    string name = null;
                    string value = null;
                    string old = null;

                    string[] cells = entry.Split(new char[] { UnitSeparator });

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
            }

            return values;
        }
    }
}