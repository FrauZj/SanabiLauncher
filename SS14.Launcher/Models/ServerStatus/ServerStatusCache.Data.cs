using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Toolkit.Mvvm.ComponentModel;

namespace SS14.Launcher.Models.ServerStatus;

public sealed class ServerStatusData : ObservableObject, IServerStatusData
{
    private string? _name;
    private string? _desc;
    private TimeSpan? _ping;
    private int _playerCount;
    private int _softMaxPlayerCount;
    private DateTime? _roundStartTime;
    private GameRoundStatus _roundStatus;
    private ServerStatusCode _status = ServerStatusCode.FetchingStatus;
    private ServerStatusInfoCode _statusInfo = ServerStatusInfoCode.NotFetched;
    private ServerInfoLink[]? _links;
    private string[] _tags = Array.Empty<string>();

    public ServerStatusData(string address)
    {
        Address = address;
    }

    public ServerStatusData(string address, string hubAddress)
    {
        Address = address;
        HubAddress = hubAddress;
    }

    public async Task UpdateTrueIp()
    {
        // not dns
        if (!Uri.TryCreate(Address, UriKind.Absolute, out var uri))
            return;

        TrueAddress = "Fetching…";

        IPAddress[]? trueAddresses = null;
        using (var linkedToken = new CancellationTokenSource(2500))
            trueAddresses = await Dns.GetHostAddressesAsync(uri.Host, linkedToken.Token);

        if (trueAddresses == null)
        {
            TrueAddress = "Unknown (try refreshing)";
            goto doUpdate;
        }

        if (trueAddresses.Length == 0)
        {
            TrueAddress = "N/A";
            goto doUpdate;
        }

        if (uri.Port == -1)
            TrueAddress = $"{trueAddresses[0]}:{Global.DefaultServerPort} [unspecified port; defaulting to {Global.DefaultServerPort}]";
        else
            TrueAddress = $"{trueAddresses[0]}:{uri.Port}";

        TrueAddressResolved = true;

    doUpdate:
        TrueAddressUpdateCallback?.Invoke();
        return;
    }

    public Action? TrueAddressUpdateCallback = null;
    public bool TrueAddressResolved = false;
    public string TrueAddress = "Fetching…"; // Actual IP, where Address can just point to a site, this points to the host. Resolved when fetching server status
    public string Address { get; }
    public string? HubAddress { get; }

    public string? Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string? Description
    {
        get => _desc;
        set => SetProperty(ref _desc, value);
    }

    /// <summary>
    ///     Round-trip-time between the client and server.
    /// </summary>
    public TimeSpan? Ping
    {
        get => _ping;
        set => SetProperty(ref _ping, value);
    }

    public ServerStatusCode Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public ServerStatusInfoCode StatusInfo
    {
        get => _statusInfo;
        set => SetProperty(ref _statusInfo, value);
    }

    public int PlayerCount
    {
        get => _playerCount;
        set => SetProperty(ref _playerCount, value);
    }

    /// <summary>
    /// 0 means there's no maximum.
    /// </summary>
    public int SoftMaxPlayerCount
    {
        get => _softMaxPlayerCount;
        set => SetProperty(ref _softMaxPlayerCount, value);
    }

    public DateTime? RoundStartTime
    {
        get => _roundStartTime;
        set => SetProperty(ref _roundStartTime, value);
    }

    public GameRoundStatus RoundStatus
    {
        get => _roundStatus;
        set => SetProperty(ref _roundStatus, value);
    }

    public ServerInfoLink[]? Links
    {
        get => _links;
        set => SetProperty(ref _links, value);
    }

    public string[] Tags
    {
        get => _tags;
        set => SetProperty(ref _tags, value);
    }

    public CancellationTokenSource? InfoCancel;
}
