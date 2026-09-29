using laba7.Models;
using laba7.Services;

namespace laba7.Views;

public partial class AddTransactionPage : ContentPage
{
    private readonly decimal _currentBalance;

    /// <summary>
    /// Конструктор с параметрами, демонстрирующий передачу данных при переходе на страницу (навигации)
    /// </summary>
    /// <param name="pageTitle">Заголовок, переданный с предыдущей страницы</param>
    /// <param name="currentBalance">Текущий баланс для валидации расхода (баланс не должен уйти в минус)</param>
    /// <param name="defaultIsIncome">Значение по умолчанию для типа операции</param>
    public AddTransactionPage(string pageTitle, decimal currentBalance, bool defaultIsIncome = false)
    {
        InitializeComponent();

        _currentBalance = currentBalance;

        // Использование данных, переданных через конструктор
        Title = pageTitle;
        HeaderTitleLabel.Text = pageTitle;
        DataTransferNoteLabel.Text = $"Передано при навигации: Заголовок = «{pageTitle}», Баланс = {_currentBalance:N0} ₽";

        AvailableBalanceLabel.Text = $"{_currentBalance:N0} ₽";
        AvailableBalanceLabel.TextColor = _currentBalance >= 0 ? Color.FromArgb("#4ADE80") : Color.FromArgb("#F87171");

        if (defaultIsIncome)
        {
            IncomeRadio.IsChecked = true;
        }
        else
        {
            ExpenseRadio.IsChecked = true;
        }
    }

    /// <summary>
    /// Сохранение операции и передача данных через MessagingCenter
    /// </summary>
    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        string description = DescriptionEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(description))
        {
            await DisplayAlertAsync("Ошибка ввода", "Пожалуйста, введите описание операции.", "ОК");
            DescriptionEntry.Focus();
            return;
        }

        string amountText = AmountEntry.Text?.Trim().Replace(',', '.') ?? string.Empty;
        if (!decimal.TryParse(amountText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal amount) || amount <= 0)
        {
            await DisplayAlertAsync("Ошибка ввода", "Пожалуйста, введите корректную сумму больше 0 ₽.", "ОК");
            AmountEntry.Focus();
            return;
        }

        bool isIncome = IncomeRadio.IsChecked;

        // Проверка: расходы не могут превышать текущий баланс и уводить баланс в минус
        if (!isIncome && amount > _currentBalance)
        {
            await DisplayAlertAsync(
                "Недостаточно средств",
                $"Сумма расхода ({amount:N0} ₽) не может превышать текущий доступный баланс ({_currentBalance:N0} ₽)! Баланс не может уйти в минус.",
                "ОК");
            AmountEntry.Focus();
            return;
        }

        var newTransaction = new TransactionItem
        {
            Description = description,
            Amount = amount,
            IsIncome = isIncome,
            Date = DateTime.Now
        };

        // Отправка сообщения о сохранении новой операции через MessagingCenter на страницу операций
        MessagingCenter.Send<AddTransactionPage, TransactionItem>(this, "TransactionAdded", newTransaction);

        // Возврат на предыдущую страницу в стеке навигации NavigationPage
        await Navigation.PopAsync();
    }

    /// <summary>
    /// Отмена и возврат к предыдущей странице стека навигации
    /// </summary>
    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        // Метод PopAsync удаляет текущую страницу с вершины стека навигации
        await Navigation.PopAsync();
    }
}
