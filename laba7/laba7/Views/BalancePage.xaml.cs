using System.Windows.Input;
using laba7.Models;
using laba7.Services;

namespace laba7.Views;

public partial class BalancePage : ContentPage
{
    private decimal _currentBalance;
    private int _operationsCount;

    // Свойства, заполняемые перед вызовом PushAsync (передача данных через свойства страницы)
    public TransactionItem? LastTransaction { get; set; }
    public string PageSubtitle { get; set; } = string.Empty;

    public ICommand DeleteRecentCommand { get; }

    /// <summary>
    /// Конструктор страницы, демонстрирующий передачу данных через параметры конструктора
    /// </summary>
    /// <param name="initialBalance">Текущий баланс</param>
    /// <param name="initialCount">Количество операций</param>
    public BalancePage(decimal initialBalance, int initialCount)
    {
        InitializeComponent();

        _currentBalance = initialBalance;
        _operationsCount = initialCount;

        DeleteRecentCommand = new Command(OnDeleteRecent);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // 1. Отображаем данные, полученные через конструктор
        NavConstructorInfoLabel.Text = $"Через конструктор: Баланс = {_currentBalance:N0} ₽, Операций = {_operationsCount}";

        // 2. Отображаем данные, полученные через свойства перед PushAsync
        NavPropertyInfoLabel.Text = $"Через свойства: Подзаголовок = «{PageSubtitle}»";

        UpdateUI();

        // Подписка на сообщения MessagingCenter для динамического обновления данных
        // 1. Сообщение об изменении баланса
        MessagingCenter.Subscribe<Page, decimal>(this, "BalanceChanged", (sender, updatedBalance) =>
        {
            _currentBalance = updatedBalance;
            UpdateUI();
        });

        // 2. Сообщение об удалении операции
        MessagingCenter.Subscribe<Page, TransactionItem>(this, "TransactionDeleted", (sender, deletedItem) =>
        {
            if (LastTransaction != null && LastTransaction.Id == deletedItem.Id)
            {
                LastTransaction = null;
            }
            if (_operationsCount > 0)
            {
                _operationsCount--;
            }
            UpdateUI();
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Корректное освобождение ресурсов при возврате (PopAsync): отписка от сообщений MessagingCenter
        MessagingCenter.Unsubscribe<Page, decimal>(this, "BalanceChanged");
        MessagingCenter.Unsubscribe<Page, TransactionItem>(this, "TransactionDeleted");
    }

    private void UpdateUI()
    {
        BalanceValueLabel.Text = $"{(_currentBalance >= 0 ? "+" : "")}{_currentBalance:N0} ₽";
        BalanceValueLabel.TextColor = _currentBalance >= 0 ? Color.FromArgb("#4ADE80") : Color.FromArgb("#F87171");

        BalanceStatusLabel.Text = _currentBalance >= 0 
            ? "Финансовое состояние стабильное (баланс положительный)" 
            : "Внимание: расходы превышают доходы (баланс отрицательный)";

        // Обновление отображения пользовательского элемента ContentView (TransactionCard)
        if (LastTransaction != null)
        {
            RecentTransactionCard.IsVisible = true;
            NoTransactionsLabel.IsVisible = false;

            RecentTransactionCard.Description = LastTransaction.Description;
            RecentTransactionCard.Amount = LastTransaction.Amount;
            RecentTransactionCard.IsIncome = LastTransaction.IsIncome;
            RecentTransactionCard.DeleteCommandParameter = LastTransaction;
        }
        else
        {
            RecentTransactionCard.IsVisible = false;
            NoTransactionsLabel.IsVisible = true;
        }
    }

    /// <summary>
    /// Удаление последней операции с проверкой баланса и отправкой сообщения через MessagingCenter
    /// </summary>
    private async void OnDeleteRecent()
    {
        if (LastTransaction == null) return;

        // Проверка: удаление дохода не должно приводить к уходу баланса в минус
        if (LastTransaction.IsIncome && (_currentBalance - LastTransaction.Amount) < 0)
        {
            await DisplayAlertAsync(
                "Невозможно удалить",
                $"Удаление этой операции уменьшит баланс на {LastTransaction.Amount:N0} ₽, из-за чего баланс станет отрицательным. Баланс не может уйти в минус!",
                "ОК");
            return;
        }

        bool confirm = await DisplayAlertAsync(
            "Удаление операции",
            $"Удалить «{LastTransaction.Description}» на сумму {LastTransaction.Amount:N0} ₽?",
            "Удалить",
            "Отмена");

        if (confirm)
        {
            var itemToDelete = LastTransaction;
            LastTransaction = null;

            // Отправка сообщений всем заинтересованным страницам
            MessagingCenter.Send<Page, TransactionItem>(this, "TransactionDeleted", itemToDelete);

            decimal delta = itemToDelete.IsIncome ? -itemToDelete.Amount : itemToDelete.Amount;
            _currentBalance += delta;
            MessagingCenter.Send<Page, decimal>(this, "BalanceChanged", _currentBalance);

            UpdateUI();
        }
    }

    /// <summary>
    /// Возврат к предыдущей странице в стеке навигации
    /// </summary>
    private async void OnBackButtonClicked(object? sender, EventArgs e)
    {
        // Метод PopAsync возвращает пользователя к предыдущей странице стека
        await Navigation.PopAsync();
    }
}
