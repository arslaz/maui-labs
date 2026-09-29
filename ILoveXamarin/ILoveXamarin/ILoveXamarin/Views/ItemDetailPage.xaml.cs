using ILoveXamarin.ViewModels;
using System.ComponentModel;
using Xamarin.Forms;

namespace ILoveXamarin.Views
{
    public partial class ItemDetailPage : ContentPage
    {
        public ItemDetailPage()
        {
            InitializeComponent();
            BindingContext = new ItemDetailViewModel();
        }
    }
}