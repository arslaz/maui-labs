namespace laba7.Models;

/// <summary>
/// Модель финансовой операции (доход или расход)
/// </summary>
public class TransactionItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Описание операции
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Сумма операции
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Тип операции: true - доход, false - расход
    /// </summary>
    public bool IsIncome { get; set; }
    
    /// <summary>
    /// Дата проведения операции
    /// </summary>
    public DateTime Date { get; set; } = DateTime.Now;

    /// <summary>
    /// Форматированная сумма со знаком
    /// </summary>
    public string FormattedAmount => $"{(IsIncome ? "+" : "-")} {Amount:N0} ₽";

    /// <summary>
    /// Текстовое представление типа операции
    /// </summary>
    public string TypeText => IsIncome ? "Доход" : "Расход";
}
