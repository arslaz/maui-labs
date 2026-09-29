using laba67.Pages;

namespace laba67;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Инициализация стека навигации с использованием NavigationPage
        var navigationPage = new NavigationPage(new CatalogPage())
        {
            BarBackgroundColor = Color.FromArgb("#4F46E5"),
            BarTextColor = Colors.White
        };

        return new Window(navigationPage);
    }
}