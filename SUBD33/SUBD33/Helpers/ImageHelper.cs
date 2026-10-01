namespace SUBD33.Helpers;

public static class ImageHelper
{
    public static ImageSource GetImageSource(string? photoPath)
    {
        if (string.IsNullOrWhiteSpace(photoPath))
        {
            return ImageSource.FromFile("avatar_default.png");
        }

        // Если это абсолютный путь к файлу на диске
        if (File.Exists(photoPath))
        {
            return ImageSource.FromFile(photoPath);
        }

        // Если указано имя файла ресурса (например student1.png или student1)
        string cleanName = Path.GetFileNameWithoutExtension(photoPath);
        return ImageSource.FromFile($"{cleanName}.png");
    }
}
