using Laba9.Views;

namespace Laba9;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("entrydetail", typeof(EntryDetailPage));
        Routing.RegisterRoute("settings", typeof(SettingsPage));
    }
}