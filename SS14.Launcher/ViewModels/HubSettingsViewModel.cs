using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DynamicData;
using Mono.Posix;
using Sanabi.Framework.Data;
using Splat;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels;

public class HubSettingsViewModel : ViewModelBase
{
    private static readonly DataManager _dataManager = Locator.Current.GetRequiredService<DataManager>();

    public bool EnableStockHub { get; set; }

    private static readonly Uri[] _defaultHubsStatic = ConfigConstants.MirrorHubUrls;

    // Binding; do not rename/remove/change signature
    public static Uri[] DefaultHubs => _defaultHubsStatic;

    /// <summary>
    ///     List of default hub addresses, accounting for
    ///         whether hubs are enabled.
    /// </summary>
    public static Uri[] EnabledDefaultHubs => _dataManager.GetCVar(SanabiCVars.EnableStockHub) ? _defaultHubsStatic : [];

    public ObservableCollection<HubViewModel> HubList { get; set; } = new();

    public HubSettingsViewModel()
    {
        EnableStockHub = _dataManager.GetCVar(SanabiCVars.EnableStockHub);
    }

    public void Save()
    {
        var hubs = new List<Hub>();

        for (var i = hubs.Count; i < HubList.Count; i++)
        {
            var uri = new Uri(HubList[i].Address, UriKind.Absolute);

            // Automatically add trailing slashes for the user
            if (!uri.AbsoluteUri.EndsWith("/"))
            {
                uri = new Uri(uri.AbsoluteUri + "/", UriKind.Absolute);
            }

            hubs.Add(new Hub(uri, i));
        }

        _dataManager.SetHubs(hubs);

        _dataManager.SetCVar(SanabiCVars.EnableStockHub, EnableStockHub);
        _dataManager.CommitConfig();
    }

    public void Populate()
    {
        HubList.AddRange(_dataManager.Hubs.OrderBy(h => h.Priority)
            .Select(h => new HubViewModel(h.Address.AbsoluteUri, this, true)));
    }

    // Binding; do not rename/remove/change signature
    public void Add()
    {
        HubList.Add(new HubViewModel("", this));
    }

    // Binding; do not rename/remove/change signature
    public void Reset()
    {
        HubList.Clear();
    }

    public List<string> GetDupes()
    {
        return HubList.Select(h => NormalizeHubUri(h.Address)).GroupBy(h => h)
            .Where(group => group.Count() > 1)
            .Select(x => x.Key)
            .ToList();
    }

    public static bool IsValidHubUri(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
               && string.IsNullOrEmpty(uri.Fragment)
               && string.IsNullOrEmpty(uri.Query);
    }

    [return: NotNullIfNotNull(nameof(address))]
    public static string? NormalizeHubUri(string? address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
            return address;

        if (!uri.AbsoluteUri.EndsWith('/'))
        {
            return uri.AbsoluteUri + '/';
        }

        return uri.AbsoluteUri;
    }
}

public class HubViewModel : ViewModelBase
{
    public string Address { get; set; }
    private readonly HubSettingsViewModel _parentVm;
    private bool IsNotDefault { get; }

    public HubViewModel(string address, HubSettingsViewModel parentVm, bool isNotDefault = true)
    {
        Address = address;
        _parentVm = parentVm;
        IsNotDefault = isNotDefault;
    }

    // Binding; do not rename/remove/change signature
    public void Remove()
    {
        _parentVm.HubList.Remove(this);
    }

    // Binding; do not rename/remove/change signature
    public void Up()
    {
        var i = _parentVm.HubList.IndexOf(this);

        if (i == 0)
            return;

        _parentVm.HubList[i] = _parentVm.HubList[i - 1];
        _parentVm.HubList[i - 1] = this;
    }

    // Binding; do not rename/remove/change signature
    public void Down()
    {
        var i = _parentVm.HubList.IndexOf(this);

        if (i == _parentVm.HubList.Count - 1)
            return;

        _parentVm.HubList[i] = _parentVm.HubList[i + 1];
        _parentVm.HubList[i + 1] = this;
    }
}
