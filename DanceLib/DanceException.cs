namespace DanceLibrary;

// Long Term we should generalize this to have a tag field that 
//  can be used to filter on any arbitrary tag.
public class DanceException : DanceObject
{
    public string Organization { get; set; }

    public DanceInstance DanceInstance { get; set; }

    public sealed override TempoRange TempoRange { get; set; }

    public override string Id => DanceInstance.Id + "-" + Organization;

    public override Meter Meter => DanceInstance.Meter;

    public override string Name => $"{DanceInstance.Name} ({Organization}";

    public bool ShouldSerializeId() => false;
    public bool ShouldSerializeMeter() => false;
    public bool ShouldSerializeName() => false;
    public bool ShouldSerializeDanceInstance() => false;
}
