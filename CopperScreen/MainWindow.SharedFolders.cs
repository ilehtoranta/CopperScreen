using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using CopperMod.Amiga.Lightweight;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private StackPanel _sharedFolderRows = null!;
    private readonly List<Func<LightweightHostDirectoryConfiguration>> _sharedFolderReaders = [];
    private Control CreateSharedFolderSettings()
    {
        var layout = new StackPanel { Spacing = 10 };
        layout.Children.Add(SettingsNote("Shared folders appear as Amiga drives. Writable mounts save files directly into the selected folder. Bootable folders require your own Workbench files. Changes require a restart."));
        _sharedFolderRows = new StackPanel { Spacing = 10 }; layout.Children.Add(_sharedFolderRows);
        layout.Children.Add(CreatePanelButton("Add shared folder", () =>
        {
            try
            {
                ReadSharedFolderSettings(_settingsDraft);
                var index = 0; while (_settingsDraft.HostDirectories.Any(m => m.DeviceName.Equals($"HOST{index}", StringComparison.OrdinalIgnoreCase))) index++;
                _settingsDraft.HostDirectories.Add(new($"HOST{index}", $"Host{index}", "")); RefreshSharedFolderSettings(); MarkSettingsRestartRequired();
            }
            catch (ArgumentException ex) { SetSettingsError(ex.Message); }
        }));
        return CreateSettingsGroup("Shared folders", layout);
    }
    private void ReadSharedFolderSettings(CopperScreenSettingsDraft draft)
    {
        var values = _sharedFolderReaders.Select(r => r()).ToArray();
        if (values.Select(v => v.DeviceName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Length ||
            values.Select(v => v.VolumeName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Length)
            throw new ArgumentException("Shared folders require unique device and volume names.");
        draft.HostDirectories.Clear(); draft.HostDirectories.AddRange(values);
    }
    private void RefreshSharedFolderSettings()
    {
        if (_sharedFolderRows is null) return;
        _sharedFolderReaders.Clear(); _sharedFolderRows.Children.Clear();
        foreach (var (mount, index) in _settingsDraft.HostDirectories.Select((m, i) => (m, i)))
        {
            var form = CreateSettingsGroupForm();
            var device = AddTextSetting(form, "Device name"); device.Text = mount.DeviceName;
            var volume = AddTextSetting(form, "Volume label"); volume.Text = mount.VolumeName;
            var path = AddTextSetting(form, "Host folder"); path.Text = mount.Path;
            var readOnly = new CheckBox { Content = "Read-only (clear to allow direct changes in this folder)", IsChecked = mount.ReadOnly };
            path.TextChanged += (_, _) => { if (path.Text != mount.Path) readOnly.IsChecked = true; };
            form.Children.Add(CreatePanelButton("Choose folder…", async () =>
            {
                var selected = await StorageProvider.OpenFolderPickerAsync(new() { Title = "Choose shared folder", AllowMultiple = false });
                if (selected.Count == 1) { path.Text = selected[0].TryGetLocalPath(); readOnly.IsChecked = true; }
            }));
            form.Children.Add(readOnly);
            var boot = new CheckBox { Content = "Boot from this folder", IsChecked = mount.BootPriority.HasValue }; form.Children.Add(boot);
            var priority = AddTextSetting(form, "Boot priority"); priority.Text = (mount.BootPriority ?? 0).ToString(); priority.IsEnabled = boot.IsChecked == true;
            boot.IsCheckedChanged += (_, _) => { priority.IsEnabled = boot.IsChecked == true; MarkSettingsRestartRequired(); };
            readOnly.IsCheckedChanged += (_, _) => MarkSettingsRestartRequired();
            form.Children.Add(CreatePanelButton("Remove shared folder", () =>
            {
                var reader = _sharedFolderReaders[index]; _sharedFolderReaders.RemoveAt(index);
                try { ReadSharedFolderSettings(_settingsDraft); }
                catch (ArgumentException ex) { _sharedFolderReaders.Insert(index, reader); SetSettingsError(ex.Message); return; }
                RefreshSharedFolderSettings(); MarkSettingsRestartRequired();
            }));
            _sharedFolderReaders.Add(() =>
            {
                int? bootPriority = null;
                if (boot.IsChecked == true)
                { if (!int.TryParse(priority.Text, out var value) || value is < -127 or > 127) throw new ArgumentException("Boot priority must be between -127 and 127."); bootPriority = value; }
                var result = new LightweightHostDirectoryConfiguration(device.Text?.Trim() ?? "", volume.Text?.Trim() ?? "", path.Text?.Trim() ?? "", readOnly.IsChecked == true, bootPriority);
                result.Validate(); return result;
            });
            _sharedFolderRows.Children.Add(new Expander { Header = $"{mount.DeviceName}: {mount.VolumeName}", Content = form, IsExpanded = true, HorizontalAlignment = HorizontalAlignment.Stretch });
        }
    }
}
