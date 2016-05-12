using System;
using System.ComponentModel;
using System.Globalization;

namespace m4dModels
{
    class SongFilterConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string)
            {
                return new SongFilter(value as string);
            }
            return base.ConvertFrom(context, culture, value);
        }
    }
}
