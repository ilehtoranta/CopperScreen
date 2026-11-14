using Avalonia.Controls;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private DiskImageManagerWindow? _diskImageManager;

    private async Task ShowDiskImageManagerAsync()
    {
        if (_diskImageManager != null) { _diskImageManager.Activate(); return; }
        if (_runtime != null || _applyingSettings)
        {
            var owner = _settingsVisible ? (Window)_settingsWindow : this;
            var message = new Window
            {
                Title = "Disk Image Manager", Width = 460, SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(20), Spacing = 14,
                    Children = { SettingsNote("Stop the Amiga before opening the Disk Image Manager. It reads saved disk images; unsaved floppy changes must be saved separately.") }
                }
            };
            ((StackPanel)message.Content).Children.Add(CreatePanelButton("Close", () => message.Close()));
            await message.ShowDialog(owner);
            return;
        }
        ReleaseInteractiveInput();
        _diskImageManager = new DiskImageManagerWindow();
        try { await _diskImageManager.ShowDialog(_settingsVisible ? (Window)_settingsWindow : this); }
        finally { _diskImageManager = null; }
    }
}
