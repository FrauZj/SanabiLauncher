using JetBrains.Annotations;
using SS14.Common.Data.CVars;

namespace Sanabi.Framework.Data;

/// <summary>
///     Contains definitions for all SanabiLauncher-specific configuration values.
/// </summary>
[UsedImplicitly]
public static partial class SanabiAccountCVars
{
    /// <summary>
    ///     Whether to generate a new spoofing seed when setting this account as the active one.
    ///         Set to false when done.
    /// </summary>
    public static readonly CVarDef<bool> ShouldRegenerateSeed = CVarDef.Create("ShouldRegenerateSeed", true);

    /// <summary>
    ///     Seed to be used for generating HWID in <see cref="Game.Patches.HwidPatch"/>, and spoofed fingerprint.
    ///         This is an ulong value bit-interpreted as a long. This is done because SQLite
    ///         is weird with ulong values.
    /// </summary>
    public static readonly CVarDef<long> SpoofingSeed = CVarDef.Create("SpoofingSeed", 1L);
}
