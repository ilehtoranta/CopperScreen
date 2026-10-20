#if COPPERSCREEN_INSTALLED_TESTS || COPPERSCREEN_INSTALLED_MEDIA_TESTS
using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace CopperScreen;

// Diagnostic fixtures only, using the app's production storage/session APIs.
// No barriers, test firmware or fault switches are added to shipping builds.
internal static class CopperScreenInstalledMediaProof
{
    private sealed record State(Dictionary<string, string> Originals, Dictionary<string, string> PersistentFiles,
        Dictionary<string, string> Exports, string Backup, string Workspace, string ImportedWorkspace, string PreservedImage);
    private static string StatePath(string directory) => Path.Combine(directory, "media-proof.json");
    private static string ExternalState(string output) => Path.Combine(output, "media-proof-exports.json");
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static State Read(string path) => JsonSerializer.Deserialize<State>(File.ReadAllText(path)) ?? throw new InvalidDataException("Missing media proof state.");
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void CheckFiles(Dictionary<string, string> files)
    {
        foreach (var (path, hash) in files) Require(File.Exists(path) && Hash(path) == hash, "Media proof file changed or is missing: " + path);
    }
    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException) { return; }
        throw new InvalidOperationException(message);
    }
    private static void Remember(Dictionary<string, string> files, params string[] paths)
    { foreach (var path in paths) files.Add(path, Hash(path)); }

    public static string? Prepare(string phase, string directory, string output)
    {
        if (phase == "recovered") return Path.Combine(directory, "CopperOS", "Workspaces", "live-recovery", "System.adf");
        if (phase != "fresh") return null;
        var state = Read(ExternalState(output));
        Require((!Directory.Exists(directory) || !Directory.EnumerateFileSystemEntries(directory).Any()) &&
            !File.Exists(StatePath(directory)) && state.PersistentFiles.Keys.All(p => !File.Exists(p)), "Uninstall retained package-private media state.");
        CheckFiles(state.Originals); CheckFiles(state.Exports);
        return CopperOsWorkspace.ImportBackup(directory, "restored-after-uninstall", state.Backup);
    }

    public static object Complete(string phase, string directory, string output, object nativeResult)
    {
        var native = JsonSerializer.SerializeToElement(nativeResult);
        Require(native.GetProperty("passed").GetBoolean() && native.GetProperty("engine").GetString() == "Lightweight" &&
            !native.GetProperty("legacy").GetBoolean() && !native.GetProperty("hostDosCalls").GetBoolean(), "Media qualification requires a passing native Lightweight replay.");
        State state;
        if (phase == "seed") state = Seed(directory, output);
        else if (phase == "fresh")
        {
            state = Read(ExternalState(output)); CheckFiles(state.Originals); CheckFiles(state.Exports);
            var restored = Path.Combine(directory, "CopperOS", "Workspaces", "restored-after-uninstall", "System.adf");
            Require(Hash(restored) == state.PersistentFiles[state.ImportedWorkspace], "Fresh import changed the saved native image.");
            return new { Protocol = "copperscreen-installed-media-v1", Passed = true, Phase = phase, state.Originals, state.Exports,
                RestoredWorkspace = restored, RestoredImageSha256 = Hash(restored), PackagePrivateStateWasRemoved = true,
                NativeReadback = true, NativePickerWalkthrough = false };
        }
        else
        {
            state = Read(StatePath(directory)); CheckFiles(state.Originals); CheckFiles(state.PersistentFiles); CheckFiles(state.Exports);
            if (phase == "recovery")
            {
                Require(native.GetProperty("denyHostSaves").GetBoolean(), "Expected a real denied native checkpoint.");
                var backup = native.GetProperty("recoveredBackup").GetString()!;
                var imported = CopperOsWorkspace.ImportBackup(directory, "live-recovery", backup);
                Require(Hash(imported) == native.GetProperty("imageSha256").GetString(), "Live recovery import differs from the native export.");
                Remember(state.PersistentFiles, imported, Path.Combine(Path.GetDirectoryName(imported)!, "workspace.json"));
                Remember(state.Exports, backup, native.GetProperty("recoveredExport").GetString()!);
            }
        }
        CheckFiles(state.Originals); CheckFiles(state.PersistentFiles); CheckFiles(state.Exports);
        var json = JsonSerializer.Serialize(state);
        CopperScreenUserData.WriteAtomically(StatePath(directory), json);
        CopperScreenUserData.WriteAtomically(ExternalState(output), json);
        return new { Protocol = "copperscreen-installed-media-v1", Passed = true, Phase = phase, state.Originals, state.PersistentFiles, state.Exports,
            state.Backup, state.Workspace, state.ImportedWorkspace, state.PreservedImage,
            NativeReadback = phase == "recovered", NativePickerWalkthrough = false };
    }

    private static State Seed(string directory, string output)
    {
        var external = Path.Combine(output, "Media"); Directory.CreateDirectory(external);
        var originals = new Dictionary<string, string>(); var exports = new Dictionary<string, string>();
        var persisted = new Dictionary<string, string>();
        var adf = RunAdfProtection(directory, external); Remember(originals, adf.Original, adf.Archive);
        Remember(exports, adf.Saved); Remember(persisted, adf.Rom);
        var image = Path.Combine(directory, "CopperOS", "Workspaces", "persistence", "System.adf");
        var complete = File.ReadAllBytes(image);
        using (var owner = CopperOsWorkspace.Open(image))
        {
            owner.Save(complete); // Retain a previous checkpoint containing the verified guest files.
            Reject(() => CopperOsWorkspace.RecoverPrevious(image), "Recovery ignored an active session lease.");
        }
        var backup = Path.Combine(external, "baseline.copperos"); CopperOsWorkspace.ExportSaved(image, backup);
        Reject(() => CopperOsWorkspace.ExportSaved(image, backup), "Backup silently replaced an existing export.");
        Reject(() => CopperOsWorkspace.ExportSaved(image, Path.Combine(Path.GetDirectoryName(image)!, "unsafe.copperos")), "Backup entered managed storage.");
        var imported = CopperOsWorkspace.ImportBackup(directory, "backup-import", backup);
        Require(File.ReadAllBytes(imported).AsSpan().SequenceEqual(complete), "Backup import changed native files.");
        Reject(() => CopperOsWorkspace.ImportBackup(directory, "backup-import", backup), "Import replaced an existing workspace.");
        var invalid = Path.Combine(external, "invalid.copperos"); File.WriteAllBytes(invalid, [1, 2, 3]);
        Reject(() => CopperOsWorkspace.ImportBackup(directory, "invalid-import", invalid), "Invalid backup was admitted.");
        Require(!Directory.Exists(Path.Combine(directory, "CopperOS", "Workspaces", "invalid-import")), "Failed import created a workspace.");
        var damaged = (byte[])complete.Clone(); damaged[880 * 512 + 20] ^= 1; File.WriteAllBytes(image, damaged);
        Reject(() => CopperOsWorkspace.Open(image), "Damaged primary was silently opened.");
        var preserved = CopperOsWorkspace.RecoverPrevious(image);
        Require(File.ReadAllBytes(preserved).AsSpan().SequenceEqual(damaged) && File.ReadAllBytes(image).AsSpan().SequenceEqual(complete), "Recovery failed to preserve and restore images.");
        var previous = Path.Combine(Path.GetDirectoryName(image)!, "Previous.adf");
        File.Delete(image); CopperOsWorkspace.RecoverPrevious(image);
        Require(File.ReadAllBytes(image).AsSpan().SequenceEqual(complete), "Missing-primary recovery changed native files.");
        File.SetAttributes(image, FileAttributes.ReadOnly);
        try { using var owner = CopperOsWorkspace.Open(image); Reject(() => owner.Save(complete), "Read-only checkpoint unexpectedly succeeded."); }
        catch (UnauthorizedAccessException) { /* Windows may reject the writable open itself. */ }
        finally { File.SetAttributes(image, FileAttributes.Normal); }
        Require(File.ReadAllBytes(image).AsSpan().SequenceEqual(complete), "Denied checkpoint changed the primary.");
        Remember(persisted, image, previous, preserved, Path.Combine(Path.GetDirectoryName(image)!, "workspace.json"),
            imported, Path.Combine(Path.GetDirectoryName(imported)!, "workspace.json"));
        Remember(exports, backup);
        return new(originals, persisted, exports, backup, image, imported, preserved);
    }

    // Synthetic v34 loop and Paula write, not a native OS/ROM compatibility replay.
    internal sealed record AdfProof(string Original, string Archive, string Saved, string Rom);
    internal static AdfProof RunAdfProtection(string directory, string external)
    {
        Directory.CreateDirectory(directory); Directory.CreateDirectory(external);
        var rom = Path.Combine(directory, "media-fixture.rom"); var firmware = new byte[262144];
        BinaryPrimitives.WriteUInt32BigEndian(firmware, 0x70000); BinaryPrimitives.WriteUInt32BigEndian(firmware.AsSpan(4), 0xFC0100);
        firmware[13] = 34; firmware[0x100] = 0x60; firmware[0x101] = 0xFE; File.WriteAllBytes(rom, firmware);
        var bytes = new byte[901120]; var original = Path.Combine(external, "original.adf"); File.WriteAllBytes(original, bytes);
        var archive = Path.Combine(external, "original.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create)) { using var entry = zip.CreateEntry("disk.adf").Open(); entry.Write(bytes); }
        var originalHash = Hash(original); var archiveHash = Hash(archive);
        // The profile-persistence fixture may override the default profile with deliberately
        // non-bootable media. This device control owns an independent bounded configuration.
        var draft = new CopperScreenSettingsDraft { Engine = CopperScreenEngine.Lightweight,
            KickstartSource = CopperScreenKickstartSource.Kickstart13Rom, RomVersion = KickstartVersion.Kickstart13,
            KickstartRomPath = rom, FloppyDriveCount = 2 };
        using var session = new CopperScreenLightweightSession(draft.ToStartupOptions(AppContext.BaseDirectory));
        Require(session.InsertLoadedDisk(0, archive, CopperScreenDiskImageArchive.LoadDiskImage(archive), false) && session.Machine.IsDriveWriteProtected(0), "Imported disk was writable by default.");
        Require(session.InsertLoadedDisk(1, original, CopperScreenAdfImage.Load(original), false), "Cannot mount the second protected original.");
        Require(session.SetDriveWriteProtected(0, false), "Explicit in-memory writes were rejected.");
        bytes[128] = 37;
        var machine = session.Machine;
        void Byte(uint address, byte value) { var cycle = machine.Cpu.Cycles; machine.WriteByte(address, value, ref cycle, Copper68k.M68kBusAccessKind.CpuDataWrite); machine.Cpu.Cycles = cycle; }
        void Word(uint address, ushort value) { var cycle = machine.Cpu.Cycles; machine.WriteWord(address, value, ref cycle, Copper68k.M68kBusAccessKind.CpuDataWrite); machine.Cpu.Cycles = cycle; }
        Byte(0xBFD100, 0x77); Byte(0xBFD300, 255);
        for (var i = 0; i < 31; i++) session.RenderNextFrame(session.Framebuffer);
        var track = CopperDisk.AmigaDosTrackEncoder.EncodeTrack(CopperDisk.AmigaDiskLoader.FromAdfBytes(bytes), 0, 0);
        for (var i = 0; i < track.Length; i += 2) Word((uint)(0x20000 + i), (ushort)((track[i] << 8) | track[i + 1]));
        Word((uint)(0x20000 + track.Length), 0xAAAA); Word(0xDFF020, 2); Word(0xDFF022, 0);
        Word(0xDFF096, 0x8210); Word(0xDFF09E, 0x8100);
        var length = (ushort)(0xC000 | (track.Length / 2 + 1)); Word(0xDFF024, length); Word(0xDFF024, length);
        for (var i = 0; i < 12; i++) session.RenderNextFrame(session.Framebuffer);
        Require(machine.IsDriveDirty(0) && machine.ExportAdf().AsSpan().SequenceEqual(bytes), "Guest DMA did not write the synthetic save.");
        session.TogglePaused();
        using (var runtime = CopperScreenRuntime.CreateForTests(session))
        {
            runtime.Start(); var saved = Path.Combine(external, "saved.adf"); File.WriteAllBytes(saved, [1, 2, 3]);
            var refused = runtime.SaveAdfAsync(0, saved).GetAwaiter().GetResult(); Require(!refused.Success && refused.State.Drives[0].HasUnsavedChanges, "Implicit replacement was accepted.");
            Require(!runtime.SaveAdfAsync(0, original, true).GetAwaiter().GetResult().Success, "Another mounted original was overwritten.");
            using (var locked = new FileStream(saved, FileMode.Open, FileAccess.Read, FileShare.None))
                Require(!runtime.SaveAdfAsync(0, saved, true).GetAwaiter().GetResult().Success, "Locked destination save unexpectedly succeeded.");
            Require(!runtime.SaveAdfAsync(0, Path.Combine(external, "removed", "saved.adf")).GetAwaiter().GetResult().Success, "Missing destination save unexpectedly succeeded.");
            Require(!runtime.EjectDiskAsync().GetAwaiter().GetResult().Success, "Dirty disk was ejected.");
            Require(runtime.ResetAsync().GetAwaiter().GetResult().State.Drives[0].HasUnsavedChanges, "Reset lost the dirty disk.");
            var result = runtime.SaveAdfAsync(0, saved, true).GetAwaiter().GetResult();
            Require(result.Success && !result.State.Drives[0].HasUnsavedChanges && File.ReadAllBytes(saved).AsSpan().SequenceEqual(bytes), "Explicit ADF save failed.");
            Require(Hash(original) == originalHash && Hash(archive) == archiveHash, "Saving changed imported originals.");
            Require(CopperScreenAdfImage.Load(saved).Data.AsSpan().SequenceEqual(bytes), "Saved ADF did not reopen.");
            return new(original, archive, saved, rom);
        }
    }
}
#endif
