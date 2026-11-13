using Avalonia;
using Avalonia.Automation;
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

    private Control CreateStorageSettingsPage()
    {
        var disks = CreateSettingsPageLayout();
        disks.Children.Add(CreateHardfileSettings());
        disks.Children.Add(CreateSharedFolderSettings());
        var tabs = new TabControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Items =
            {
                new TabItem { Header = "Floppy drives", Content = CreateFloppySettingsPage() },
                new TabItem { Header = "Hard disks & folders", Content = CreateScrollableSettingsPage(disks) },
                new TabItem { Header = "Backups", Content = CreateHardDiskBackupSettingsPage() }
            }
        };
        tabs.SelectedIndex = 0;
        AutomationProperties.SetName(tabs, "Storage tabs");
        return tabs;
    }

    private Control CreateHardDiskBackupSettingsPage()
    {
        var page = CreateSettingsPageLayout();
        var layout = CreateSettingsGroupForm();
        layout.Children.Add(SettingsNote("Includes complete hard-disk images and machine settings. Excludes ROMs, floppy images and shared-folder contents."));
        layout.Children.Add(SettingsNote("Stop the Amiga before exporting or restoring. Keep backups outside app data so they survive uninstalling."));
        layout.Children.Add(CreatePanelButton("Export hard-disk backup…", async () => await ExportHardDiskBackupAsync()));
        layout.Children.Add(SettingsNote("Restore creates new disk files and loads the saved machine configuration, including disk write protection. Check the ROM path before starting the Amiga."));
        layout.Children.Add(CreatePanelButton("Restore hard-disk backup…", async () => await RestoreHardDiskBackupAsync()));
        layout.Children.Add(SettingsHelp("Backup details", "Every complete hard-disk image is included, with all RDB partitions and unpartitioned space. Existing files and backups are preserved. CopperOS floppy workspace backups are managed separately from the launcher."));
        _hardDiskBackupStatus = SettingsNote(string.Empty);
        _hardDiskBackupStatus.IsVisible = false;
        layout.Children.Add(_hardDiskBackupStatus);
        page.Children.Add(CreateSettingsGroup("Hard-disk backups", layout));
        return CreateScrollableSettingsPage(page);
    }

    private Control CreateHardfileSettings()
    {
        var layout = new StackPanel { Spacing = 10 };
        layout.Children.Add(SettingsHelp("Hard disk help", "Hard disks attach when the Amiga starts. CopperStart accepts one existing image on unit 0: choose RDB to discover its FFS partitions and boot priorities, or Partition to enter exact geometry. TD64 supports FFS offsets above 4 GiB. Changes require a restart."));
        layout.Children.Add(SettingsNote("Attach an existing image or create a new one. Changes apply when the Amiga restarts."));
        _hardfileRows = new StackPanel { Spacing = 10 };
        layout.Children.Add(_hardfileRows);
        layout.Children.Add(CommandGroup(
            CreatePanelButton("Add existing disk…", async () => await AddExistingHardDiskAsync()),
            CreatePanelButton("Create disk image…", async () => await CreateHardDiskImageAsync())));
        return CreateSettingsGroup("Hard disks", layout);
    }

    private int NextHardDiskUnit()
    {
        var unit = 0;
        while (_settingsDraft.HardDrives.Any(h => h.Unit == unit)) unit++;
        return unit;
    }

    private async Task AddExistingHardDiskAsync()
    {
        try
        {
            var owner = _settingsVisible ? (TopLevel)_settingsWindow : this;
            var selected = await owner.StorageProvider.OpenFilePickerAsync(new()
            {
                Title = "Choose hard disk image", AllowMultiple = false,
                FileTypeFilter = [new("Hard disk images") { Patterns = ["*.hdf", "*.img"] }, FilePickerFileTypes.All]
            });
            if (selected.FirstOrDefault()?.TryGetLocalPath() is not { } path) return;
            ReadHardfileSettings(_settingsDraft);
            _settingsDraft.HardDrives.Add(new(NextHardDiskUnit(), path, true, 0,
                _settingsDraft.KickstartSource == CopperScreenKickstartSource.CopperStart ? AmigaHardfileMountMode.RigidDiskBlock : AmigaHardfileMountMode.Auto));
            RefreshHardfileSettings(); MarkSettingsRestartRequired();
        }
        catch (Exception ex) when (IsWorkspaceFileError(ex) || ex is FormatException or OverflowException) { SetSettingsError(ex.Message); }
    }

    private Task CreateHardDiskImageAsync() => OpenHardDiskSetupAsync(null);

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
            var diskOptions = CreateSettingsGroupForm();
            var unit = AddTextSetting(diskOptions, "Unit"); unit.Text = settings.Unit.ToString(CultureInfo.InvariantCulture);
            var path = AddTextSetting(form, "HDF path"); path.Text = settings.Path;
            bool? sparseEnabled = settings.SparseStorage;
            var sparseForm = CreateSettingsGroupForm();
            var allocation = SettingsNote(string.Empty);
            void RefreshAllocation()
            {
                try
                {
                    var selectedPath = CopperScreenStartupOptions.ResolveOptionalPath(path.Text, AppContext.BaseDirectory);
                    if (selectedPath is null || !File.Exists(selectedPath)) { allocation.Text = "New images use sparse storage where supported."; return; }
                    var allocated = CopperDisk.AmigaSparseFile.GetAllocatedBytes(selectedPath);
                    allocation.Text = $"Virtual capacity: {new FileInfo(selectedPath).Length:N0} bytes. Host allocation: " +
                        (allocated is { } bytes ? $"{bytes:N0} bytes." : "unavailable.") +
                        (sparseEnabled == true ? " Sparse storage enabled; guest TRIM requires host filesystem support." : " Sparse storage not enabled.");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { allocation.Text = "Disk allocation unavailable."; }
            }
            sparseForm.Children.Add(allocation);
            sparseForm.Children.Add(CreatePanelButton("Enable sparse storage", () =>
            {
                try
                {
                    if (_runtime != null || _applyingSettings) throw new InvalidOperationException("Stop the Amiga before enabling sparse storage.");
                    var selectedPath = CopperScreenStartupOptions.ResolveOptionalPath(path.Text, AppContext.BaseDirectory)
                        ?? throw new InvalidOperationException("Choose an existing disk image.");
                    using var file = new FileStream(selectedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    if (!CopperDisk.AmigaSparseFile.TryEnable(file)) throw new IOException("Sparse storage is unavailable on this filesystem. The image can still be used normally.");
                    sparseEnabled = true;
                    file.Dispose();
                    RefreshAllocation(); MarkSettingsRestartRequired();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException) { SetSettingsError(ex.Message); }
            }));
            sparseForm.Children.Add(SettingsNote("Virtual capacity stays fixed. CopperTrim reclaims free guest blocks after you enable writes. Enabling sparse storage alone does not reclaim existing data. Host storage is allocated as data is written and may run out before the virtual disk is full."));
            path.TextChanged += (_, _) => { sparseEnabled = null; RefreshAllocation(); };
            RefreshAllocation();
            var readOnly = new CheckBox { Content = "Read-only", IsChecked = settings.ReadOnly };
            path.TextChanged += (_, _) => { if (path.Text != settings.Path) readOnly.IsChecked = true; };
            form.Children.Add(CreatePanelButton("Browse HDF…", async () =>
            {
                var owner = _settingsVisible ? (TopLevel)_settingsWindow : this;
                var selected = await owner.StorageProvider.OpenFilePickerAsync(new() { Title = "Choose hard disk image", AllowMultiple = false,
                    FileTypeFilter = [new("Hard disk images") { Patterns = ["*.hdf", "*.img"] }, FilePickerFileTypes.All] });
                if (selected.Count == 1) { path.Text = selected[0].TryGetLocalPath(); readOnly.IsChecked = true; }
            }));
            readOnly.IsCheckedChanged += (_, _) => MarkSettingsRestartRequired(); form.Children.Add(readOnly);
            form.Children.Add(SettingsWriteWarning(readOnly, "Writes directly to this file.", $"Hard disk {settings.Unit} write warning"));
            var mode = AddComboSetting(diskOptions, "Image layout", ["Auto", "RDB", "Partition"]); mode.SelectedIndex = (int)settings.Mode;
            var createSizeBytes = settings.CreateSizeBytes;
            var creationNote = SettingsNote($"Blank image: {createSizeBytes / 1048576d:N1} MiB. Created on start if the file does not exist; format it inside the Amiga.");
            form.Children.Add(creationNote);
            var advanced = CreateSettingsGroupForm();
            var partitionForm = CreateSettingsGroupForm();
            diskOptions.Children.Add(SettingsHelp("Image layout help", "RDB reads partition names, geometry and boot priorities from the image. Partition mode uses the fields below. No disk is formatted. Use Create disk image to add a new blank image."));
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
            partitionForm.Children.Add(SettingsHelp("Partition help", "First and last cylinders are inclusive. Heads and blocks per track must match the formatted image. FFS uses 512-byte blocks; its DOS type is $444F5301."));
            diskOptions.Children.Add(CreatePanelButton("Use FFS partition settings", () =>
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
            diskOptions.Children.Add(partitionForm);
            void UpdateLayout()
            {
                var rdb = mode.SelectedIndex == (int)AmigaHardfileMountMode.RigidDiskBlock;
                partitionForm.IsVisible = !rdb;
                if (rdb) createSizeBytes = 0;
                creationNote.IsVisible = createSizeBytes > 0;
            }
            mode.SelectionChanged += (_, _) => { UpdateLayout(); MarkSettingsRestartRequired(); };
            UpdateLayout();
            form.Children.Add(new Expander { Header = "Disk layout and partition settings", Content = diskOptions, HorizontalAlignment = HorizontalAlignment.Stretch });
            form.Children.Add(new Expander { Header = "Host storage allocation", Content = sparseForm, HorizontalAlignment = HorizontalAlignment.Stretch });
            var addPartition = CreatePanelButton("Add partition and format…", async () =>
            {
                try
                {
                    ReadHardfileSettings(_settingsDraft);
                    await OpenHardDiskSetupAsync(index);
                }
                catch (Exception ex) when (IsWorkspaceFileError(ex) || ex is FormatException or OverflowException) { SetSettingsError(ex.Message); }
            });
            form.Children.Add(addPartition);
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
                var bytes = createSizeBytes;
                if (bytes < 0 || bytes % 512 != 0) throw new InvalidOperationException("Hard disk creation size must be a nonnegative multiple of 512 bytes.");
                if (mode.SelectedIndex == (int)AmigaHardfileMountMode.RigidDiskBlock)
                    return new(number, path.Text.Trim(), readOnly.IsChecked == true, 0, AmigaHardfileMountMode.RigidDiskBlock, SparseStorage: sparseEnabled);
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
                    (AmigaHardfileMountMode)mode.SelectedIndex, any ? metadata : null,
                    sparseEnabled ?? (bytes > 0 && !File.Exists(CopperScreenStartupOptions.ResolveOptionalPath(path.Text, AppContext.BaseDirectory))));
            });
            var summary = SettingsNote(string.Empty);
            void UpdateSummary() => summary.Text = $"Unit {unit.Text} · {System.IO.Path.GetFileName(path.Text)} · " + (readOnly.IsChecked == true ? "Read-only" : "Writable");
            UpdateSummary();
            path.TextChanged += (_, _) => UpdateSummary();
            unit.TextChanged += (_, _) => UpdateSummary();
            readOnly.IsCheckedChanged += (_, _) => UpdateSummary();
            _hardfileRows.Children.Add(new Expander { Header = summary, Content = form, IsExpanded = true, HorizontalAlignment = HorizontalAlignment.Stretch });
        }
    }
}
