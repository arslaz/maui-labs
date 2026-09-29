using System.Globalization;

namespace laba6.Converters;

public class RatingToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double rating = 0;

        if (value is double d)
        {
            rating = d;
        }
        else if (value is int i)
        {
            rating = i;
        }
        else if (value != null && double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
        {
            rating = parsed;
        }

        // По условию: зеленый для рейтинга выше 4, желтый для 3-4, красный для ниже 3
        if (rating > 4.0)
            return Colors.Green;
        if (rating >= 3.0)
            return Colors.DarkOrange; // Отчетливый желто-оранжевый оттенок, отлично видимый на любом фоне
        
        return Colors.Red;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
