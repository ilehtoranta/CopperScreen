using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using CopperDisk;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private void RequireStoppedForHardDiskSetup()
    {
        if (_runtime != null) throw new InvalidOperationException("Stop the Amiga before creating or partitioning hard disks.");
        if (_applyingSettings) throw new InvalidOperationException("Wait for the current operation to finish.");
    }

    private TextBox AddDiskSizeInput(StackPanel form, string label, string value, out ComboBox unit)
    {
        var size = new TextBox { Text = value, MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        unit = new ComboBox { ItemsSource = new[] { "MiB", "GiB" }, SelectedIndex = 0, MinWidth = 76 };
        AutomationProperties.SetName(size, label);
        AutomationProperties.SetName(unit, label + " unit");
        form.Children.Add(CreateSettingsRow(label, WithAction(size, unit)));
        return size;
    }

    private static string DiskSizeText(long bytes) => bytes >= 1073741824L
        ? $"{bytes / 1073741824m:0.#####} GiB" : $"{bytes / 1048576m:0.#####} MiB";

    private async Task OpenHardDiskSetupAsync(int? diskIndex)
    {
        try
        {
            RequireStoppedForHardDiskSetup();
            var existing = diskIndex is { } index ? _settingsDraft.HardDrives[index] : null;
            var source = existing == null ? null : CopperScreenStartupOptions.ResolveOptionalPath(existing.Path, AppContext.BaseDirectory);
            if (existing != null && existing.Mode != AmigaHardfileMountMode.RigidDiskBlock)
                throw new InvalidOperationException("Choose RDB layout before adding a partition. Raw partition images cannot be repartitioned here.");
            var layout = source == null ? null : CopperScreenHardDiskSetup.Inspect(source);
            var free = layout == null ? null : CopperScreenHardDiskSetup.FirstFreeRange(layout);
            if (layout != null && free == null) throw new InvalidOperationException("This disk has no free space. Existing partitions are preserved.");
            var sourceCapacity = source == null ? 0 : new FileInfo(source).Length;
            var owner = _settingsVisible ? (Window)_settingsWindow : this;
            var dialog = new Window
            {
                Title = existing == null ? "Create disk image" : "Add partition and format",
                Width = 580, MaxHeight = 800, SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            var form = CreateSettingsGroupForm(); form.Margin = new Thickness(20);
            var explanation = SettingsNote(existing == null
                ? "Create a new disk with one formatted partition. It starts read-only; enable writes in disk settings when ready."
                : "Save a new copy with an additional partition, then attach that copy. The original disk and existing partitions are preserved.");
            form.Children.Add(explanation);
            if (layout != null)
                foreach (var p in layout.Partitions)
                    form.Children.Add(SettingsNote($"{p.DeviceName}: {DiskSizeText(checked(((long)p.LastCylinder - p.FirstCylinder + 1) * layout.Heads * layout.SectorsPerTrack * 512))}, {p.BlockSize}-byte blocks."));
            var path = AddTextSetting(form, "New image file", markRestart: false);
            form.Children.Add(CreatePanelButton("Choose destination…", async () =>
            {
                var file = await dialog.StorageProvider.SaveFilePickerAsync(new()
                {
                    Title = "New hard disk image", SuggestedFileName = "Amiga.hdf", DefaultExtension = "hdf",
                    ShowOverwritePrompt = false, FileTypeChoices = [new("Hard disk image") { Patterns = ["*.hdf"] }]
                });
                if (file?.TryGetLocalPath() is { } selected) path.Text = selected;
            }));
            var size = AddDiskSizeInput(form, "Disk capacity", source == null ? "100" : (sourceCapacity / 1048576m).ToString(CultureInfo.InvariantCulture), out var sizeUnit);
            size.IsEnabled = sizeUnit.IsEnabled = existing == null;
            var fs = AddComboSetting(form, "Filesystem", existing == null
                ? ["Unformatted", .. CopperScreenHardDiskSetup.FileSystemNames] : CopperScreenHardDiskSetup.FileSystemNames, markRestart: false);
            fs.SelectedIndex = existing == null ? 1 : 0;
            bool IsBlank() => existing == null && fs.SelectedIndex == 0;
            int FileSystemIndex() => fs.SelectedIndex - (existing == null ? 1 : 0);
            var partition = CreateSettingsGroupForm(); form.Children.Add(partition);
            var device = AddTextSetting(partition, "New partition device", markRestart: false);
            var deviceNumber = 0;
            while (layout?.Partitions.Any(p => string.Equals(p.DeviceName, $"DH{deviceNumber}", StringComparison.OrdinalIgnoreCase)) == true) deviceNumber++;
            device.Text = $"DH{deviceNumber}";
            var fill = new CheckBox { Content = "Use available space", IsChecked = true }; partition.Children.Add(fill);
            var partitionSize = AddDiskSizeInput(partition, "Partition size", "8", out var partitionUnit);
            var blockSize = AddComboSetting(partition, "Filesystem block size", ["512 bytes", "1 KiB", "2 KiB", "4 KiB"], markRestart: false);
            blockSize.SelectedIndex = 0;
            partition.Children.Add(SettingsNote("Filesystem blocks are independent of the disk's 512-byte sectors. The guest filesystem handler must support the chosen block size."));
            var label = AddTextSetting(partition, "Volume label", markRestart: false); label.Text = "Work";
            var bootable = new CheckBox { Content = "Bootable partition", IsChecked = false }; partition.Children.Add(bootable);
            partition.Children.Add(SettingsHelp("Filesystem details", "FFS uses DOS1; SFS and SFS2 require a compatible guest ROM or handler. CopperNova (CNFS) is experimental and requires a compatible CNFS handler. Formatting does not install a handler or boot files."));
            var experimental = new CheckBox { Content = "I want to create an experimental CopperNova (CNFS) volume", IsVisible = false }; partition.Children.Add(experimental);
            var preview = SettingsNote(string.Empty); AutomationProperties.SetName(preview, "Partition size preview"); partition.Children.Add(preview);
            partition.Children.Add(SettingsNote("A partition uses one contiguous free area. The preview shows the usable size after alignment and disk metadata."));
            var advanced = CreateSettingsGroupForm();
            var geometry = SettingsNote(string.Empty); advanced.Children.Add(geometry);
            var explicitRange = new CheckBox { Content = "Use explicit cylinder range" }; advanced.Children.Add(explicitRange);
            var first = AddTextSetting(advanced, "First cylinder", markRestart: false); first.Text = free?.First.ToString(CultureInfo.InvariantCulture) ?? "1";
            var last = AddTextSetting(advanced, "Last cylinder", markRestart: false); last.Text = free?.Last.ToString(CultureInfo.InvariantCulture) ?? "";
            advanced.Children.Add(SettingsNote("Normal sizes are rounded down to whole cylinders and filesystem blocks. Automatic placement uses a contiguous free area; the explicit range overrides the partition size."));
            partition.Children.Add(new Expander { Header = "Advanced geometry", Content = advanced, HorizontalAlignment = HorizontalAlignment.Stretch });
            CopperScreenHardDiskSetup.PartitionRange? currentRange = null;
            (long Capacity, AmigaRdbSetupLayout Layout, CopperScreenHardDiskSetup.PartitionRange Range, uint BlockSize) ReadPartition()
            {
                var capacity = source == null ? CopperScreenHardDiskSetup.AlignNewCapacity(CopperScreenHardDiskSetup.ParseSize(size.Text, sizeUnit.SelectedItem?.ToString() ?? "")) : sourceCapacity;
                var diskLayout = layout ?? CopperScreenHardDiskSetup.NewLayout(capacity);
                if ((uint)blockSize.SelectedIndex >= CopperScreenHardDiskSetup.BlockSizes.Length) throw new ArgumentException("Choose a filesystem block size.");
                var blocks = CopperScreenHardDiskSetup.BlockSizes[blockSize.SelectedIndex];
                uint? low = null, high = null;
                if (explicitRange.IsChecked == true)
                {
                    if (!uint.TryParse(first.Text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var f) ||
                        !uint.TryParse(last.Text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var l))
                        throw new ArgumentException("Enter whole first and last cylinder numbers.");
                    low = f; high = l;
                }
                var requested = explicitRange.IsChecked == true || fill.IsChecked == true ? (long?)null
                    : CopperScreenHardDiskSetup.ParseSize(partitionSize.Text, partitionUnit.SelectedItem?.ToString() ?? "");
                return (capacity, diskLayout, CopperScreenHardDiskSetup.PlanPartition(diskLayout, requested, blocks, low, high), blocks);
            }
            void RefreshPreview()
            {
                var manual = explicitRange.IsChecked == true;
                first.IsEnabled = last.IsEnabled = manual;
                fill.IsEnabled = !manual;
                partitionSize.IsEnabled = partitionUnit.IsEnabled = !manual && fill.IsChecked != true;
                if (IsBlank()) { currentRange = null; return; }
                try
                {
                    var planned = ReadPartition(); currentRange = planned.Range;
                    if (manual || fill.IsChecked == true)
                        partitionSize.Text = (planned.Range.Bytes / (partitionUnit.SelectedItem is "GiB" ? 1073741824m : 1048576m)).ToString(CultureInfo.InvariantCulture);
                    var cylinderBytes = checked((long)planned.Layout.Heads * planned.Layout.SectorsPerTrack * 512);
                    geometry.Text = $"512-byte device sectors; {planned.Layout.Heads} heads × {planned.Layout.SectorsPerTrack} sectors/track. Cylinder: {cylinderBytes:N0} bytes. Usable cylinders {planned.Layout.FirstCylinder}–{planned.Layout.LastCylinder}.";
                    var usableBytes = checked(((long)planned.Layout.LastCylinder - planned.Layout.FirstCylinder + 1) * cylinderBytes);
                    var occupiedBytes = planned.Layout.Partitions.Sum(p => checked(((long)p.LastCylinder - p.FirstCylinder + 1) * cylinderBytes));
                    preview.Text = $"Disk: {DiskSizeText(planned.Capacity)}. Partition: {DiskSizeText(planned.Range.Bytes)} ({planned.Range.Bytes:N0} bytes). Free space after creation: {DiskSizeText(usableBytes - occupiedBytes - planned.Range.Bytes)}.";
                }
                catch (Exception ex) when (ex is ArgumentException or OverflowException) { currentRange = null; preview.Text = ex.Message; }
            }
            var status = SettingsNote(string.Empty); form.Children.Add(status);
            using var cancellation = new CancellationTokenSource();
            var busy = false;
            var cancel = CreatePanelButton("Cancel", () => { if (busy) cancellation.Cancel(); else dialog.Close(); });
            var save = CreatePanelButton(existing == null ? "Create image" : "Save partitioned copy", async () =>
            {
                if (busy) return;
                var close = false;
                try
                {
                    RequireStoppedForHardDiskSetup();
                    if (!IsBlank() && FileSystemIndex() == 3 && experimental.IsChecked != true)
                        throw new InvalidOperationException("Confirm that you want an experimental CopperNova (CNFS) volume.");
                    var destination = CopperScreenStartupOptions.ResolveOptionalPath(path.Text, AppContext.BaseDirectory)
                        ?? throw new InvalidOperationException("Choose a new destination filename.");
                    if (IsBlank())
                    {
                        if (_settingsDraft.KickstartSource == CopperScreenKickstartSource.CopperStart)
                            throw new InvalidOperationException("CopperStart needs an existing disk image. Choose a filesystem to create a formatted image instead.");
                        var bytes = CopperScreenHardDiskSetup.ParseSize(size.Text, sizeUnit.SelectedItem?.ToString() ?? "");
                        if (bytes < 1048576) throw new ArgumentException("Unformatted images need at least 1 MiB.");
                        if (Path.Exists(destination)) throw new IOException("Choose a new filename. Existing files are preserved.");
                        ReadHardfileSettings(_settingsDraft);
                        _settingsDraft.HardDrives.Add(new(NextHardDiskUnit(), destination, true, bytes, SparseStorage: true));
                        RefreshHardfileSettings(); ClearSettingsStartupError(); MarkSettingsRestartRequired();
                        close = true; return;
                    }
                    var planned = ReadPartition();
                    ReadHardfileSettings(_settingsDraft);
                    var name = device.Text ?? ""; var volume = label.Text ?? "";
                    var fileSystem = (CopperScreenHardDiskSetup.FileSystem)FileSystemIndex(); var boot = bootable.IsChecked == true;
                    busy = true; _applyingSettings = true; UpdateSettingsStatus(); UpdateToolbarStatus();
                    status.Text = "Preparing and verifying disk… Cancel leaves the original disk unchanged.";
                    await Task.Run(() => CopperScreenHardDiskSetup.Create(destination, planned.Capacity, source, name,
                        planned.Range.First, planned.Range.Last, fileSystem, volume, boot, cancellation.Token, planned.BlockSize));
                    var attached = new CopperScreenHardfileSettings(existing?.Unit ?? NextHardDiskUnit(), destination, existing?.ReadOnly ?? true,
                        0, AmigaHardfileMountMode.RigidDiskBlock, SparseStorage: true);
                    if (diskIndex is { } row) _settingsDraft.HardDrives[row] = attached; else _settingsDraft.HardDrives.Add(attached);
                    RefreshHardfileSettings(); ClearSettingsStartupError(); MarkSettingsRestartRequired(); close = true;
                }
                catch (OperationCanceledException) { status.Text = "Disk setup cancelled. No new disk was attached."; close = true; }
                catch (Exception ex) when (IsWorkspaceFileError(ex) || ex is FormatException or OverflowException) { status.Text = ex.Message; }
                finally
                {
                    if (busy) { busy = false; _applyingSettings = false; UpdateSettingsStatus(); UpdateToolbarStatus(); }
                    if (close) dialog.Close();
                }
            });
            fs.SelectionChanged += (_, _) =>
            {
                partition.IsVisible = !IsBlank();
                experimental.IsVisible = !IsBlank() && FileSystemIndex() == 3; experimental.IsChecked = false;
                status.Text = string.Empty;
                if (existing == null) explanation.Text = IsBlank()
                    ? "Add an unformatted image to this machine. The file is created when the Amiga starts; format it inside the Amiga before use."
                    : "Create a new disk with one formatted partition. It starts read-only; enable writes in disk settings when ready.";
                if (existing == null) save.Content = IsBlank() ? "Add blank image" : "Create image";
                RefreshPreview();
            };
            explicitRange.IsCheckedChanged += (_, _) =>
            {
                if (explicitRange.IsChecked == true && currentRange is { } range)
                { first.Text = range.First.ToString(CultureInfo.InvariantCulture); last.Text = range.Last.ToString(CultureInfo.InvariantCulture); }
                RefreshPreview();
            };
            fill.IsCheckedChanged += (_, _) => RefreshPreview();
            foreach (var box in new[] { size, partitionSize, first, last }) box.TextChanged += (_, _) => RefreshPreview();
            foreach (var combo in new[] { sizeUnit, partitionUnit, blockSize }) combo.SelectionChanged += (_, _) => RefreshPreview();
            RefreshPreview();
            form.Children.Add(CommandGroup(cancel, save));
            dialog.Closing += (_, e) => { if (busy) { e.Cancel = true; cancellation.Cancel(); } };
            dialog.Content = new ScrollViewer { Content = form, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
            await dialog.ShowDialog(owner);
        }
        catch (Exception ex) when (IsWorkspaceFileError(ex) || ex is FormatException or OverflowException) { SetSettingsError(ex.Message); }
    }
}
