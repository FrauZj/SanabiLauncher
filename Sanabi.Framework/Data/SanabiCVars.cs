using JetBrains.Annotations;
using SS14.Common.Data.CVars;

namespace Sanabi.Framework.Data;

/// <summary>
///     Contains definitions for all SanabiLauncher-specific configuration values.
/// </summary>
[UsedImplicitly]
public static partial class SanabiCVars
{
    /// <summary>
    ///     Include "SS14-Launcher-Fingerprint" and this launcher's fingerprint, in
    ///         DefaultRequestHeaders for *every* single HTTP query we make?
    ///
    ///     This may be a detection vector.
    /// </summary>
    public static readonly CVarDef<bool> PassFingerprint = CVarDef.Create("PassFingerprint", true);

    /// <summary>
    ///     Generates a random value for "SS14-Launcher-Fingerprint" to be used for the duration of every launch.
    ///         Original fingerprint is still saved and will be used when this CVar is turned
    ///         off.
    ///
    ///     Not passed if <see cref="PassFingerprint"/> is off.
    /// </summary>
    public static readonly CVarDef<bool> PassSpoofedFingerprint = CVarDef.Create("SpoofFingerprint", false);

    /// <summary>
    ///     Willingly (opt-in) send your (optionally spoofed) HWID to server?
    /// </summary>
    public static readonly CVarDef<bool> AllowHwid = CVarDef.Create("AllowHwid", true);

    /// <summary>
    ///     Should the launcher start on the login menu
    ///         (where no external API has yet been queried) or on the homepage
    ///         (where hub API is likely to be queried)? Turning this on can help against detection
    ///         if you know what you are doing.
    ///
    ///     This is intended to work like this regardless of if the launcher is starting
    ///         logged in or not.
    /// </summary>
    public static readonly CVarDef<bool> StartOnLoginMenu = CVarDef.Create("StartOnLoginMenu", false);

    /// <summary>
    ///     In hub settings, do we use the hub(s) that the launcher uses
    ///         as default?
    ///
    ///     This setting exists so that you can willingly enable/disable
    ///         the default hub(s) and not need to remember it's URL,
    ///         opposed to outright removing the default hub and requiring
    ///         the user to have it manually listed as a hub.
    /// </summary>
    public static readonly CVarDef<bool> EnableStockHub = CVarDef.Create("EnableStockHub", true);

    /// <summary>
    ///     Should server ping be determined when opening their dropdown? Some ABSOLUTELY 'SESSED servers might use
    ///         this to detect you.
    /// </summary>
    public static readonly CVarDef<bool> PingServers = CVarDef.Create("PingServers", true);
}
