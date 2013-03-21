using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace DanceLibrary
{
    public enum Kind { Rate, Duration };

    public interface IConversand
    {
        Kind Kind { get; }
        string Name { get; }        
    }

    public static class Conversands
    {
        static Conversands()
        {
            foreach (Meter meter in Meter.CommonMeters)
            {
                s_meterConversands.Add(meter);
                s_allConversands.Add(meter);
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
                return new ReadOnlyCollection<IConversand>(s_meterConversands);
            }
        }

        public static ReadOnlyCollection<IConversand> Durations
        {
            get
            {
                return new ReadOnlyCollection<IConversand>(s_durationConversands);
            }
        }

        public static int GetSerialization(IConversand c)
        {
            return s_allConversands.IndexOf(c);
        }

        public static IConversand Deserialize(int i)
        {
            return s_allConversands[i];
        }

        private static List<IConversand> s_allConversands = new List<IConversand>();
        private static List<IConversand> s_meterConversands = new List<IConversand>();
        private static List<IConversand> s_durationConversands = new List<IConversand>();
    }
}
