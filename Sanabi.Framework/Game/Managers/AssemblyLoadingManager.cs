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
public static partial class AssemblyLoadingManager
{
    public static int TotalExternalModCount = 0;
    private static readonly Queue<ILoadedModData> _dataPendingAssemblyLoad = new(); // Important to be Queue rather than Stack to preserve order of assemblies
    private static readonly Queue<LoadedFolderData> _dataPendingResourceLoad = new(); // Important to be Queue rather than Stack to preserve order of assemblies
    private static MethodInfo _modInitMethod = default!;
    private static MethodInfo _iResourceManagerAddRootsMethod = default!; // has to be interface or else it cant get called on dynamic or whatever IDFK
    private static ConstructorInfo _dirLoaderConstructorData = default!;
    private static object _universalLoaderSawmill = default!;
    private static object _resPathRootValue = default!;

    public const string ResourcesFolderName = "Resources";

    [PatchEntry(PatchRunLevel.Engine)]
    private static void Start()
    {
        if (!SanabiConfig.ProcessConfig.LoadExternalMods)
            return;

        var internalModLoader = ReflectionManager.GetTypeByQualifiedName("Robust.Shared.ContentPack.ModLoader");
        var baseModLoader = ReflectionManager.GetTypeByQualifiedName("Robust.Shared.ContentPack.BaseModLoader");

        _modInitMethod = PatchHelpers.GetMethod(internalModLoader, "InitMod")
            ?? throw new InvalidOperationException("Couldn't resolve BaseModLoader.InitMod!");

        _iResourceManagerAddRootsMethod = AccessTools.Method("Robust.Shared.ContentPack.IResourceManager:AddRoot");

        var sawmillImplType = ReflectionManager.GetTypeByQualifiedName("Robust.Shared.Log.LogManager+Sawmill");

        // DEMENTED
        // `public Sawmill(Sawmill? parent, string name)`, although sawmill impl type is nullable, its notnullable here
        _universalLoaderSawmill = PatchHelpers.GetConstructorAndMakeInstance(sawmillImplType, [sawmillImplType, typeof(string)], [null, new Guid().ToString()]);


        _resPathRootValue = PatchHelpers.GetConstructorAndMakeInstance("Robust.Shared.Utility.ResPath", [typeof(string)], ["/"]);

        var sawmillInterfaceType = ReflectionManager.GetTypeByQualifiedName("Robust.Shared.Log.ISawmill");
        var dirLoaderType = ReflectionManager.GetTypeByQualifiedName("Robust.Shared.ContentPack.ResourceManager+DirLoader");

        // (DirectoryInfo directory, ISawmill sawmill, bool checkCasing)
        _dirLoaderConstructorData = dirLoaderType.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, [typeof(DirectoryInfo), sawmillInterfaceType, typeof(bool)])
            ?? throw new InvalidOperationException("Couldn't resolve DirLoader constructor!");

        PatchHelpers.PatchMethod(
            ReflectionManager.GetTypeByQualifiedName("Robust.Shared.ProgramShared"),
            "DoMounts",
            DoMountsPrefix,
            HarmonyPatchType.Prefix
        );

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

        if (!TryGetExternalMods(out var modules))
            return;

        TotalExternalModCount = modules.Count;

        var index = 0;
        foreach (var module in modules)
        {
            SanabiLogger.LogInfo($"Trying to load mod {module.Name}");
            if (!GetIsModEnabled(SanabiConfig.ProcessConfig.LoadedExternalModsFlags, index++))
                continue;

            SanabiLogger.LogInfo($"Loaded mod {module.Name}");

            // if dll is present, load it
            if (module.DllFullName != string.Empty)
                module.Assembly = Assembly.LoadFrom(module.GetDllFullPath());

            _dataPendingAssemblyLoad.Enqueue(module);

            if (module is LoadedFolderData folderModData)
                _dataPendingResourceLoad.Enqueue(folderModData);
        }
    }

    private static void DoMountsPrefix(ref object res) // cant use `dynamic`; have to call IResourceManagerInternal's (yes the interface) AddRoot method
    {
        while (_dataPendingResourceLoad.TryDequeue(out var modData))
        {
            var dirInfo = new DirectoryInfo(modData.GetResourcesPath());

            var dirLoader = _dirLoaderConstructorData.Invoke([dirInfo, _universalLoaderSawmill, false]);
            _iResourceManagerAddRootsMethod.Invoke(res, [_resPathRootValue, dirLoader]);
        }
    }

    public static bool TryGetExternalMods([MaybeNullWhen(false)] out List<ILoadedModData> externalMods)
    {
        var modPaths = Directory.GetFileSystemEntries(LauncherPaths.SanabiModsPath, "*", SearchOption.TopDirectoryOnly);

        if (modPaths.Length == 0)
        {
            externalMods = null;
            return false;
        }

        // setup array
        externalMods = new(modPaths.Length);

        // wtf why would you ever need more than 64 mods
        if (modPaths.Length > 64)
        {
            Array.Resize(ref modPaths, 64);
            SanabiLogger.LogError("Only the first 64 mods will be loaded!");
        }

        foreach (var modPath in modPaths)
        {
            if (Directory.Exists(modPath)) // Path points to dir
            {
                string? dllPath = null;
                string? resourcesPath = null;

                foreach (var modSubPath in Directory.GetFileSystemEntries(modPath, "*", SearchOption.TopDirectoryOnly))
                {
                    if (Path.GetExtension(modSubPath) == ".dll")
                        dllPath = modSubPath;
                    else if (Directory.Exists(modSubPath) && new DirectoryInfo(modSubPath).Name == ResourcesFolderName)
                        resourcesPath = modSubPath;

                    if (dllPath != null && resourcesPath != null)
                        break;
                }

                // only resourcesPath is necessary
                if (resourcesPath == null)
                {
                    SanabiLogger.LogError($"Couldn't resolve resourcesPath [{resourcesPath ?? "N/A"}] on a folder! Path: {modPath}");
                    continue;
                }

                externalMods.Add(new LoadedFolderData(new DirectoryInfo(modPath).Name, string.Empty, modPath, null));
            }
            else // Path points to file
            {
                if (Path.GetExtension(modPath) != ".dll")
                {
                    SanabiLogger.LogError($"Tried to load mod file, but wasn't a `.dll`! Path: {modPath}");
                    continue;
                }

                externalMods.Add(new LoadedDllData(new DirectoryInfo(modPath).Name, Path.GetFileName(modPath), modPath, null! /* not loaded yet */));
            }
        }

        return true;
    }
}

/// <summary>
///     Represents a loaded assembly, optionally with extra loaded resources.
/// </summary>
public interface ILoadedModData
{
    /// <summary>
    ///     Name of this folder/file, has file-extension if applicable.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    ///     Name of the loaded DLL, with file-extension. Uses system-format paths.
    ///         For mods that load by folders, this will be empty if there is no DLL.
    /// </summary>
    public string DllFullName { get; set; }

    /// <summary>
    ///     Path to this folder/dll. Uses system-format paths. Has file-extension if applicable.
    /// </summary>
    public string ModPath { get; set; }

    /// <summary>
    ///     Mod assembly.
    /// </summary>
    public Assembly? Assembly { get; set; }

    public string GetDllFullPath();
}

/// <summary>
///     For standalone .DLLs being loaded.
///         <see cref="ModPath"/> would be path to the `.dll`.
/// </summary>
public sealed class LoadedDllData(string name, string dllFullName, string modPath, Assembly assembly) : ILoadedModData
{
    public string Name { get; set; } = name;

    public string DllFullName { get; set; } = dllFullName;

    public string ModPath { get; set; } = modPath;

    /// <summary>
    ///     Mod assembly. For purely DLLmods, will never be null.
    /// </summary>
    public Assembly? Assembly { get; set; } = assembly;

    public string GetDllFullPath() => ModPath;
}

/// <summary>
///     For mods that are folders with:
///     - loaded resources
///     - OPTIONALLY, a loaded `.dll`.
///
///     <see cref="ModPath"/> would be path to the folder containing
///         resources folder and the `.dll` (if it's there).
/// </summary>
public sealed class LoadedFolderData(string name, string dllFullName, string modPath, Assembly? assembly) : ILoadedModData
{
    public string Name { get; set; } = name;

    public string DllFullName { get; set; } = dllFullName;

    public string ModPath { get; set; } = modPath;

    /// <summary>
    ///     Mod assembly. For resourcemods, may be null.
    /// </summary>
    public Assembly? Assembly { get; set; } = assembly;

    public string GetDllFullPath() => Path.Join(ModPath, DllFullName);

    public string GetResourcesPath() => Path.Join(ModPath, AssemblyLoadingManager.ResourcesFolderName);
}
