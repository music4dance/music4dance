using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

using DanceLibrary;
using System.Xml.Serialization;

namespace DanceCalc
{
    public class MainPageState : IXmlSerializable
    {
        public MainPageState()
        {
            Reset();
        }

        public void Reset()
        {
            Meter from = new Meter(4, 4);
            From = from;
            DurationType to = new DurationType(DurationKind.Measure);
            To = to;
            Timing = new SongTiming(from, to, 32.0M, 48M);
            Epsilon = 10M;
            Counting = false;
        }

        /// <summary>
        /// Are we currently attempting to count out a rhythm
        /// </summary>
        public bool Counting { get; set; }

        /// <summary>
        /// Type of conversion for the "From" control
        /// </summary>
        public IConversand From { get; set; }

        /// <summary>
        /// Type of conversion for the "To" control
        /// </summary>
        public IConversand To { get; set; }

        /// <summary>
        /// The song timing data
        /// </summary>
        [XmlIgnoreAttribute]
        public SongTiming Timing { get; set; }

        /// <summary>
        /// The current epsilon/fuzziness for filting
        /// </summary>
        [XmlIgnoreAttribute]
        public decimal Epsilon { get; set; }


        public System.Xml.Schema.XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(System.Xml.XmlReader reader)
        {
            string s = reader.GetAttribute("From");
            int i = 0;
            if (!int.TryParse(s,out i))
            {
                throw new InvalidCastException("From attribute must be an integer");
            }
            From = Conversands.Deserialize(i);

            s = reader.GetAttribute("To");
            if (!int.TryParse(s,out i))
            {
                throw new InvalidCastException("To attribute must be an integer");
            }
            To = Conversands.Deserialize(i);

            s = reader.GetAttribute("Timing");
            if (s != null)
            {
                Timing = new SongTiming(s);
            }

            s = reader.GetAttribute("Epsilon");
            decimal e = 10M;
            if (s != null && !decimal.TryParse(s,out e))
            {
                throw new InvalidCastException("Epsilon must be decimal");                
            }
            Epsilon = e;
        }

        public void WriteXml(System.Xml.XmlWriter writer)
        {
            string from = Conversands.GetSerialization(From).ToString();
            writer.WriteAttributeString("From", from);
            string to = Conversands.GetSerialization(To).ToString();
            writer.WriteAttributeString("To", to);
            string st = Timing.ToString();
            writer.WriteAttributeString("Timing", st);
            string e = Epsilon.ToString();
            writer.WriteAttributeString("Epsilon", e);
        }
    }
}
