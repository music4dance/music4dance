using System;
using System.Runtime.Serialization;

namespace m4dModels
{
    [DataContract]
    public class EchoTrack
    {
        public static EchoTrack BuildEchoTrack(dynamic response)
        {
            try
            {
                if (response == null)
                {
                    return null;
                }

                int? bpMeas = response.time_signature;
                decimal? bpMin = response.tempo;
                float? danceability = (float)response.danceability;
                float? energy = (float)response.energy;
                float? valence = (float)response.valence;

                return new EchoTrack
                {
                    BeatsPerMeasure = bpMeas, BeatsPerMinute = bpMin, Danceability = danceability,
                    Energy = energy, Valence = valence
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        [DataMember]
        public int? BeatsPerMeasure;

        [DataMember]
        public decimal? BeatsPerMinute;

        [DataMember]
        public float? Danceability;

        [DataMember]
        public float? Energy;

        [DataMember]
        public float? Valence;

        public string Meter
        {
            get
            {
                if (!BeatsPerMeasure.HasValue)
                {
                    return null;
                }

                switch (BeatsPerMeasure)
                {
                    case 2:
                    case 3:
                    case 4:
                        return $"{BeatsPerMeasure}/4";
                    case 6:
                    case 9:
                    case 12:
                        return $"{BeatsPerMeasure}/8";
                    default:
                        return null;
                }
            }
        }
    }
}
