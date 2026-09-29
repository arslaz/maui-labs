using System.Collections.ObjectModel;
using System.Windows.Input;
using laba7.Models;
using laba7.Services;

namespace laba7.Views;

public partial class OperationsPage : ContentPage
{
    public ObservableCollection<TransactionItem> Transactions { get; } = new();
    public ICommand DeleteCommand { get; }

    public OperationsPage()
    {
        InitializeComponent();

        // Явная привязка источника данных для надежности отображения
        TransactionsCollectionView.ItemsSource = Transactions;

        // Инициализация команды удаления с параметром TransactionItem
        DeleteCommand = new Command<TransactionItem>(OnDeleteTransaction);

        // Подписка на сообщения MessagingCenter
        // 1. Сообщение о сохранении новой операции со страницы AddTransactionPage
        MessagingCenter.Subscribe<AddTransactionPage, TransactionItem>(this, "TransactionAdded", (sender, newTransaction) =>
        {
            Transactions.Insert(0, newTransaction);
            UpdateSummary();

            // Отправляем сообщение об изменении баланса для заинтересованных страниц (страницы баланса)
            MessagingCenter.Send<Page, decimal>(this, "BalanceChanged", CalculateBalance());
        });

        // 2. Сообщение об удалении операции
        MessagingCenter.Subscribe<Page, TransactionItem>(this, "TransactionDeleted", (sender, deletedItem) =>
        {
            var match = Transactions.FirstOrDefault(t => t.Id == deletedItem.Id);
            if (match != null)
            {
                Transactions.Remove(match);
                UpdateSummary();
            }
        });

        // Добавим начальные данные для наглядности работы интерфейса
        Transactions.Add(new TransactionItem
        {
            Description = "Зарплата за месяц",
            Amount = 95000m,
            IsIncome = true,
            Date = DateTime.Now.AddDays(-2)
        });

        Transactions.Add(new TransactionItem
        {
            Description = "Покупка продуктов",
            Amount = 4320m,
            IsIncome = false,
            Date = DateTime.Now.AddDays(-1)
        });

        Transactions.Add(new TransactionItem
        {
            Description = "Оплата коммунальных услуг",
            Amount = 5600m,
            IsIncome = false,
            Date = DateTime.Now
        });

        Transactions.Add(new TransactionItem
        {
            Description = "Кэшбэк по карте",
            Amount = 1450m,
            IsIncome = true,
            Date = DateTime.Now
        });

        UpdateSummary();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateSummary();
    }

    /// <summary>
    /// Удаление операции через команду с проверкой баланса и отправкой сообщения в MessagingCenter
    /// </summary>
    private async void OnDeleteTransaction(TransactionItem? item)
    {
        if (item == null) return;

        // Проверка: удаление дохода не должно приводить к уходу баланса в минус
        if (item.IsIncome && (CalculateBalance() - item.Amount) < 0)
        {
            await DisplayAlertAsync(
                "Невозможно удалить", 
                $"Удаление этой операции уменьшит баланс на {item.Amount:N0} ₽, из-за чего баланс станет отрицательным. Баланс не может уйти в минус!", 
                "ОК");
            return;
        }

        bool confirm = await DisplayAlertAsync(
            "Удаление операции", 
            $"Вы действительно хотите удалить «{item.Description}» на сумму {item.Amount:N0} ₽?", 
            "Удалить", 
            "Отмена");

        if (confirm)
        {
            Transactions.Remove(item);
            UpdateSummary();

            // Сообщение 1: уведомляем все заинтересованные страницы об удалении операции
            MessagingCenter.Send<Page, TransactionItem>(this, "TransactionDeleted", item);

            // Сообщение 2: уведомляем об изменении баланса
            MessagingCenter.Send<Page, decimal>(this, "BalanceChanged", CalculateBalance());
        }
    }

    /// <summary>
    /// Переход на страницу добавления операции.
    /// Демонстрирует передачу данных через конструктор страницы (заголовок и текущий баланс для проверки лимита).
    /// </summary>
    private async void OnAddTransactionClicked(object? sender, EventArgs e)
    {
        // Передача данных через конструктор: заголовок страницы и текущий баланс
        var addPage = new AddTransactionPage("Новая финансовая операция", currentBalance: CalculateBalance(), defaultIsIncome: false);
        await Navigation.PushAsync(addPage);
    }

    /// <summary>
    /// Переход на страницу баланса.
    /// Демонстрирует передачу данных через конструктор и свойства страницы.
    /// </summary>
    private async void OnViewBalanceClicked(object? sender, EventArgs e)
    {
        decimal currentBalance = CalculateBalance();
        int totalCount = Transactions.Count;

        // Передача данных через конструктор (баланс и число операций)
        var balancePage = new BalancePage(currentBalance, totalCount)
        {
            // Передача данных через свойства страницы перед вызовом PushAsync
            LastTransaction = Transactions.FirstOrDefault(),
            PageSubtitle = $"Сформировано на основе {totalCount} операций"
        };

        await Navigation.PushAsync(balancePage);
    }

    private decimal CalculateBalance()
    {
        decimal income = Transactions.Where(t => t.IsIncome).Sum(t => t.Amount);
        decimal expense = Transactions.Where(t => !t.IsIncome).Sum(t => t.Amount);
        return income - expense;
    }

    private void UpdateSummary()
    {
        decimal balance = CalculateBalance();
        QuickBalanceLabel.Text = $"{(balance >= 0 ? "+" : "")}{balance:N0} ₽";
        QuickBalanceLabel.TextColor = balance >= 0 ? Color.FromArgb("#4ADE80") : Color.FromArgb("#F87171");
        OperationsCountLabel.Text = $"{Transactions.Count} операций";
        EmptyListLabel.IsVisible = Transactions.Count == 0;
    }
}
