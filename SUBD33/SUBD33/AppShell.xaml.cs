using SUBD33.Views;

namespace SUBD33;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(IncidentsPage), typeof(IncidentsPage));
        Routing.RegisterRoute(nameof(AddIncidentPage), typeof(AddIncidentPage));
        Routing.RegisterRoute(nameof(StudentsPage), typeof(StudentsPage));
        Routing.RegisterRoute(nameof(AddStudentPage), typeof(AddStudentPage));
        Routing.RegisterRoute(nameof(StudentCardPage), typeof(StudentCardPage));
        Routing.RegisterRoute(nameof(QueriesPage), typeof(QueriesPage));
    }
}