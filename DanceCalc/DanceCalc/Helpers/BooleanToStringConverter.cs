using System;
using System.Globalization;
using System.Windows.Data;

namespace DanceCalc
{
    public class BooleanToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string t = parameter as string;
            string[] values = {"_FALSE_","_TRUE_"};
            if (t != null)
            {
                string[] valuesT = t.Split(',');                
                if (valuesT.Length > 1)
                {
                    values = valuesT;
                }
            }

            if (value is bool && ((bool)value) == true)
            {
                return values[1];
            }
            else
            {
                return values[0];
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

    }
}
