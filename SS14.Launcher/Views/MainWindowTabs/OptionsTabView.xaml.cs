using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using SS14.Launcher.Utility;
using SS14.Launcher.ViewModels.MainWindowTabs;

namespace SS14.Launcher.Views.MainWindowTabs;

public partial class OptionsTabView : UserControl
{
    public OptionsTabView()
    {
        InitializeComponent();
        UlongBox.AddHandler(TextInputEvent, OnSeedTextInput, RoutingStrategies.Tunnel);

        Flip.Command = ReactiveCommand.Create(() =>
        {
            var window = (Window?)VisualRoot;
            if (window == null)
                return;

            window.Classes.Add("DoAFlip");

            DispatcherTimer.RunOnce(() => { window.Classes.Remove("DoAFlip"); }, TimeSpan.FromSeconds(1));
        });
    }

    // Vibecoded. there is some bug here that throws under some conditions but i forgot what and doesnt really seem to be often at all
    // This validates if input can be parsed to a ulong
    public void OnSeedTextInput(object? sender, TextInputEventArgs e)
    {
        var tb = (TextBox)sender!;

        // Selection info in Avalonia
        var selStart = tb.SelectionStart;
        var selEnd = tb.SelectionEnd;
        var selLen = selEnd - selStart;

        // Build the predicted new text
        var current = tb.Text ?? string.Empty;

        var newText =
            current.Remove(selStart, selLen)
                   .Insert(selStart, e.Text ?? string.Empty);

        // Allow empty text
        if (string.IsNullOrWhiteSpace(newText))
            return;

        // Check if ulong-compatible
        if (!ulong.TryParse(newText, out _))
            e.Handled = true; // BLOCK input
    }

    public async void ClearEnginesPressed(object? _1, RoutedEventArgs _2)
    {
        ((OptionsTabViewModel)DataContext!).ClearEngines();
        await ClearEnginesButton.DisplayDoneMessage();
    }

    public async void ClearServerContentPressed(object? _1, RoutedEventArgs _2)
    {
        ((OptionsTabViewModel)DataContext!).ClearServerContent();
        await ClearServerContentButton.DisplayDoneMessage();
    }

    private async void OpenHubSettings(object? sender, RoutedEventArgs args)
    {
        await new HubSettingsDialog().ShowDialog((Window)this.GetVisualRoot()!);
    }
}
