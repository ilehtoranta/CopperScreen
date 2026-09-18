using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using System.Globalization;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private StackPanel _hardfileRows = null!;
    private readonly List<Func<CopperScreenHardfileSettings>> _hardfileReaders = [];

    private Control CreateHardfileSettings()
    {
        var layout = new StackPanel { Spacing = 10 };
        layout.Children.Add(SettingsNote("CopperHDF hard disks attach when the Amiga starts. Writable images save directly to their backing file. Changes here require a restart. Use OFS with Kickstart 1.3 unless your RDB supplies another filesystem."));
        _hardfileRows = new StackPanel { Spacing = 10 };
        layout.Children.Add(_hardfileRows);
        layout.Children.Add(CreatePanelButton("Add hard disk", () =>
        {
            try
            {
                ReadHardfileSettings(_settingsDraft);
                var unit = 0;
                while (_settingsDraft.HardDrives.Any(h => h.Unit == unit)) unit++;
                _settingsDraft.HardDrives.Add(new(unit, "", true, 0));
                RefreshHardfileSettings(); MarkSettingsRestartRequired();
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException or InvalidOperationException) { SetSettingsError(ex.Message); }
        }));
        return CreateSettingsGroup("Hard disks", layout);
    }

    private void ReadHardfileSettings(CopperScreenSettingsDraft draft)
    {
        var values = _hardfileReaders.Select(read => read()).ToArray();
        if (values.Select(v => v.Unit).Distinct().Count() != values.Length) throw new InvalidOperationException("Each hard disk needs a different unit number.");
        draft.HardDrives.Clear(); draft.HardDrives.AddRange(values);
    }

    private void RefreshHardfileSettings()
    {
        if (_hardfileRows is null) return;
        _hardfileRows.Children.Clear(); _hardfileReaders.Clear();
        foreach (var (settings, index) in _settingsDraft.HardDrives.Select((h, i) => (h, i)))
        {
            var form = CreateSettingsGroupForm();
            var unit = AddTextSetting(form, "Unit"); unit.Text = settings.Unit.ToString(CultureInfo.InvariantCulture);
            var path = AddTextSetting(form, "HDF path"); path.Text = settings.Path;
            form.Children.Add(CreatePanelButton("Browse HDF…", async () =>
            {
                var selected = await StorageProvider.OpenFilePickerAsync(new() { Title = "Choose hard disk image", AllowMultiple = false,
                    FileTypeFilter = [new("Hard disk images") { Patterns = ["*.hdf", "*.img"] }, FilePickerFileTypes.All] });
                if (selected.Count == 1) path.Text = selected[0].TryGetLocalPath();
            }));
            var readOnly = new CheckBox { Content = "Read-only", IsChecked = settings.ReadOnly };
            readOnly.IsCheckedChanged += (_, _) => MarkSettingsRestartRequired(); form.Children.Add(readOnly);
            var mode = AddComboSetting(form, "Image layout", ["Auto", "RigidDiskBlock", "Partition"]); mode.SelectedItem = settings.Mode.ToString();
            var size = AddTextSetting(form, "Create size (bytes)"); size.Text = settings.CreateSizeBytes.ToString(CultureInfo.InvariantCulture);
            form.Children.Add(SettingsNote("Use 0 for an existing image. Creation only applies to a missing file; the size must be a multiple of 512 bytes."));
            var advanced = CreateSettingsGroupForm();
            var partition = settings.Partition ?? new AmigaHardfilePartitionMetadata();
            var fields = new Dictionary<string, TextBox>();
            // The advanced fields correspond directly to the saved profile's
            // environment vector. Preserve all fields when editing path/protection.
            string[] labels = ["Device name", "Environment last index", "Block size (longs)", "Sector origin", "Heads", "Sectors per block", "Blocks per track", "Reserved blocks", "Preallocated blocks", "Interleave", "First cylinder", "Last cylinder", "Buffers", "Buffer memory flags", "Maximum transfer (bytes)", "Address mask", "Boot priority", "DOS type"];
            string[] names = ["DeviceName", "TableSize", "SizeBlockLongs", "SectorOrigin", "Surfaces", "SectorsPerBlock", "BlocksPerTrack", "ReservedBlocks", "PreAllocBlocks", "Interleave", "LowCylinder", "HighCylinder", "NumBuffers", "BufferMemoryType", "MaxTransfer", "Mask", "BootPriority", "DosType"];
            var properties = names.Select(name => typeof(AmigaHardfilePartitionMetadata).GetProperty(name)!).ToArray();
            for (var i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                var box = AddTextSetting(advanced, labels[i]);
                box.Text = Convert.ToString(property.GetValue(partition), CultureInfo.InvariantCulture);
                box.PlaceholderText = "Default"; fields.Add(property.Name, box);
            }
            form.Children.Add(new Expander { Header = "Partition metadata", Content = advanced, HorizontalAlignment = HorizontalAlignment.Stretch });
            form.Children.Add(CreatePanelButton("Remove hard disk", () =>
            {
                // Removal also works for an incomplete row.
                var removed = _hardfileReaders[index];
                _hardfileReaders.RemoveAt(index);
                try { ReadHardfileSettings(_settingsDraft); }
                catch (Exception ex) when (ex is FormatException or OverflowException or InvalidOperationException) { _hardfileReaders.Insert(index, removed); SetSettingsError(ex.Message); return; }
                RefreshHardfileSettings(); MarkSettingsRestartRequired();
            }));
            _hardfileReaders.Add(() =>
            {
                if (!int.TryParse(unit.Text, out var number) || number < 0) throw new InvalidOperationException("Hard disk unit must be a nonnegative integer.");
                if (string.IsNullOrWhiteSpace(path.Text)) throw new InvalidOperationException($"Choose a backing file for hard disk unit {number}.");
                if (!long.TryParse(size.Text, out var bytes) || bytes < 0 || bytes % 512 != 0) throw new InvalidOperationException("Hard disk creation size must be a nonnegative multiple of 512 bytes.");
                var metadata = new AmigaHardfilePartitionMetadata(); var any = false;
                foreach (var property in properties)
                {
                    var text = fields[property.Name].Text?.Trim(); if (string.IsNullOrEmpty(text)) continue;
                    any = true;
                    object value;
                    if (property.PropertyType == typeof(string)) value = text;
                    else if (property.PropertyType == typeof(int?)) value = int.Parse(text, CultureInfo.InvariantCulture);
                    else value = text.StartsWith('$') ? uint.Parse(text[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                        : text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? uint.Parse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                        : uint.Parse(text, CultureInfo.InvariantCulture);
                    property.SetValue(metadata, value);
                }
                return new(number, path.Text.Trim(), readOnly.IsChecked == true, bytes,
                    Enum.Parse<AmigaHardfileMountMode>((string)mode.SelectedItem!), any ? metadata : null);
            });
            _hardfileRows.Children.Add(new Expander { Header = $"Unit {settings.Unit}: {System.IO.Path.GetFileName(settings.Path)}", Content = form, IsExpanded = true, HorizontalAlignment = HorizontalAlignment.Stretch });
        }
    }
}
