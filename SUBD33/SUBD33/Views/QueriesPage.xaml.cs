using Microsoft.Maui.Controls.Shapes;
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
                _queryButtons[i].BackgroundColor = Color.FromArgb("#6366F1");
                _queryButtons[i].TextColor = Colors.White;
            }
            else
            {
                _queryButtons[i].FontAttributes = FontAttributes.None;
                _queryButtons[i].BackgroundColor = Color.FromArgb("#334155");
                _queryButtons[i].TextColor = Color.FromArgb("#F1F5F9");
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

    private void RenderTable(QueryResult result)
    {
        TableContainer.Children.Clear();

        if (result.Headers.Count == 0) return;

        var colWidths = new double[result.Headers.Count];
        for (int c = 0; c < result.Headers.Count; c++)
        {
            int maxLen = result.Headers[c].Length;
            for (int r = 0; r < result.Rows.Count; r++)
            {
                if (c < result.Rows[r].Count && result.Rows[r][c] != null)
                {
                    maxLen = Math.Max(maxLen, result.Rows[r][c].Length);
                }
            }
            colWidths[c] = Math.Clamp(maxLen * 9.5 + 32, 140.0, 380.0);
        }

        var headerGrid = new Grid
        {
            BackgroundColor = Color.FromArgb("#1E293B"),
            Padding = new Thickness(12, 10),
            ColumnSpacing = 16
        };

        for (int c = 0; c < result.Headers.Count; c++)
        {
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(colWidths[c]) });
            var headerLabel = new Label
            {
                Text = result.Headers[c],
                TextColor = Color.FromArgb("#38BDF8"),
                FontAttributes = FontAttributes.Bold,
                FontSize = 13,
                VerticalOptions = LayoutOptions.Center,
                WidthRequest = colWidths[c]
            };
            Grid.SetColumn(headerLabel, c);
            headerGrid.Children.Add(headerLabel);
        }

        var headerBorder = new Border
        {
            Stroke = Color.FromArgb("#475569"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(6, 6, 0, 0) },
            Content = headerGrid
        };
        TableContainer.Children.Add(headerBorder);

        for (int r = 0; r < result.Rows.Count; r++)
        {
            var rowData = result.Rows[r];
            bool isEven = (r % 2 == 0);

            var rowGrid = new Grid
            {
                BackgroundColor = isEven ? Color.FromArgb("#0F172A") : Color.FromArgb("#1E293B"),
                Padding = new Thickness(12, 10),
                ColumnSpacing = 16
            };

            for (int c = 0; c < rowData.Count; c++)
            {
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(colWidths[c]) });
                var cellLabel = new Label
                {
                    Text = rowData[c] ?? "",
                    TextColor = Color.FromArgb("#F8FAFC"),
                    FontSize = 13,
                    VerticalOptions = LayoutOptions.Center,
                    WidthRequest = colWidths[c]
                };
                Grid.SetColumn(cellLabel, c);
                rowGrid.Children.Add(cellLabel);
            }

            var rowBorder = new Border
            {
                Stroke = Color.FromArgb("#334155"),
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
