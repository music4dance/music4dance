
namespace m4dModels
{
    public class LogValue
    {
        public LogValue()
        {

        }

        public LogValue(string name, string value, string old = null)
        {
            Name = name;
            Value = value;
            Old = old;
        }

        public bool IsAction => SongProperty.IsActionName(Name);

        public bool IsComplex => SongProperty.IsComplexName(Name);

        public string BaseName => SongProperty.ParseBaseName(Name);

        public string DanceQualifier => SongProperty.ParsePart(Name, 1);
        public string Name { get; set; }
        public string Value { get; set; }
        public string Old { get; set; }
    }
}