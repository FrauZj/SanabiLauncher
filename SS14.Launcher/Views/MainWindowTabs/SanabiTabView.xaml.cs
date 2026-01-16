using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SS14.Launcher.Views.MainWindowTabs;

public partial class SanabiTabView : UserControl
{
    public SanabiTabView()
    {
        InitializeComponent();
        UlongBox.AddHandler(TextInputEvent, OnSeedTextInput, RoutingStrategies.Tunnel);
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
}
