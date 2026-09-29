namespace laba67.Services;

/// <summary>
/// Реализация механизма MessagingCenter для обмена сообщениями между компонентами приложения
/// по паттерну «издатель-подписчик» в соответствии с требованиями лабораторной работы.
/// </summary>
public static class MessagingCenter
{
    private class Subscription
    {
        public WeakReference Subscriber { get; set; } = null!;
        public string Message { get; set; } = string.Empty;
        public Delegate Callback { get; set; } = null!;
        public Type SenderType { get; set; } = null!;
        public Type ArgsType { get; set; } = null!;
    }

    private static readonly List<Subscription> Subscriptions = new();
    private static readonly object LockObj = new();

    /// <summary>
    /// Подписывает объект на получение сообщений
    /// </summary>
    public static void Subscribe<TSender, TArgs>(object subscriber, string message, Action<TSender, TArgs> callback)
        where TSender : class
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(callback);

        lock (LockObj)
        {
            // Очищаем устаревшие ссылки перед добавлением новой
            Subscriptions.RemoveAll(s => !s.Subscriber.IsAlive);

            Subscriptions.Add(new Subscription
            {
                Subscriber = new WeakReference(subscriber),
                Message = message,
                Callback = callback,
                SenderType = typeof(TSender),
                ArgsType = typeof(TArgs)
            });
        }
    }

    /// <summary>
    /// Отправляет сообщение подписчикам
    /// </summary>
    public static void Send<TSender, TArgs>(TSender sender, string message, TArgs args)
        where TSender : class
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(message);

        List<Action<TSender, TArgs>> matchedCallbacks = new();

        lock (LockObj)
        {
            Subscriptions.RemoveAll(s => !s.Subscriber.IsAlive);

            foreach (var sub in Subscriptions)
            {
                if (sub.Message == message &&
                    sub.SenderType.IsAssignableFrom(typeof(TSender)) &&
                    sub.ArgsType.IsAssignableFrom(typeof(TArgs)) &&
                    sub.Callback is Action<TSender, TArgs> action)
                {
                    matchedCallbacks.Add(action);
                }
            }
        }

        foreach (var action in matchedCallbacks)
        {
            action(sender, args);
        }
    }

    /// <summary>
    /// Отписывает объект от получения сообщений
    /// </summary>
    public static void Unsubscribe<TSender, TArgs>(object subscriber, string message)
        where TSender : class
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        ArgumentNullException.ThrowIfNull(message);

        lock (LockObj)
        {
            Subscriptions.RemoveAll(s =>
                s.Message == message &&
                s.SenderType == typeof(TSender) &&
                s.ArgsType == typeof(TArgs) &&
                (!s.Subscriber.IsAlive || s.Subscriber.Target == subscriber));
        }
    }
}
