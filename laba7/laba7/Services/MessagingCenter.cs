namespace laba7.Services;

/// <summary>
/// Реализация механизма MessagingCenter (паттерн «издатель-подписчик»)
/// в соответствии с теоретической частью лабораторной работы.
/// Позволяет компонентам обмениваться сообщениями со слабой связанностью (через WeakReference).
/// </summary>
public static class MessagingCenter
{
    private class Subscription
    {
        public WeakReference Subscriber { get; }
        public Type SenderType { get; }
        public Type? ArgsType { get; }
        public string Message { get; }
        public Delegate Callback { get; }

        public Subscription(object subscriber, Type senderType, Type? argsType, string message, Delegate callback)
        {
            Subscriber = new WeakReference(subscriber);
            SenderType = senderType;
            ArgsType = argsType;
            Message = message;
            Callback = callback;
        }
    }

    private static readonly List<Subscription> _subscriptions = new();
    private static readonly object _lock = new();

    /// <summary>
    /// Отправляет сообщение подписчикам с аргументами
    /// </summary>
    /// <typeparam name="TSender">Тип отправителя</typeparam>
    /// <typeparam name="TArgs">Тип аргументов сообщения</typeparam>
    public static void Send<TSender, TArgs>(TSender sender, string message, TArgs args) where TSender : class
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(message);

        List<Delegate> callbacksToInvoke = new();

        lock (_lock)
        {
            _subscriptions.RemoveAll(s => !s.Subscriber.IsAlive);

            foreach (var sub in _subscriptions)
            {
                if (sub.Subscriber.IsAlive &&
                    (sub.SenderType.IsAssignableFrom(typeof(TSender)) || typeof(TSender).IsAssignableFrom(sub.SenderType)) &&
                    (sub.ArgsType == null || sub.ArgsType.IsAssignableFrom(typeof(TArgs)) || typeof(TArgs).IsAssignableFrom(sub.ArgsType)) &&
                    string.Equals(sub.Message, message, StringComparison.Ordinal))
                {
                    callbacksToInvoke.Add(sub.Callback);
                }
            }
        }

        foreach (var callback in callbacksToInvoke)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    callback.DynamicInvoke(sender, args);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MessagingCenter] Ошибка вызова подписчика: {ex}");
                }
            });
        }
    }

    /// <summary>
    /// Отправляет сообщение подписчикам без аргументов
    /// </summary>
    public static void Send<TSender>(TSender sender, string message) where TSender : class
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(message);

        List<Delegate> callbacksToInvoke = new();

        lock (_lock)
        {
            _subscriptions.RemoveAll(s => !s.Subscriber.IsAlive);

            foreach (var sub in _subscriptions)
            {
                if (sub.Subscriber.IsAlive &&
                    (sub.SenderType.IsAssignableFrom(typeof(TSender)) || typeof(TSender).IsAssignableFrom(sub.SenderType)) &&
                    sub.ArgsType == null &&
                    string.Equals(sub.Message, message, StringComparison.Ordinal))
                {
                    callbacksToInvoke.Add(sub.Callback);
                }
            }
        }

        foreach (var callback in callbacksToInvoke)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    callback.DynamicInvoke(sender);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MessagingCenter] Ошибка вызова подписчика: {ex}");
                }
            });
        }
    }

    /// <summary>
    /// Подписывает объект на получение сообщений с аргументами
    /// </summary>
    public static void Subscribe<TSender, TArgs>(object subscriber, string message, Action<TSender, TArgs> callback) where TSender : class
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(callback);

        lock (_lock)
        {
            _subscriptions.RemoveAll(s => !s.Subscriber.IsAlive);
            _subscriptions.Add(new Subscription(subscriber, typeof(TSender), typeof(TArgs), message, callback));
        }
    }

    /// <summary>
    /// Подписывает объект на получение сообщений без аргументов
    /// </summary>
    public static void Subscribe<TSender>(object subscriber, string message, Action<TSender> callback) where TSender : class
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(callback);

        lock (_lock)
        {
            _subscriptions.RemoveAll(s => !s.Subscriber.IsAlive);
            _subscriptions.Add(new Subscription(subscriber, typeof(TSender), null, message, callback));
        }
    }

    /// <summary>
    /// Отписывает объект от получения сообщений с аргументами
    /// </summary>
    public static void Unsubscribe<TSender, TArgs>(object subscriber, string message) where TSender : class
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        ArgumentNullException.ThrowIfNull(message);

        lock (_lock)
        {
            _subscriptions.RemoveAll(s =>
                !s.Subscriber.IsAlive ||
                (s.Subscriber.Target == subscriber &&
                 (s.SenderType == typeof(TSender) || s.SenderType.IsAssignableFrom(typeof(TSender)) || typeof(TSender).IsAssignableFrom(s.SenderType)) &&
                 (s.ArgsType == typeof(TArgs) || (s.ArgsType != null && s.ArgsType.IsAssignableFrom(typeof(TArgs)))) &&
                 string.Equals(s.Message, message, StringComparison.Ordinal)));
        }
    }

    /// <summary>
    /// Отписывает объект от получения сообщений без аргументов
    /// </summary>
    public static void Unsubscribe<TSender>(object subscriber, string message) where TSender : class
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        ArgumentNullException.ThrowIfNull(message);

        lock (_lock)
        {
            _subscriptions.RemoveAll(s =>
                !s.Subscriber.IsAlive ||
                (s.Subscriber.Target == subscriber &&
                 (s.SenderType == typeof(TSender) || s.SenderType.IsAssignableFrom(typeof(TSender)) || typeof(TSender).IsAssignableFrom(s.SenderType)) &&
                 s.ArgsType == null &&
                 string.Equals(s.Message, message, StringComparison.Ordinal)));
        }
    }
}
