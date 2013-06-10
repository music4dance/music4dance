using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DanceLibrary
{
    public enum Kind { Tempo, Duration };

    public interface IConversand
    {
        Kind Kind { get; }
        string Name { get; }
        string Label { get; }
    }

    public static class Conversands
    {
        static Conversands()
        {
            foreach (TempoType tempo in TempoType.CommonTempoes)
            {
                s_tempoConversands.Add(tempo);
                s_allConversands.Add(tempo);
            }

            foreach (DurationType sd in DurationType.CommonDurations)
            {
                s_durationConversands.Add(sd);
                s_allConversands.Add(sd);
            }
        }

        public static ReadOnlyCollection<IConversand> All
        {
            get
            {
                return new ReadOnlyCollection<IConversand>(s_allConversands);
            }
        }

        public static ReadOnlyCollection<IConversand> Meters
        {
            get
            {
                return new ReadOnlyCollection<IConversand>(s_tempoConversands);
            }
        }

        public static ReadOnlyCollection<IConversand> Durations
        {
            get
            {
                return new ReadOnlyCollection<IConversand>(s_durationConversands);
            }
        }

        public static string GetSerialization(IConversand c)
        {
            return c.Label + ":" + c.Name;
        }

        public static IConversand Deserialize(string s)
        {
            IConversand ret = null;

            string[] rg = s.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);

            if (rg.Length != 2)
                throw new ArgumentOutOfRangeException("conversand serialization must be of the form 'TypeName:Value'");

            // There has to be a better way of managing a factory for this pattern, but it's not
            //  obvious right now,sI'm going to just brute force it

            if (string.Equals(rg[0], TempoType.TypeName))
            {
                ret = new TempoType(rg[1]);
            }
            else if (string.Equals(rg[0], DurationType.TypeName))
            {
                ret = new DurationType(rg[1]);
            }
            else
            {
                throw new ArgumentOutOfRangeException("Only conversands of type 'TempoType' and 'Duration' are currently supported");
            }

            return ret;
        }

        private static List<IConversand> s_allConversands = new List<IConversand>();
        private static List<IConversand> s_tempoConversands = new List<IConversand>();
        private static List<IConversand> s_durationConversands = new List<IConversand>();
    }
}
