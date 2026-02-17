namespace Sanabi.Framework.Data;

/// <summary>
///     Lazy way of transferring data in same process.
/// </summary>
public static class LazySanabiConfig
{
    // For launcher process
    /// <inheritdoc cref="SanabiCVars.PingServers"/>
    public static bool PingServers = true;

    /// <inheritdoc cref="SanabiCVars.RandomiseServerPingQueryDelay"/>
    public static bool RandomiseServerPingQueryDelay = true;

    // For loader/game process
    public static string RobustClientExecutable = string.Empty;
}
