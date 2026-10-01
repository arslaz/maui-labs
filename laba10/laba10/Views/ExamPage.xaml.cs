using laba10.ViewModels;

namespace laba10.Views;

public partial class ExamPage : ContentPage
{
    public ExamPage(ExamViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
