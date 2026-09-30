using Laba9.ViewModels;

namespace Laba9.Views;

public partial class EntryDetailPage : ContentPage
{
    public EntryDetailPage(EntryDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
