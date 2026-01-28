using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using Sanabi.Framework.Data;
using Sanabi.Framework.Game.Patches;
using Sanabi.Framework.Misc;
using Sanabi.Framework.Patching;
using SS14.Launcher;

namespace Sanabi.Framework.Game.Managers;

/// <summary>
///     Handles loading external mods from the mods directory,
///         into the game.
/// </summary>
public static class AssemblyLoadingManager
{
    public static int TotalExternalModCount = 0;
    private static readonly Queue<Assembly> _assembliesPendingLoad = new(); // Important to be Queue rather than Stack to preserve order of assemblies
    private static readonly Queue<MethodInfo> _pendingEntSysManUpdateCallbacks = new();
    private static MethodInfo _modInitMethod = default!;

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

    public static bool TryGetExternalDlls([MaybeNullWhen(false)] out string[] externalDlls)
    {
        externalDlls = Directory.GetFiles(LauncherPaths.SanabiModsPath, "*.dll", SearchOption.TopDirectoryOnly);
        if (externalDlls.Length == 0)
            return false;

        // wtf why would you ever need more than 64 mods
        if (externalDlls.Length > 64)
        {
            Array.Resize(ref externalDlls, 64);
            SanabiLogger.LogError("Only the first 64 mods will be loaded!");
        }

        return true;
    }

    // Bitmap will fix it
    public static bool GetIsModEnabled(long bitmap, int index)
        => (bitmap & (1L << index)) != 0;

    [PatchEntry(PatchRunLevel.Engine)]
    private static void Start()
    {
        if (!SanabiConfig.ProcessConfig.LoadExternalMods)
            return;

        var internalModLoader = ReflectionManager.GetTypeByQualifiedName("Robust.Shared.ContentPack.ModLoader");
        var baseModLoader = ReflectionManager.GetTypeByQualifiedName("Robust.Shared.ContentPack.BaseModLoader");

        _modInitMethod = PatchHelpers.GetMethod(internalModLoader, "InitMod")
            ?? throw new InvalidOperationException("Couldn't resolve BaseModLoader.InitMod!");

        PatchHelpers.PatchMethod(
            internalModLoader,
            "TryLoadModules",
            ModLoaderPostfix,
            HarmonyPatchType.Postfix
        );

        PatchHelpers.PatchMethod(
            ReflectionManager.GetTypeByQualifiedName("Robust.Shared.GameObjects.EntitySystemManager"),
            "Initialize",
            EntSysManInitPostfix,
            HarmonyPatchType.Postfix
        );

        if (!TryGetExternalDlls(out var externalDlls))
            return;

        TotalExternalModCount = externalDlls.Length;
        foreach (var dll in externalDlls)
            _assembliesPendingLoad.Enqueue(Assembly.LoadFrom(dll));
    }

    private static void ModLoaderPostfix(ref dynamic __instance)
    {
        var index = 0;
        while (_assembliesPendingLoad.TryDequeue(out var assembly))
        {
            if (!GetIsModEnabled(SanabiConfig.ProcessConfig.LoadedExternalModsFlags, index++))
                continue;

            LoadModAssembly(ref __instance, assembly);
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

    private static void LoadModAssembly(ref dynamic modLoader, Assembly modAssembly)
    {
        AssemblyHidingManager.HideAssembly(modAssembly);
        PortModMarseyLogger(modAssembly);

        _modInitMethod.Invoke(modLoader, (Assembly[])[modAssembly]);

        if (GetModAssemblyEntryType(modAssembly) is { } modEntryType)
        {
            if (modEntryType?.GetMethod("Entry", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) is { } modEntryMethod)
                Enter(modEntryMethod, async: false); // Non-async makes it possible to debug

            if (modEntryType?.GetMethod("AfterEntitySystemsLoaded", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) is { } entitySystemsLoadedMethod)
                _pendingEntSysManUpdateCallbacks.Enqueue(entitySystemsLoadedMethod);
        }
    }
}
