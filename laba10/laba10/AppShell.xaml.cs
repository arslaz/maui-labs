using laba10.Views;

namespace laba10;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("ExamPage", typeof(ExamPage));
        Routing.RegisterRoute("TeacherEditPage", typeof(TeacherEditPage));
    }
}