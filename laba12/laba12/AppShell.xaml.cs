using laba12.Views;

namespace laba12;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(DeliveryDetailPage), typeof(DeliveryDetailPage));
        Routing.RegisterRoute(nameof(RouteMapPage), typeof(RouteMapPage));
        Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
        Routing.RegisterRoute(nameof(ProfilePage), typeof(ProfilePage));
    }
}