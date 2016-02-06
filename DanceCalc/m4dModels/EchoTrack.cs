using System;
using System.Runtime.Serialization;
using DanceLibrary;

namespace m4dModels
{
    [DataContract]
    public class EchoTrack
    {
        // TODONEXT: Verify that this works (
        static public EchoTrack BuildEchoTrack(dynamic response)
        {
            try
            {
                if (response.response.status.code != 0)
                    return null;

                dynamic audioSummary = response.response.track.audio_summary;
                int? bpMeas = audioSummary.time_signature;
                decimal? bpMin = audioSummary.tempo;
                float? danceability = (float)audioSummary.danceability;
                float? energy = (float)audioSummary.energy;
                float? valence = (float)audioSummary.valence;

                return new EchoTrack {BeatsPerMeasure = bpMeas, BeatsPerMinute = bpMin, Danceability = danceability, Energy = energy, Valence = valence};
            }
            catch (Exception)
            {
                return null;
            }
        }

        [DataMember] public int? BeatsPerMeasure;
        [DataMember] public decimal? BeatsPerMinute;
        [DataMember] public float? Danceability;
        [DataMember] public float? Energy;
        [DataMember] public float? Valence;

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
