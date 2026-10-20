using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using System.Globalization;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private StackPanel _hardfileRows = null!;
    private readonly List<Func<CopperScreenHardfileSettings>> _hardfileReaders = [];
    private TextBlock _hardDiskBackupStatus = null!;

    private Control CreateHardfileSettings()
    {
        var layout = new StackPanel { Spacing = 10 };
        layout.Children.Add(SettingsNote("Hard disks attach when the Amiga starts. Writable images save directly to their backing file. CopperStart accepts one existing image on unit 0: choose RDB to discover its FFS partitions and boot priorities, or Partition to enter exact geometry. TD64 supports FFS offsets above 4 GiB. Changes require a restart."));
        layout.Children.Add(SettingsNote("Stop the Amiga before backup or restore. A backup includes every complete hard-disk image, all RDB partitions and the machine settings. Firmware, floppy images and shared-folder contents stay at their original paths and are not included. Restore creates new working disks and preserves their saved write protection."));
        layout.Children.Add(CommandGroup(CreatePanelButton("Export hard-disk backup…", async () => await ExportHardDiskBackupAsync()),
            CreatePanelButton("Restore hard-disk backup…", async () => await RestoreHardDiskBackupAsync())));
        _hardDiskBackupStatus = SettingsNote("Backups never replace an existing file. Keep the exported backup outside app data.");
        layout.Children.Add(_hardDiskBackupStatus);
        _hardfileRows = new StackPanel { Spacing = 10 };
        layout.Children.Add(_hardfileRows);
        layout.Children.Add(CreatePanelButton("Add hard disk", () =>
        {
            try
            {
                ReadHardfileSettings(_settingsDraft);
                var unit = 0;
                while (_settingsDraft.HardDrives.Any(h => h.Unit == unit)) unit++;
                _settingsDraft.HardDrives.Add(_settingsDraft.KickstartSource == CopperScreenKickstartSource.CopperStart
                    ? new(unit, "", true, 0, AmigaHardfileMountMode.RigidDiskBlock)
                    : new(unit, "", true, 0));
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
            var readOnly = new CheckBox { Content = "Read-only (clear to allow direct writes to this file)", IsChecked = settings.ReadOnly };
            path.TextChanged += (_, _) => { if (path.Text != settings.Path) readOnly.IsChecked = true; };
            form.Children.Add(CreatePanelButton("Browse HDF…", async () =>
            {
                var selected = await StorageProvider.OpenFilePickerAsync(new() { Title = "Choose hard disk image", AllowMultiple = false,
                    FileTypeFilter = [new("Hard disk images") { Patterns = ["*.hdf", "*.img"] }, FilePickerFileTypes.All] });
                if (selected.Count == 1) { path.Text = selected[0].TryGetLocalPath(); readOnly.IsChecked = true; }
            }));
            readOnly.IsCheckedChanged += (_, _) => MarkSettingsRestartRequired(); form.Children.Add(readOnly);
            var mode = AddComboSetting(form, "Image layout", ["Auto", "RDB", "Partition"]); mode.SelectedIndex = (int)settings.Mode;
            var size = AddTextSetting(form, "Create size (bytes)"); size.Text = settings.CreateSizeBytes.ToString(CultureInfo.InvariantCulture);
            form.Children.Add(SettingsNote("Use 0 for an existing image. Creation only applies to a missing file; the size must be a multiple of 512 bytes."));
            var advanced = CreateSettingsGroupForm();
            var partitionForm = CreateSettingsGroupForm();
            form.Children.Add(SettingsNote("RDB reads partition names, geometry and boot priorities from the image. Partition mode uses the fields below. No disk is formatted."));
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
                var box = AddTextSetting(names[i] is "DeviceName" or "Surfaces" or "BlocksPerTrack" or "LowCylinder" or "HighCylinder" ? partitionForm : advanced, labels[i]);
                box.Text = Convert.ToString(property.GetValue(partition), CultureInfo.InvariantCulture);
                box.PlaceholderText = names[i] is "Surfaces" or "BlocksPerTrack" or "LowCylinder" or "HighCylinder"
                    ? "From the image's partition definition" : "Default";
                fields.Add(property.Name, box);
            }
            partitionForm.Children.Add(SettingsNote("First and last cylinders are inclusive. Heads and blocks per track must match the formatted image. FFS uses 512-byte blocks; its DOS type is $444F5301."));
            form.Children.Add(CreatePanelButton("Use FFS partition settings", () =>
            {
                mode.SelectedItem = "Partition";
                fields["DosType"].Text = "$444F5301";
                fields["SizeBlockLongs"].Text = "128";
                fields["SectorsPerBlock"].Text = "1";
                fields["ReservedBlocks"].Text = "2";
                fields["MaxTransfer"].Text = "$0001FE00";
                fields["Mask"].Text = "$FFFFFFFE";
                if (string.IsNullOrWhiteSpace(fields["DeviceName"].Text)) fields["DeviceName"].Text = $"DH{settings.Unit}";
                MarkSettingsRestartRequired();
            }));
            partitionForm.Children.Add(new Expander { Header = "Partition metadata", Content = advanced, HorizontalAlignment = HorizontalAlignment.Stretch });
            form.Children.Add(partitionForm);
            void UpdateLayout()
            {
                var rdb = mode.SelectedIndex == (int)AmigaHardfileMountMode.RigidDiskBlock;
                partitionForm.IsVisible = !rdb;
                size.IsEnabled = !rdb;
                if (rdb) size.Text = "0";
            }
            mode.SelectionChanged += (_, _) => { UpdateLayout(); MarkSettingsRestartRequired(); };
            UpdateLayout();
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
                if (mode.SelectedIndex == (int)AmigaHardfileMountMode.RigidDiskBlock)
                    return new(number, path.Text.Trim(), readOnly.IsChecked == true, 0, AmigaHardfileMountMode.RigidDiskBlock);
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
                    (AmigaHardfileMountMode)mode.SelectedIndex, any ? metadata : null);
            });
            _hardfileRows.Children.Add(new Expander { Header = $"Unit {settings.Unit}: {System.IO.Path.GetFileName(settings.Path)}", Content = form, IsExpanded = true, HorizontalAlignment = HorizontalAlignment.Stretch });
        }
    }
}
