using System.Globalization;
using System.Resources;

namespace lab11.Resources.Languages;

public class AppResources
{
    private static ResourceManager? _resourceManager;

    public static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager("lab11.Resources.Languages.AppResources", typeof(AppResources).Assembly);

    public static string GetString(string key, CultureInfo? culture = null)
    {
        try
        {
            var value = ResourceManager.GetString(key, culture);
            if (!string.IsNullOrEmpty(value))
                return value;
        }
        catch
        {
            // Fallback handled below
        }

        return key;
    }
}
