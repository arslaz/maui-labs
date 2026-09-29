using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace laba6.Models;

public class Book : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string _author = string.Empty;
    private int _year;
    private bool _isRead;
    private double _rating;
    private string _coverImage = "dotnet_bot.png";

    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged();
            }
        }
    }

    public string Author
    {
        get => _author;
        set
        {
            if (_author != value)
            {
                _author = value;
                OnPropertyChanged();
            }
        }
    }

    public int Year
    {
        get => _year;
        set
        {
            if (_year != value)
            {
                _year = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsRead
    {
        get => _isRead;
        set
        {
            if (_isRead != value)
            {
                _isRead = value;
                OnPropertyChanged();
            }
        }
    }

    public double Rating
    {
        get => _rating;
        set
        {
            if (Math.Abs(_rating - value) > 0.001)
            {
                _rating = value;
                OnPropertyChanged();
            }
        }
    }

    public string CoverImage
    {
        get => _coverImage;
        set
        {
            if (_coverImage != value)
            {
                _coverImage = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
