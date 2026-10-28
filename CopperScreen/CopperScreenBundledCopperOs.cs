using System.Security.Cryptography;
using System.Text.Json;

namespace CopperScreen;

// Bundled media are immutable resources. The guest writes only to its personal
// HDF, created once. A recognized system-file update preserves all other bytes
// and keeps an atomic full-disk backup; custom system files remain untouched.
internal static class CopperScreenBundledCopperOs
{
    private sealed record SeedContract(string SeedId, int Bytes, string Sha256);
    private static readonly SeedContract Seed = ReadContract();
    private static readonly object PreparationGate = new();

    public static CopperScreenSettingsDraft PrepareForLaunch(CopperScreenSettingsDraft current, string userData)
    {
        if (CopperScreenLaunchWorkflow.StartChoice(current) != 3) return current;
        if (current.HardDrives.Count == 0) return Prepare(current, userData);
        var desktop = Path.Combine(userData, "CopperOS", "A1200-Desktop");
        if (current.HardDrives.Count == 1 &&
            Path.GetFullPath(current.HardDrives[0].Path).Equals(Path.GetFullPath(Path.Combine(desktop, "CopperOS.hdf")), StringComparison.OrdinalIgnoreCase) &&
            ReadMarker(desktop) is { } desktopSeed && IsDesktopSeed(desktopSeed))
        {
            // Firmware updates repair the app-owned cache while retaining the
            // user's disk, geometry and write protection, including documents.
            var updated = Prepare(current, userData);
            updated.HardDrives[0] = current.HardDrives[0];
            return updated;
        }
        // Only upgrade the app-created Shell proof configuration. Keep its disk
        // (including edits) intact and leave explicitly selected disks alone.
        var legacy = Path.Combine(userData, "CopperOS", "A1200");
        if (current.HardDrives.Count != 1 ||
            !Path.GetFullPath(current.HardDrives[0].Path).Equals(Path.GetFullPath(Path.Combine(legacy, "CopperOS.hdf")), StringComparison.OrdinalIgnoreCase))
            return current;
        var previous = ReadMarker(legacy);
        if (previous?.SeedId != "copperos-a1200-hdf-2026-10-22" || previous.Bytes != 8388608 ||
            previous.Sha256 != "54c6f78a7480437069b66a5f913c5ccde76fc3e604babeb1dc22674360242175") return current;
        return Prepare(current, userData);
    }

    private static SeedContract? ReadMarker(string workspace)
    {
        var marker = Path.Combine(workspace, "seed.json");
        if (!File.Exists(marker)) return null;
        try { return JsonSerializer.Deserialize<SeedContract>(File.ReadAllText(marker)); }
        catch (JsonException) { return null; }
    }

    private static bool IsDesktopSeed(SeedContract seed) =>
        seed.Bytes == Seed.Bytes && (seed.SeedId == Seed.SeedId && seed.Sha256 == Seed.Sha256 || IsPreviousDesktopSeed(seed));

    private static bool IsPreviousDesktopSeed(SeedContract seed) => seed.Bytes == 8388608 &&
        (seed.SeedId == "copperos-a1200-desktop-2026-10-25" &&
         seed.Sha256 == "e113a115bc2518014b79526f00e07c8bccacac3dc2537f30734115f4085f82ce" ||
         seed.SeedId == "copperos-a1200-desktop-2026-10-25-blitter.3" &&
         seed.Sha256 == "11f825cd5ec50786b9779449c1eefd1a2088a9bab502c2be326db94cbc968236" ||
         seed.SeedId == "copperos-a1200-desktop-2026-10-25-text.4" &&
         seed.Sha256 == "0eca0b4e67b3a1c0c16528d2543f01d436a5e10cf39cd1e542236a83225fed15");

    private static SeedContract ReadContract()
    {
        using var stream = typeof(CopperScreenBundledCopperOs).Assembly.GetManifestResourceStream("CopperScreen.Firmware.CopperOsA1200.json")!;
        return JsonSerializer.Deserialize<SeedContract>(stream)!;
    }

    public static CopperScreenSettingsDraft Prepare(CopperScreenSettingsDraft current, string userData)
    {
        lock (PreparationGate) return PrepareOwned(current, userData);
    }

    private static CopperScreenSettingsDraft PrepareOwned(CopperScreenSettingsDraft current, string userData)
    {
        var firmwareDirectory = Path.Combine(userData, "Firmware", "CopperStart-A1200", CopperScreenCopperStartRom.Sha256For(true));
        Directory.CreateDirectory(firmwareDirectory);
        var romPath = Path.Combine(firmwareDirectory, "CopperStart-A1200.rom");
        if (!HasExpectedRom(romPath))
        {
            var temporary = Path.Combine(firmwareDirectory, Guid.NewGuid().ToString("N") + ".rom");
            try
            {
                Extract("CopperStart-A1200.rom", temporary);
                _ = CopperScreenCopperStartRom.Read(temporary, a1200: true);
                try { File.Move(temporary, romPath); }
                catch (IOException) when (File.Exists(romPath))
                {
                    // Another process may have populated the immutable cache.
                    // Never delete a good destination in an overwrite race.
                    if (!HasExpectedRom(romPath))
                        try { File.Replace(temporary, romPath, null); }
                        catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && HasExpectedRom(romPath)) { }
                }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        _ = CopperScreenCopperStartRom.Read(romPath, a1200: true);

        var parent = Path.Combine(userData, "CopperOS");
        var workspace = Path.Combine(parent, "A1200-Desktop");
        var image = Path.Combine(workspace, "CopperOS.hdf");
        if (!Directory.Exists(workspace))
        {
            Directory.CreateDirectory(parent);
            var temporary = Path.Combine(parent, ".a1200-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            try
            {
                var seedPath = Path.Combine(temporary, "CopperOS.hdf");
                Extract("CopperOS.hdf", seedPath);
                using (var seed = File.OpenRead(seedPath))
                    if (seed.Length != Seed.Bytes || !Convert.ToHexString(SHA256.HashData(seed)).Equals(Seed.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The bundled CopperOS image is damaged. Reinstall the application; existing personal hard disks are preserved.");
                File.WriteAllText(Path.Combine(temporary, "seed.json"), JsonSerializer.Serialize(Seed));
                try { Directory.Move(temporary, workspace); }
                catch (IOException) when (Directory.Exists(workspace)) { }
            }
            finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true); }
        }
        if (!File.Exists(image))
            throw new FileNotFoundException("Your CopperOS hard disk is missing. Restore it from a backup or select a disk in Configure Amiga > Hard Disks. Existing work has not been replaced.", image);
        var protectedDisk = current.HardDrives.Any(d => d.ReadOnly &&
            Path.GetFullPath(d.Path).Equals(Path.GetFullPath(image), StringComparison.OrdinalIgnoreCase));
        if (!protectedDisk && ReadMarker(workspace) is { } previous && IsPreviousDesktopSeed(previous) && previous.Sha256 != Seed.Sha256)
        {
            using var source = typeof(CopperScreenBundledCopperOs).Assembly.GetManifestResourceStream("CopperScreen.Bundled.CopperOS.hdf")!;
            using var buffer = new MemoryStream(); source.CopyTo(buffer); var bundled = buffer.ToArray();
            if (!Convert.ToHexString(SHA256.HashData(bundled)).Equals(Seed.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The bundled CopperOS system update is damaged.");
            if (CopperScreenCopperOsSystemUpdate.TryUpgrade(image, CopperScreenCopperOsSystemUpdate.ReadHome(bundled),
                CopperScreenCopperOsSystemUpdate.ReadCommand(bundled), CopperScreenCopperOsSystemUpdate.ReadGui(bundled)))
            {
                var marker = Path.Combine(workspace, "seed.json");
                var temporary = marker + "." + Guid.NewGuid().ToString("N");
                try { File.WriteAllText(temporary, JsonSerializer.Serialize(Seed)); File.Move(temporary, marker, overwrite: true); }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }
        var hardfile = CopperScreenHardfileSettings.Ffs(0, image, false, "DH0:2:32:0:255");
        hardfile.ValidateCopperStart(checkImage: true);
        var draft = current.Clone();
        draft.KickstartRomPath = romPath;
        draft.HardDrives.Clear();
        draft.HardDrives.Add(hardfile);
        return draft;
    }

    private static void Extract(string name, string destination)
    {
        using var source = typeof(CopperScreenBundledCopperOs).Assembly.GetManifestResourceStream("CopperScreen.Bundled." + name)
            ?? throw new InvalidOperationException("The CopperOS bundle is missing. Reinstall the application.");
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        source.CopyTo(output);
        output.Flush(flushToDisk: true);
    }

    private static bool HasExpectedRom(string path)
    {
        if (!File.Exists(path)) return false;
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).Equals(CopperScreenCopperStartRom.Sha256For(true), StringComparison.OrdinalIgnoreCase);
    }
}
