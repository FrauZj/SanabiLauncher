using System.Reflection;
using Sanabi.Framework.Misc;
using Sanabi.Framework.Patching;
using SS14.Launcher;

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
    ///         have no parameters.
    /// </summary>
    /// <param name="async">Whether to run the method on another task.</param>
    public static void Enter(MethodInfo entryMethod, bool async = false)
    {
        var parameters = entryMethod.GetParameters();
        object?[]? invokedParameters = null;
        if (parameters.Length == 1 &&
            parameters[0].ParameterType == AssemblyManager.Assemblies.GetType())
            invokedParameters = [AssemblyManager.Assemblies];

        /*
        when only parameter is string:
        - give mod data path
        when only parameter is assemblies dict:
        - give assemblies dict
        when first parameter is assemblies dict and second parameter is string:
        - give assemblies dict
        - give mod data path
        */

        if (parameters.Length >= 1)
        {
            if (parameters[0].ParameterType == AssemblyManager.Assemblies.GetType())
            {
                if (parameters.Length == 2 &&
                    parameters[1].ParameterType == typeof(string))
                    invokedParameters = [AssemblyManager.Assemblies, LauncherPaths.SanabiModDataPath];
                else
                    invokedParameters = [AssemblyManager.Assemblies];
            }
            else if (parameters[0].ParameterType == typeof(string))
                invokedParameters = [LauncherPaths.SanabiModDataPath];
        }

        if (async)
            _ = Task.Run(async () => entryMethod.Invoke(null, invokedParameters));
        else
            entryMethod.Invoke(null, invokedParameters);

        Console.WriteLine($"Entered patch at {entryMethod.DeclaringType?.FullName}");
    }

    public static void EnterDb(MethodInfo entryMethod, bool async = false)
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
            if (modData.Assembly != null)
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
        AssemblyHidingManager.HideAssembly(modData.Assembly!);
        PortModMarseyLogger(modData.Assembly!);

        _modInitMethod.Invoke(modLoader, (Assembly[])[modData.Assembly!]);

        if (GetModAssemblyEntryType(modData.Assembly!) is { } modEntryType)
        {
            if (modEntryType?.GetMethod("Entry", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) is { } modEntryMethod)
                Enter(modEntryMethod, async: false); // Non-async makes it possible to print logs properly

            if (modEntryType?.GetMethod("AfterEntitySystemsLoaded", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) is { } entitySystemsLoadedMethod)
                _pendingEntSysManUpdateCallbacks.Enqueue(entitySystemsLoadedMethod);
        }
    }
}
