using System.Globalization;

namespace laba12.Converters;

public class BoolToInvertedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }
}

public class StringNotEmptyToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrWhiteSpace(value as string);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StatusColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "Доставлен" => Color.FromArgb("#10B981"), // Изумрудно-зеленый
            "Прибыл" => Color.FromArgb("#06B6D4"),    // Бирюзовый (PATCH отметка прибытия)
            "В пути" => Color.FromArgb("#3B82F6"),    // Синий
            "Отказ" => Color.FromArgb("#EF4444"),     // Красный (PUT отказ от груза)
            _ => Color.FromArgb("#F59E0B")            // Янтарно-желтый (Ожидает)
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
