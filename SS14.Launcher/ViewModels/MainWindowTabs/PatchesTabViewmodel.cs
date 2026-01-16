using System.IO;
using Splat;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;
using SS14.Common.Data.CVars;
using System.Diagnostics;
using Sanabi.Framework.Data;
using System.Collections.ObjectModel;
using Sanabi.Framework.Game.Managers;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public class SanabiTabViewModel : MainWindowTabViewModel
{
    public DataManager Cfg { get; }

    public ObservableCollection<LoadedPatchViewmodel> PatchList { get; set; } = new();

    public SanabiTabViewModel()
    {
        Cfg = Locator.Current.GetRequiredService<DataManager>();
        RefreshPatches();
    }

    // Binding; do not rename/remove/change signature
    public void RefreshPatches()
    {
        PatchList.Clear();

        if (!AssemblyLoadingManager.TryGetExternalDlls(out var externalDlls))
            return;

        var i = 0;
        var originalMap = Cfg.GetCVar(SanabiCVars.LoadedExternalModsFlags);

        foreach (var dll in externalDlls)
        {
            var patchVm = new LoadedPatchViewmodel(this, Path.GetFileName(dll), i++);
            patchVm.SetEnabled(AssemblyLoadingManager.GetIsModEnabled(originalMap, i), originalMap);

            PatchList.Add(patchVm);
        }
    }

    internal void SetAndCommitCvar<T>(CVarDef<T> cVarDef, T newValue)
    {
        Cfg.SetCVar(cVarDef, newValue);
        Cfg.CommitConfig();
    }

    // Binding; do not rename/remove/change signature
    public static void OpenModDirectory()
    {
        Process.Start(new ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = LauncherPaths.SanabiModsPath
        });
    }

    public override string Name => "Sanabi";

    public bool PatchingEnabled
    {
        get => Cfg.GetCVar(SanabiCVars.PatchingEnabled);
        set => SetAndCommitCvar(SanabiCVars.PatchingEnabled, value);
    }

    public bool PatchingLevel
    {
        get => Cfg.GetCVar(SanabiCVars.PatchingLevel);
        set => SetAndCommitCvar(SanabiCVars.PatchingLevel, value);
    }

    public bool HwidPatchEnabled
    {
        get => Cfg.GetCVar(SanabiCVars.HwidPatchEnabled);
        set => SetAndCommitCvar(SanabiCVars.HwidPatchEnabled, value);
    }

    public bool LoadInternalMods
    {
        get => Cfg.GetCVar(SanabiCVars.LoadInternalMods);
        set => SetAndCommitCvar(SanabiCVars.LoadInternalMods, value);
    }

    public bool LoadExternalMods
    {
        get => Cfg.GetCVar(SanabiCVars.LoadExternalMods);
        set => SetAndCommitCvar(SanabiCVars.LoadExternalMods, value);
    }
}

public class LoadedPatchViewmodel(SanabiTabViewModel parentVm, string filename, int index) : ViewModelBase
{
    private SanabiTabViewModel _parentVm = parentVm;

    // Binding; do not rename/remove/change signature
    public string Filename { get; set; } = filename;

    // Bitmap index
    public int Index { get; set; } = index;

    // Binding; do not rename/remove/change signature
    private bool IsEnabled
    {
        get => AssemblyLoadingManager.GetIsModEnabled(_parentVm.Cfg.GetCVar(SanabiCVars.LoadedExternalModsFlags), Index);
        set => SetEnabled(value, null);
    }

    public void SetEnabled(bool value, long? map = null)
    {
        map ??= _parentVm.Cfg.GetCVar(SanabiCVars.LoadedExternalModsFlags);
        if (value)
            map |= 1L << Index;
        else
            map &= ~(1L << Index);

        _parentVm.SetAndCommitCvar(SanabiCVars.LoadedExternalModsFlags, map.Value);
    }
}
