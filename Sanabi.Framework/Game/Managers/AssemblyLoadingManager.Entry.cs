using System.Reflection;
using Sanabi.Framework.Misc;
using Sanabi.Framework.Patching;

namespace Sanabi.Framework.Game.Managers;

/// <summary>
///     Handles loading external mods from the mods directory,
///         into the game.
/// </summary>
public static partial class AssemblyLoadingManager
{
    private static readonly Queue<MethodInfo> _pendingEntSysManUpdateCallbacks = new();

    /// <summary>
    ///     Invokes a static method and enters it. The method may
    ///         have no parameters. If the method has one parameter,
    ///         whose type is a `Dictionary<string, Assembly?>`, the
    ///         method will be invoked with <see cref="AssemblyManager.Assemblies"/>
    ///         as the only parameter.
    /// </summary>
    /// <param name="async">Whether to run the method on another task.</param>
    public static void Enter(MethodInfo entryMethod, bool async = false)
    {
        var parameters = entryMethod.GetParameters();
        object?[]? invokedParameters = null;
        if (parameters.Length == 1 &&
            parameters[0].ParameterType == AssemblyManager.Assemblies.GetType())
            invokedParameters = [AssemblyManager.Assemblies];

        if (async)
            _ = Task.Run(async () => entryMethod.Invoke(null, invokedParameters));
        else
            entryMethod.Invoke(null, invokedParameters);

        Console.WriteLine($"Entered patch at {entryMethod.DeclaringType?.FullName}");
    }

    // Bitmap will fix it
    public static bool GetIsModEnabled(long bitmap, int index)
        => (bitmap & (1L << index)) != 0;
    private static void ModLoaderPostfix(ref dynamic __instance)
    {
        //var index = 0;
        while (_dataPendingAssemblyLoad.TryDequeue(out var modData))
        {
            //if (!GetIsModEnabled(SanabiConfig.ProcessConfig.LoadedExternalModsFlags, index++))
            //    continue;

            LoadModAssemblyIntoGame(ref __instance, modData);
        }
    }

    private static void EntSysManInitPostfix()
    {
        while (_pendingEntSysManUpdateCallbacks.TryDequeue(out var callbackInfo))
            callbackInfo.Invoke(null, null);
    }

    /// <summary>
    ///     Tries to get the entry point type for a mod assembly.
    ///         This is compatible with Marsey patches.
    /// </summary>
    public static Type? GetModAssemblyEntryType(Assembly assembly)
        => assembly.GetType("PatchEntry") ?? assembly.GetType("ModEntry") ?? assembly.GetType("MarseyEntry");

    private static void LogDelegate(AssemblyName asm, string message)
    {
        SanabiLogger.LogInfo($"PRT-{asm.FullName}: {message}");
    }

    /// <summary>
    ///     Ports MarseyLogger to work with a mod assembly patch;
    ///         i.e. makes it print here.
    /// </summary>
    /// <param name="assembly">The mod assembly.</param>
    public static void PortModMarseyLogger(Assembly assembly)
    {
        if (assembly.GetType("MarseyLogger") is not { } loggerType ||
            assembly.GetType("MarseyLogger+Forward") is not { } delegateType)
            return;

        var marseyLogDelegate = Delegate.CreateDelegate(delegateType, PatchHelpers.GetMethod(LogDelegate));

        var loggerForwardDelegateType = loggerType.GetField("logDelegate");
        loggerForwardDelegateType?.SetValue(null, marseyLogDelegate);
    }

    private static void LoadModAssemblyIntoGame(ref dynamic modLoader, ILoadedModData modData)
    {
        AssemblyHidingManager.HideAssembly(modData.Assembly);
        PortModMarseyLogger(modData.Assembly);

        _modInitMethod.Invoke(modLoader, (Assembly[])[modData.Assembly]);

        if (GetModAssemblyEntryType(modData.Assembly) is { } modEntryType)
        {
            if (modEntryType?.GetMethod("Entry", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) is { } modEntryMethod)
                Enter(modEntryMethod, async: false); // Non-async makes it possible to print logs properly

            if (modEntryType?.GetMethod("AfterEntitySystemsLoaded", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) is { } entitySystemsLoadedMethod)
                _pendingEntSysManUpdateCallbacks.Enqueue(entitySystemsLoadedMethod);
        }
    }
}
