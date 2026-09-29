using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using MyApp.Namespace.ViewModels;

namespace MyApp.Namespace.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class MyXamarinAppPage : ContentPage
    {
        public MyXamarinAppPage()
        {
            InitializeComponent();
            BindingContext = new MyXamarinAppViewModel();
        }
    }
}
