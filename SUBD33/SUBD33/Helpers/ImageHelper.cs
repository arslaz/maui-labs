namespace SUBD33.Helpers;

public static class ImageHelper
{
    public static ImageSource GetImageSource(string? photoPath)
    {
        if (string.IsNullOrWhiteSpace(photoPath))
        {
            return ImageSource.FromFile("avatar_default.png");
        }

        if (File.Exists(photoPath))
        {
            return ImageSource.FromFile(photoPath);
        }

        string cleanName = Path.GetFileNameWithoutExtension(photoPath);
        return ImageSource.FromFile($"{cleanName}.png");
    }
}
