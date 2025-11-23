using System.Runtime.Serialization;

namespace m4dModels;

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
            return !BeatsPerMeasure.HasValue
                ? null
                : BeatsPerMeasure switch
                {
                    2 or 3 or 4 => $"{BeatsPerMeasure}/4",
                    6 or 9 or 12 => $"{BeatsPerMeasure}/8",
                    _ => null,
                };
        }
    }
}
