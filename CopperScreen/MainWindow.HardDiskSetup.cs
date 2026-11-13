using System.Globalization;
using Avalonia;
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
            if (layout != null && free == null) throw new InvalidOperationException("This disk has no free cylinders. Existing partitions are preserved.");
            var owner = _settingsVisible ? (Window)_settingsWindow : this;
            var dialog = new Window
            {
                Title = existing == null ? "Create disk image" : "Add partition and format",
                Width = 560, MaxHeight = 800, SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            var form = CreateSettingsGroupForm(); form.Margin = new Thickness(20);
            var explanation = SettingsNote(existing == null
                ? "Create a new RDB disk with one formatted partition. The disk is attached read-only; enable writes in disk settings when ready."
                : "Save a new copy with an additional formatted partition, then attach that copy. The original disk and existing partitions are preserved. Allow enough host storage for the copy.");
            form.Children.Add(explanation);
            if (layout != null)
            {
                form.Children.Add(SettingsNote($"{layout.Heads} heads × {layout.SectorsPerTrack} sectors/track. Usable cylinders {layout.FirstCylinder}–{layout.LastCylinder}."));
                foreach (var p in layout.Partitions)
                    form.Children.Add(SettingsNote($"{p.DeviceName}: cylinders {p.FirstCylinder}–{p.LastCylinder}, DOS type ${p.DosType:X8}"));
            }
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
            var size = AddTextSetting(form, "Capacity (MiB)", markRestart: false); size.Text = "100";
            size.IsEnabled = existing == null;
            if (source != null) size.Text = (new FileInfo(source).Length / 1048576d).ToString(CultureInfo.InvariantCulture);
            var fs = AddComboSetting(form, "Filesystem", existing == null
                ? ["Unformatted", .. CopperScreenHardDiskSetup.FileSystemNames] : CopperScreenHardDiskSetup.FileSystemNames, markRestart: false);
            fs.SelectedIndex = existing == null ? 1 : 0;
            bool IsBlank() => existing == null && fs.SelectedIndex == 0;
            int FileSystemIndex() => fs.SelectedIndex - (existing == null ? 1 : 0);
            var partition = CreateSettingsGroupForm();
            form.Children.Add(partition);
            var device = AddTextSetting(partition, "New partition device", markRestart: false);
            var deviceNumber = 0;
            while (layout?.Partitions.Any(p => string.Equals(p.DeviceName, $"DH{deviceNumber}", StringComparison.OrdinalIgnoreCase)) == true) deviceNumber++;
            device.Text = $"DH{deviceNumber}";
            var first = AddTextSetting(partition, "New first cylinder", markRestart: false);
            var last = AddTextSetting(partition, "New last cylinder", markRestart: false);
            first.Text = free?.First.ToString(CultureInfo.InvariantCulture) ?? "1";
            last.Text = free?.Last.ToString(CultureInfo.InvariantCulture) ?? "";
            last.PlaceholderText = "End of disk (leave blank for a new disk)";
            if (existing == null) partition.Children.Add(SettingsNote("New disks use 32 KiB cylinders. Set the last cylinder to leave free space for more partitions, or leave it blank to fill the disk."));
            var label = AddTextSetting(partition, "Volume label", markRestart: false); label.Text = "Work";
            var bootable = new CheckBox { Content = "Bootable partition", IsChecked = false }; partition.Children.Add(bootable);
            partition.Children.Add(SettingsNote("FFS uses DOS1; SFS and SFS2 require a compatible guest ROM or handler. CPX is the unnamed experimental CPX4 format and requires a CPX handler. Formatting does not install a handler or boot files."));
            var experimental = new CheckBox { Content = "I want to create an experimental CPX volume", IsVisible = false }; partition.Children.Add(experimental);
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
                        throw new InvalidOperationException("Confirm that you want an experimental CPX volume.");
                    var destination = CopperScreenStartupOptions.ResolveOptionalPath(path.Text, AppContext.BaseDirectory)
                        ?? throw new InvalidOperationException("Choose a new destination filename.");
                    long bytes = 0; uint? low = null, high = null;
                    if (existing == null)
                    {
                        if (!long.TryParse(size.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var mib) || mib < (IsBlank() ? 1 : 8))
                            throw new InvalidOperationException(IsBlank() ? "Capacity must be a positive whole number of MiB." : "Capacity must be a whole number of MiB, at least 8.");
                        bytes = checked(mib * 1048576);
                    }
                    if (IsBlank())
                    {
                        if (_settingsDraft.KickstartSource == CopperScreenKickstartSource.CopperStart)
                            throw new InvalidOperationException("CopperStart needs an existing disk image. Choose a filesystem to create a formatted image instead.");
                        if (Path.Exists(destination)) throw new IOException("Choose a new filename. Existing files are preserved.");
                        ReadHardfileSettings(_settingsDraft);
                        _settingsDraft.HardDrives.Add(new(NextHardDiskUnit(), destination, true, bytes, SparseStorage: true));
                        RefreshHardfileSettings(); ClearSettingsStartupError(); MarkSettingsRestartRequired();
                        close = true;
                        return;
                    }
                    if (!uint.TryParse(first.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var f))
                        throw new InvalidOperationException("Enter the first cylinder as a whole number.");
                    low = f;
                    if (existing != null || !string.IsNullOrWhiteSpace(last.Text))
                    {
                        if (!uint.TryParse(last.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var l))
                            throw new InvalidOperationException("Enter the last cylinder as a whole number.");
                        high = l;
                    }
                    ReadHardfileSettings(_settingsDraft);
                    var name = device.Text ?? ""; var volume = label.Text ?? "";
                    var fileSystem = (CopperScreenHardDiskSetup.FileSystem)FileSystemIndex(); var boot = bootable.IsChecked == true;
                    busy = true; _applyingSettings = true; UpdateSettingsStatus(); UpdateToolbarStatus();
                    status.Text = "Preparing and verifying disk… Cancel leaves the original disk unchanged.";
                    await Task.Run(() => CopperScreenHardDiskSetup.Create(destination, bytes, source, name, low, high, fileSystem, volume, boot, cancellation.Token));
                    var attached = new CopperScreenHardfileSettings(existing?.Unit ?? NextHardDiskUnit(), destination, existing?.ReadOnly ?? true,
                        0, AmigaHardfileMountMode.RigidDiskBlock, SparseStorage: true);
                    if (diskIndex is { } row) _settingsDraft.HardDrives[row] = attached; else _settingsDraft.HardDrives.Add(attached);
                    RefreshHardfileSettings(); ClearSettingsStartupError(); MarkSettingsRestartRequired();
                    close = true;
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
                experimental.IsVisible = !IsBlank() && FileSystemIndex() == 3;
                experimental.IsChecked = false;
                status.Text = string.Empty;
                if (existing == null) explanation.Text = IsBlank()
                    ? "Add an unformatted image to this machine. The file is created when the Amiga starts; format it inside the Amiga before use."
                    : "Create a new RDB disk with one formatted partition. The disk is attached read-only; enable writes in disk settings when ready.";
                if (existing == null) save.Content = IsBlank() ? "Add blank image" : "Create image";
            };
            form.Children.Add(CommandGroup(cancel, save));
            dialog.Closing += (_, e) => { if (busy) { e.Cancel = true; cancellation.Cancel(); } };
            dialog.Content = new ScrollViewer { Content = form, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
            await dialog.ShowDialog(owner);
        }
        catch (Exception ex) when (IsWorkspaceFileError(ex) || ex is FormatException or OverflowException) { SetSettingsError(ex.Message); }
    }
}
