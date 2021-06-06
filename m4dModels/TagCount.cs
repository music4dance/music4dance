using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.Rest;

namespace m4dModels
{
    // TagCount is a helper class to covert between a Tag+Count structure and a string of the form Tag[:Count]
    [DataContract]
    public class TagCount
    {
        #region Properties

        [DataMember] public string Value { get; set; }
        [DataMember] public int Count { get; set; }

        public string TagValue => Value.Split(':')[0];

        public string TagClass =>
            Value.Contains(':') ? Value.Substring(Value.LastIndexOf(':') + 1) : null;

        #endregion

        #region Constructors

        public TagCount(string value, int count)
        {
            Value = value;
            Count = count;
        }

        public TagCount(string serialized, int? count = null)
        {
            if (Parse(serialized))
            {
                if (count.HasValue) Count = count.Value;
                return;
            }

            Trace.WriteLineIf(TraceLevels.General.TraceError, $"Invalid TagCount: {serialized}");
            throw new ArgumentOutOfRangeException();
        }

        #endregion

        #region Operators

        public override string ToString()
        {
            return Serialize();
        }

        public override bool Equals(object obj)
        {
            var tc = obj as TagCount;
            return tc != null && Value == tc.Value && Count == tc.Count;
        }

        public override int GetHashCode()
        {
            return (Value.GetHashCode() * 1023) ^ Count;
        }

        public static bool operator ==(TagCount a, TagCount b)
        {
            // If both are null, or both are same instance, return true.
            if (ReferenceEquals(a, b)) return true;

            // Handle a is null case.
            if ((object) a == null) return (object) b == null;

            return a.Equals(b);
        }

        public static bool operator !=(TagCount a, TagCount b)
        {
            return !(a == b);
        }

        #endregion

        private bool Parse(string s)
        {
            var ret = true;
            var list = s.Split(new[] {':'}, StringSplitOptions.RemoveEmptyEntries).ToList();
            var c = 1;

            if (list.Count < 1 || list.Count > 3) return false;

            if (list.Count > 1) ret = int.TryParse(list[^1], out c);
            Count = c;
            Value = list[0].Trim();
            if (list.Count > 2 || ret == false) Value += ":" + list[1];
            return true;
        }

        public string Serialize()
        {
            return $"{Value}:{Count}";
        }
    }
}