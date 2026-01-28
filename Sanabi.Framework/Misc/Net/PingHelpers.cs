using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Sanabi.Framework.Misc.Net;

/// <summary>
///     Provides some helpers for pinging, parsing, etc. IPs, addresses and whatnot.
/// </summary>
/// <remarks>
///     When resolving an IP address from a host via DNS, only the first IP address found
///         is used and cached.
/// </remarks>
// TODO: Find a better solution.
public static class NetHelpers
{
    private static readonly bool _ipv6Available = false;
    private static readonly HttpClient _defaultHttpClient = new();

    /// <summary>
    ///     Message sent in a UDP ping.
    /// </summary>
    public static readonly byte[] PingMessage = Encoding.ASCII.GetBytes("ping");

    /// <summary>
    ///     Time in milliseconds before a ping is considered timed-out.
    /// </summary>
    public const int PingTimeout = 800;

    /// <summary>
    ///     First suitable IP for this.
    /// </summary>
    private static readonly ConcurrentDictionary<string, IPAddress> _cachedIps = new();

    /// <summary>
    ///     Cached set of hosts that have no available IPs that we can use.
    /// </summary>
    private static readonly ConcurrentBag<string> _unavailableHosts = new();

    static NetHelpers()
    {
        // Test if IPV6 is available.
        foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (netInterface.OperationalStatus != OperationalStatus.Up)
                continue;

            var properties = netInterface.GetIPProperties();
            foreach (var address in properties.UnicastAddresses)
                _ipv6Available |= address.Address.AddressFamily == AddressFamily.InterNetworkV6;
        }

        SanabiLogger.LogFatal($"IPV6 Available: {(_ipv6Available ? "yes" : "no")}");
    }

    /// <summary>
    ///     Clears internal cached Hostname/IP-address pairs,
    ///         and cached set of hosts that have no available IP.
    /// </summary>
    public static void ClearCache()
    {
        _cachedIps.Clear();
        _unavailableHosts.Clear();
    }

    public static async Task<(bool, TimeSpan?)> TryPingIcmpAsync(string address)
    {
        var uri = new Uri(address);
        using var ping = new Ping();

        try
        {
            var reply = await ping.SendPingAsync(uri.Host, PingTimeout);
            if (reply.Status == IPStatus.Success)
                return (true, reply.RoundtripTime == 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(reply.RoundtripTime));

            return (false, null);
        }
        catch (PingException)
        {
            return (false, null);
        }
    }

    /// <summary>
    ///     Tries to return the IP of this host, and whether the attempt to retrieve the IP succeeded.
    ///         Fails catastrophically (not actually) if the IP is IPV4, but IPV6 isn't supported.
    /// </summary>
    public static async Task<IPAddress?> TryParseHostToIp(string host)
    {
        if (IPAddress.TryParse(host, out var ip))
        {
            if (!_ipv6Available && ip.AddressFamily == AddressFamily.InterNetworkV6)
                return null;

            return ip;
        }

        if (_unavailableHosts.Contains(host))
            return null;

        if (_cachedIps.TryGetValue(host, out var cachedIp))
            return cachedIp;

        // Try and get the first available IP and cache+return it
        var ips = await Dns.GetHostAddressesAsync(host);
        foreach (var foundIp in ips)
        {
            if (!_ipv6Available && foundIp.AddressFamily == AddressFamily.InterNetworkV6)
                continue;

            _cachedIps[host] = foundIp;
            return foundIp;
        }

        // No available IPs; mark host as unavailable for further use.
        _unavailableHosts.Add(host);
        return null;
    }

    /// <summary>
    ///     Pings a given address with a <see cref="UdpClient"/>. Will use the
    ///         server port specified by the address, or default if none is specified.
    ///
    ///     Gets the high-enough-resolution timespan of the RTT from the client to target.
    /// </summary>
    /// <returns>Returns whether the ping succeeded (didn't time-out), and the RTT.</returns>
    public static async Task<(bool, TimeSpan?)> TryPingUdpAsync(string address, int defaultPort)
    {
        var uri = new Uri(address);
        var ip = await TryParseHostToIp(uri.Host);
        if (ip == null)
            return (false, null);

        var port = uri.Port > 0 ? uri.Port : defaultPort;

        var endpoint = new IPEndPoint(ip, port);
        using var udpClient = new UdpClient(ip.AddressFamily);

        SanabiLogger.LogInfo($"Processing UDP: Host {uri.Host}, Port {port}, Realport {uri.Port}, IP {ip}, Address {address}");
        var startTimestamp = Stopwatch.GetTimestamp();

        await udpClient.SendAsync(PingMessage, PingMessage.Length, endpoint);

        var receiveTask = udpClient.ReceiveAsync();
        var completedTask = await Task.WhenAny(receiveTask, Task.Delay(PingTimeout));

        var rtt = TimeSpan.FromSeconds((double)(Stopwatch.GetTimestamp() - startTimestamp) / Stopwatch.Frequency);
        SanabiLogger.LogInfo($"RTT-TCP: {rtt.Milliseconds}");

        if (completedTask == receiveTask)
            return (true, rtt);
        else
            return (false, null);
    }

    /// <summary>
    ///     Pings a given address with a <see cref="TcpClient"/>. Will use the
    ///         server port specified by the address, or default if none is specified.
    ///
    ///     Gets the high-enough-resolution timespan of the RTT from the client to target.
    /// </summary>
    /// <returns>Returns whether the ping succeeded (didn't time-out), and the RTT.</returns>
    public static async Task<(bool, TimeSpan?)> TryPingTcpAsync(string address, int defaultPort)
    {
        var uri = new Uri(address);
        var ip = await TryParseHostToIp(uri.Host);
        if (ip == null)
        {
            SanabiLogger.LogInfo($"Bad TCP!! Host {uri.Host}, Realport {uri.Port}, IP {ip}, Address {address}");
            return (false, null);
        }

        var port = uri.Port > 0 ? uri.Port : defaultPort;

        SanabiLogger.LogInfo($"Processing TCP: Host {uri.Host}, Port {port}, Realport {uri.Port}, IP {ip}, Address {address}");
        using var tcpClient = new TcpClient(ip.AddressFamily);
        using var cancellationSource = new CancellationTokenSource(PingTimeout);

        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            await tcpClient.ConnectAsync(ip, port).WaitAsync(cancellationSource.Token);
            return (true, TimeSpan.FromSeconds((double)(Stopwatch.GetTimestamp() - startTimestamp) / Stopwatch.Frequency));
        }
        catch (Exception) when (cancellationSource.IsCancellationRequested)
        {
            SanabiLogger.LogInfo($"TCP timed out! Host {uri.Host}, Port {port}, Realport {uri.Port}, IP {ip}, Address {address}");
            return (false, null);
        }
    }

    /// <summary>
    ///     Pings a given HTTP(S) URI with a <see cref="HttpClient"/>. Must
    ///         specify a port.
    ///
    ///     Gets the high-enough-resolution timespan of the RTT from the client to target.
    /// </summary>
    /// <returns>Returns whether the ping succeeded (didn't time-out), and the RTT.</returns>
    public static async Task<(bool, TimeSpan?)> TryPingHttpAsync(Uri resolvedUri)
    {
        SanabiLogger.LogInfo($"Processing HTTP: Host {resolvedUri.Host}, Realport {resolvedUri.Port}, URI {resolvedUri}");
        using var cancellationSource = new CancellationTokenSource(PingTimeout);

        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            await _defaultHttpClient.GetAsync(resolvedUri, cancellationSource.Token);
            return (true, TimeSpan.FromSeconds((double)(Stopwatch.GetTimestamp() - startTimestamp) / Stopwatch.Frequency));
        }
        catch (TaskCanceledException) when (cancellationSource.IsCancellationRequested)
        {
            SanabiLogger.LogInfo($"HTTP timed out! Host {resolvedUri.Host}, Realport {resolvedUri.Port}, URI {resolvedUri}");
            return (false, null);
        }
    }
}
