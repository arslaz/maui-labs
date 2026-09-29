using laba7.Views;

namespace laba7;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Использование NavigationPage для организации стека навигации (LIFO)
        var navPage = new NavigationPage(new OperationsPage())
        {
            BarBackgroundColor = Color.FromArgb("#1E293B"),
            BarTextColor = Colors.White
        };

        return new Window(navPage);
    }
}