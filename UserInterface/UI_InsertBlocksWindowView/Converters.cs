using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView
{
    public sealed class EnumToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null && parameter != null &&
                   string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool && (bool)value && parameter != null)
            {
                return Enum.Parse(targetType, parameter.ToString());
            }

            return Binding.DoNothing;
        }
    }

    public sealed class EnumToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool matches = value != null && parameter != null &&
                           string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
            return matches ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public sealed class BoolToInverseVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = !(value is bool) || !(bool)value;
            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
