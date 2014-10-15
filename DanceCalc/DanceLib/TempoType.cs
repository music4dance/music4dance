using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace DanceLibrary
{
    public enum TempoKind { BPM, MPM, BPS };

    /// <summary>
    /// Represents a rate with a labe (MPM, BPM, BPS), in other word a tempo
    /// 
    /// This is an immutable class
    /// 
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class TempoType : IConversand
    {
        public static readonly string TempoSyntaxError = "Sytax error in tempo";

        static TempoType()
        {
            s_commonTempi = new List<TempoType>(5);
            s_commonTempi.Add(new TempoType(TempoKind.BPM));
            s_commonTempi.Add(new TempoType(TempoKind.BPS));
            s_commonTempi.Add(new TempoType(TempoKind.MPM,new Meter(2, 4)));
            s_commonTempi.Add(new TempoType(TempoKind.MPM,new Meter(3, 4)));
            s_commonTempi.Add(new TempoType(TempoKind.MPM,new Meter(4, 4)));
        }

        private TempoType()
        {
        }

        [JsonConstructor]
        public TempoType(TempoKind kind, Meter meter)
        {
            TempoKind = kind;
            Meter = meter;
        }

        public TempoType(TempoKind kind) : this(kind,null)
        {
        }

        /// <summary>
        /// Create a TempoType from a string of format "[BPS|BPM|([MPM ]{positive int}/{positive int})]"
        /// </summary>
        /// <param name="s"></param>
        public TempoType(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                TempoKind = TempoKind.BPM;
                return;
            }

            s = s.Trim();
            string[] fields = s.Split(new char[] { ' ' });
            if (fields == null || fields.Length < 1)
                throw new ArgumentOutOfRangeException(TempoSyntaxError);

            if (string.Equals(fields[0], "BPS", StringComparison.OrdinalIgnoreCase))
            {
                TempoKind = TempoKind.BPS;
                if (fields.Length > 1)
                    throw new ArgumentOutOfRangeException(TempoSyntaxError);
            }
            else if (string.Equals(fields[0], "BPM", StringComparison.OrdinalIgnoreCase))
            {
                TempoKind = TempoKind.BPM;
                if (fields.Length > 1)
                    throw new ArgumentOutOfRangeException(TempoSyntaxError);
            }
            else
            {
                TempoKind = TempoKind.MPM;
                if (string.Equals(fields[0], "MPM", StringComparison.OrdinalIgnoreCase))
                {
                    if (fields.Length != 2)
                        throw new ArgumentOutOfRangeException(TempoSyntaxError);

                    Meter = new Meter(fields[1]);
                }
                else
                {
                    Meter = new Meter(fields[0]);
                    if (fields.Length > 1)
                        throw new ArgumentOutOfRangeException(TempoSyntaxError);
                }
            }
        }

        /// <summary>
        /// Kind of tempo (MPM, BPM, BPS)
        /// </summary>
        [JsonProperty]
        public TempoKind TempoKind {get; private set;}

        /// <summary>
        /// Meter for MPM kinds
        /// </summary>
        [JsonProperty]
        public Meter Meter { get; private set; }

        public override string ToString()
        {
            switch (TempoKind)
            {
                case TempoKind.BPM: return "BPM";
                case TempoKind.BPS: return "BPS";
                case TempoKind.MPM: return string.Format("MPM {0}", Meter.ToString());
                default: System.Diagnostics.Debug.Assert(false); return "#ERROR#";
            }
        }

        public override bool Equals(object obj)
        {
            TempoType tempo = obj as TempoType;
            if (tempo == null)
                return false;
            else
                return (TempoKind == tempo.TempoKind) && (Meter == tempo.Meter);
        }

        public override int GetHashCode()
        {
            int hash = TempoKind.GetHashCode();
            if (Meter != null)
            {
                hash += 7*Meter.GetHashCode();
            }
            return hash;
        }

        public static bool operator ==(TempoType a, TempoType b)
        {
            // If both are null, or both are same instance, return true.
            if (System.Object.ReferenceEquals(a, b))
            {
                return true;
            }

            // Handle a is null case☺.
            if (((object)a == null))
            {
                return ((object)b == null);
            }

            return a.Equals(b);
        }

        public static bool operator !=(TempoType a, TempoType b)
        {
            return !(a == b);
        }

        public static ReadOnlyCollection<TempoType> CommonTempi
        {
            get
            {
                return new ReadOnlyCollection<TempoType>(s_commonTempi);
            }
        }


        private static List<TempoType> s_commonTempi;


        #region IConversand Implementation
        public Kind Kind
        {
            get { return Kind.Tempo; }
        }

        public string Name
        {
            get { return this.ToString(); }
        }

        public string Label
        {
            get { return TypeName; }
        }
        
        #endregion

        public static string TypeName
        {
            get { return "Tempo"; }
        }

    }
}
