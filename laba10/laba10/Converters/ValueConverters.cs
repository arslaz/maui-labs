using System.Globalization;

namespace laba10.Converters;

public class ScoreToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double score)
        {
            if (score >= 80) return Color.FromArgb("#10B981"); // Зеленый
            if (score >= 60) return Color.FromArgb("#F59E0B"); // Янтарный
            return Color.FromArgb("#EF4444"); // Красный
        }
        return Color.FromArgb("#6B7280");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class LevelToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string level)
        {
            return level.ToUpperInvariant() switch
            {
                "C1" or "C2" => Color.FromArgb("#7C3AED"), // Deep purple
                "B2" => Color.FromArgb("#2563EB"),         // Blue
                "B1" => Color.FromArgb("#0D9488"),         // Teal
                "A2" => Color.FromArgb("#EA580C"),         // Orange
                _ => Color.FromArgb("#4F46E5")
            };
        }
        return Color.FromArgb("#4F46E5");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BooleanToStringConverter : IValueConverter
{
    public string TrueText { get; set; } = string.Empty;
    public string FalseText { get; set; } = string.Empty;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b) return TrueText;
        return FalseText;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BooleanToColorConverter : IValueConverter
{
    public Color TrueColor { get; set; } = Color.FromArgb("#10B981");
    public Color FalseColor { get; set; } = Color.FromArgb("#EF4444");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b) return TrueColor;
        return FalseColor;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return false;
    }
}

public class SelectedOptionToBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value is bool b && b) ? Color.FromArgb("#312E81") : Color.FromArgb("#1E293B");
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class SelectedOptionToBorderConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value is bool b && b) ? Color.FromArgb("#818CF8") : Color.FromArgb("#334155");
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class SelectedOptionToBadgeColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value is bool b && b) ? Color.FromArgb("#4F46E5") : Color.FromArgb("#334155");
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class PairSelectedToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value is bool b && b) ? Color.FromArgb("#6366F1") : Color.FromArgb("#334155");
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
