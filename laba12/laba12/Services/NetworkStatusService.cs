namespace laba12.Services;

public interface INetworkStatusService
{
    bool IsConnected { get; }
    event Action<bool>? ConnectivityChanged;
}

public class NetworkStatusService : INetworkStatusService
{
    public bool IsConnected => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
    public event Action<bool>? ConnectivityChanged;

    public NetworkStatusService()
    {
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        var connected = e.NetworkAccess == NetworkAccess.Internet;
        ConnectivityChanged?.Invoke(connected);
    }
}
