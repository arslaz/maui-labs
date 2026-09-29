using System.Windows.Input;

namespace laba7.Controls;

/// <summary>
/// Пользовательский элемент управления для отображения финансовой операции на базе ContentView.
/// Содержит BindableProperty для привязки данных и команду для удаления.
/// </summary>
public partial class TransactionCard : ContentView
{
    #region Bindable Properties

    /// <summary>
    /// Привязываемое свойство Description (описание операции)
    /// </summary>
    public static readonly BindableProperty DescriptionProperty =
        BindableProperty.Create(
            nameof(Description),
            typeof(string),
            typeof(TransactionCard),
            string.Empty,
            propertyChanged: (bindable, oldValue, newValue) =>
            {
                if (bindable is TransactionCard card)
                {
                    card.DescriptionLabel.Text = (string?)newValue ?? string.Empty;
                }
            });

    /// <summary>
    /// Привязываемое свойство Amount (сумма операции)
    /// </summary>
    public static readonly BindableProperty AmountProperty =
        BindableProperty.Create(
            nameof(Amount),
            typeof(decimal),
            typeof(TransactionCard),
            0m,
            propertyChanged: (bindable, oldValue, newValue) =>
            {
                if (bindable is TransactionCard card)
                {
                    card.UpdateVisuals();
                }
            });

    /// <summary>
    /// Привязываемое свойство IsIncome (тип операции: доход или расход)
    /// </summary>
    public static readonly BindableProperty IsIncomeProperty =
        BindableProperty.Create(
            nameof(IsIncome),
            typeof(bool),
            typeof(TransactionCard),
            false,
            propertyChanged: (bindable, oldValue, newValue) =>
            {
                if (bindable is TransactionCard card)
                {
                    card.UpdateVisuals();
                }
            });

    /// <summary>
    /// Команда для удаления операции
    /// </summary>
    public static readonly BindableProperty DeleteCommandProperty =
        BindableProperty.Create(
            nameof(DeleteCommand),
            typeof(ICommand),
            typeof(TransactionCard),
            null,
            propertyChanged: (bindable, oldValue, newValue) =>
            {
                if (bindable is TransactionCard card)
                {
                    card.DeleteBtn.Command = (ICommand?)newValue;
                }
            });

    /// <summary>
    /// Параметр для команды удаления
    /// </summary>
    public static readonly BindableProperty DeleteCommandParameterProperty =
        BindableProperty.Create(
            nameof(DeleteCommandParameter),
            typeof(object),
            typeof(TransactionCard),
            null,
            propertyChanged: (bindable, oldValue, newValue) =>
            {
                if (bindable is TransactionCard card)
                {
                    card.DeleteBtn.CommandParameter = newValue;
                }
            });

    /// <summary>
    /// Видимость кнопки удаления (позволяет скрыть кнопку при необходимости)
    /// </summary>
    public static readonly BindableProperty ShowDeleteButtonProperty =
        BindableProperty.Create(
            nameof(ShowDeleteButton),
            typeof(bool),
            typeof(TransactionCard),
            true,
            propertyChanged: (bindable, oldValue, newValue) =>
            {
                if (bindable is TransactionCard card)
                {
                    card.DeleteBtn.IsVisible = (bool)(newValue ?? true);
                }
            });

    #endregion

    #region Properties

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public decimal Amount
    {
        get => (decimal)GetValue(AmountProperty);
        set => SetValue(AmountProperty, value);
    }

    public bool IsIncome
    {
        get => (bool)GetValue(IsIncomeProperty);
        set => SetValue(IsIncomeProperty, value);
    }

    public ICommand? DeleteCommand
    {
        get => (ICommand?)GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public object? DeleteCommandParameter
    {
        get => GetValue(DeleteCommandParameterProperty);
        set => SetValue(DeleteCommandParameterProperty, value);
    }

    public bool ShowDeleteButton
    {
        get => (bool)GetValue(ShowDeleteButtonProperty);
        set => SetValue(ShowDeleteButtonProperty, value);
    }

    #endregion

    public TransactionCard()
    {
        InitializeComponent();
        UpdateVisuals();
    }

    /// <summary>
    /// Обновление визуального представления в зависимости от типа операции и суммы
    /// </summary>
    private void UpdateVisuals()
    {
        if (AmountLabel == null || TypeBadge == null || TypeIcon == null || TypeTextLabel == null)
            return;

        if (IsIncome)
        {
            TypeBadge.BackgroundColor = Color.FromArgb("#DCFCE7"); // Светло-зеленый
            TypeIcon.Text = "+";
            TypeIcon.TextColor = Color.FromArgb("#16A34A"); // Зеленый
            TypeTextLabel.Text = "Доход";
            AmountLabel.Text = $"+ {Amount:N0} ₽";
            AmountLabel.TextColor = Color.FromArgb("#16A34A");
        }
        else
        {
            TypeBadge.BackgroundColor = Color.FromArgb("#FEE2E2"); // Светло-красный
            TypeIcon.Text = "−";
            TypeIcon.TextColor = Color.FromArgb("#DC2626"); // Красный
            TypeTextLabel.Text = "Расход";
            AmountLabel.Text = $"- {Amount:N0} ₽";
            AmountLabel.TextColor = Color.FromArgb("#DC2626");
        }
    }
}
