using Mono.Unix.Native;

namespace Sanabi.Framework.Data;

/// <summary>
///     Global variables for Sanabi-exclusive content
/// </summary>
public static class SanabiGlobal
{
    /// <summary>
    ///     Maximum number of queries done when pinging a server.
    /// </summary>
    public const int MaximumPingQueryAttempts = 6;

    /// <summary>
    ///     Minimum number of queries to a server that must be successful.
    /// </summary>
    public const int MinimumSuccessfulPingQueryAttempts = 2;

    /// <summary>
    ///     Amount of time to pass between ping queries.
    /// </summary>
    public static readonly TimeSpan PingQueryInterval = TimeSpan.FromMilliseconds(50);
}
