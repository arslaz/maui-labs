using SUBD33.Data;
using SUBD33.Models;

namespace SUBD33.Views;

public partial class QueriesPage : ContentPage
{
    private readonly List<Button> _queryButtons = new();

    public QueriesPage()
    {
        InitializeComponent();
        _queryButtons.AddRange(new[] { BtnQuery1, BtnQuery2, BtnQuery3, BtnQuery4, BtnQuery5, BtnQuery6, BtnQuery7 });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ExecuteQuery(1);
    }

    private void OnExecuteQueryClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.CommandParameter?.ToString(), out int queryNum))
        {
            ExecuteQuery(queryNum);
        }
    }

    private async void ExecuteQuery(int queryNum)
    {
        for (int i = 0; i < _queryButtons.Count; i++)
        {
            if (i == queryNum - 1)
            {
                _queryButtons[i].FontAttributes = FontAttributes.Bold;
            }
            else
            {
                _queryButtons[i].FontAttributes = FontAttributes.None;
            }
        }

        try
        {
            var result = DatabaseHelper.Instance.ExecuteLabQuery(queryNum);
            LblCurrentQueryTitle.Text = result.Title;
            LblSqlQuery.Text = result.SqlQuery;
            LblRowsCount.Text = $"Найдено строк: {result.RowCount}";

            RenderTable(result);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Ошибка запроса", ex.Message, "OK");
        }
    }

    // Построение таблицы (аналог DataGridView)
    private void RenderTable(QueryResult result)
    {
        TableContainer.Children.Clear();

        if (result.Headers.Count == 0) return;

        // Шапка таблицы
        var headerGrid = new Grid
        {
            BackgroundColor = Color.FromArgb("#E2E8F0"),
            Padding = new Thickness(10, 8),
            ColumnSpacing = 12
        };

        for (int c = 0; c < result.Headers.Count; c++)
        {
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var headerLabel = new Label
            {
                Text = result.Headers[c],
                FontAttributes = FontAttributes.Bold,
                FontSize = 13,
                VerticalOptions = LayoutOptions.Center,
                MinimumWidthRequest = 110
            };
            Grid.SetColumn(headerLabel, c);
            headerGrid.Children.Add(headerLabel);
        }

        var headerBorder = new Border
        {
            Stroke = Color.FromArgb("#CBD5E1"),
            StrokeThickness = 1,
            Content = headerGrid
        };
        TableContainer.Children.Add(headerBorder);

        // Строки данных таблицы
        for (int r = 0; r < result.Rows.Count; r++)
        {
            var rowData = result.Rows[r];
            bool isEven = (r % 2 == 0);

            var rowGrid = new Grid
            {
                BackgroundColor = isEven ? Colors.White : Color.FromArgb("#F8FAFC"),
                Padding = new Thickness(10, 8),
                ColumnSpacing = 12
            };

            for (int c = 0; c < rowData.Count; c++)
            {
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var cellLabel = new Label
                {
                    Text = rowData[c],
                    FontSize = 13,
                    VerticalOptions = LayoutOptions.Center,
                    MinimumWidthRequest = 110
                };
                Grid.SetColumn(cellLabel, c);
                rowGrid.Children.Add(cellLabel);
            }

            var rowBorder = new Border
            {
                Stroke = Color.FromArgb("#E2E8F0"),
                StrokeThickness = 0.5,
                Content = rowGrid
            };
            TableContainer.Children.Add(rowBorder);
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
