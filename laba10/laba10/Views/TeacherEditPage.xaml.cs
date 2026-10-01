using laba10.ViewModels;

namespace laba10.Views;

public partial class TeacherEditPage : ContentPage, IQueryAttributable
{
    private readonly TeacherEditViewModel _viewModel;

    public TeacherEditPage(TeacherEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _viewModel.ApplyQueryAttributes(query);
    }
}
