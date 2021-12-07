using System;
using System.ComponentModel;
using System.Globalization;

namespace m4dModels
{
    internal class SongFilterConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture,
            object value)
        {
            return value is string s ? new SongFilter(s) : base.ConvertFrom(context, culture, value);
        }
    }
}
