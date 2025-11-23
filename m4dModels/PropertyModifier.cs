namespace m4dModels
{
    public enum PropertyAction
    {
        ReplaceValue,
        ReplaceName,
        Replace,
        Append,
        Prepend,
        Remove,
    }

    public class PropertyModifier
    {
        public PropertyAction Action { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string Replace { get; set; }
        public List<SongProperty> Properties { get; set; }
    }
}
