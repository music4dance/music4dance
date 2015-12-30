using System;
using System.Runtime.Serialization;
using DanceLibrary;

namespace m4dModels
{
    [DataContract]
    public class EchoTrack
    {
        static public EchoTrack BuildEchoTrack(dynamic response)
        {
            try
            {
                int? bpMeas = null;
                decimal? bpMin = null;

                if (response.response.status.code != 0)
                    return null;

                bpMeas = response.response.track.audio_summary.time_signature;
                bpMin = response.response.track.audio_summary.tempo;

                return new EchoTrack {BeatsPerMeasure = bpMeas, BeatsPerMinute = bpMin};
            }
            catch (Exception)
            {
                return null;
            }
        }

        [DataMember] public int? BeatsPerMeasure;
        [DataMember] public decimal? BeatsPerMinute;

        public string Meter
        {
            get
            {
                if (!BeatsPerMeasure.HasValue) return null;
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
