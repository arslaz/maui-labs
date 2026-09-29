using System.Windows.Input;

namespace laba67.Controls;

public partial class BookCard : ContentView
{
    public static readonly BindableProperty BookTitleProperty =
        BindableProperty.Create(
            nameof(BookTitle),
            typeof(string),
            typeof(BookCard),
            string.Empty);

    public static readonly BindableProperty AuthorProperty =
        BindableProperty.Create(
            nameof(Author),
            typeof(string),
            typeof(BookCard),
            string.Empty);

    public static readonly BindableProperty ProgressProperty =
        BindableProperty.Create(
            nameof(Progress),
            typeof(double),
            typeof(BookCard),
            0.0);

    public static readonly BindableProperty PageInfoProperty =
        BindableProperty.Create(
            nameof(PageInfo),
            typeof(string),
            typeof(BookCard),
            string.Empty);

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(
            nameof(Command),
            typeof(ICommand),
            typeof(BookCard),
            null);

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(
            nameof(CommandParameter),
            typeof(object),
            typeof(BookCard),
            null);

    public string BookTitle
    {
        get => (string)GetValue(BookTitleProperty);
        set => SetValue(BookTitleProperty, value);
    }

    public string Author
    {
        get => (string)GetValue(AuthorProperty);
        set => SetValue(AuthorProperty, value);
    }

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public string PageInfo
    {
        get => (string)GetValue(PageInfoProperty);
        set => SetValue(PageInfoProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public BookCard()
    {
        InitializeComponent();
    }
}
