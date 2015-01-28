
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

        public bool IsAction
        {
            get { return SongProperty.IsActionName(Name); }
        }

        public bool IsComplex
        {
            get { return SongProperty.IsComplexName(Name); }
        }

        public string BaseName
        {
            get { return SongProperty.ParseBaseName(Name); }
        }


        public string DanceQualifier
        {
            get { return SongProperty.ParsePart(Name, 1); }
        }
        public string Name { get; set; }
        public string Value { get; set; }
        public string Old { get; set; }
    }
}