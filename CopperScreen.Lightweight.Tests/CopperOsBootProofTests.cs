using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Copper68k;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class CopperOsBootProofTests
{
    [CopperOsHdfProofFact(a1200: true)]
    public void A1200ReadOnlyHdfBootsAndReportsWriteProtection()
    {
        var root = Environment.GetEnvironmentVariable("COPPERSCREEN_COPPEROS_HDF_PROOF")!;
        RunProof(root, Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_A1200_ROM")!,
            Path.Combine(root, "CopperOS.hdf"), "hdf-image.json", hardfile: true, a1200: true);
    }

    [CopperOsHdfProofFact(a1200: true)]
    public void A1200HdfBootWritesAndReadsAfterColdReopen()
    {
        var root = Environment.GetEnvironmentVariable("COPPERSCREEN_COPPEROS_HDF_PROOF")!;
        var rom = Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_A1200_ROM")!;
        Assert.Equal(1048576, CopperScreenCopperStartRom.Read(rom, a1200: true).Length);
        Assert.Throws<InvalidOperationException>(() => CopperScreenCopperStartRom.Read(rom));
        var output = Path.Combine(root, "a1200-proof", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        var disk = Path.Combine(output, "CopperOS.hdf");
        File.Copy(Path.Combine(root, "CopperOS.hdf"), disk);
        RunProof(root, rom, disk, "hdf-image.json", hardfile: true, writable: true, a1200: true);
        var written = Hash(disk);
        RunProof(root, rom, disk, "hdf-image.json", hardfile: true, writable: true, reopened: true, a1200: true);
        Assert.Equal(written, Hash(disk));
    }

    [CopperOsProofFact]
    public void OriginalSystemBootRegistersShellAndRunsInteractiveCommandsThroughLightweight()
    {
        var root = Environment.GetEnvironmentVariable("COPPERSCREEN_COPPEROS_PROOF")!;
        var rom = Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_ROM")!;
        RunProof(root, rom, Path.Combine(root, "System.adf"), "system-image.json");
    }

    [CopperOsHdfProofFact]
    public void HdfOnlyBootWritesFilesAndReadsThemAfterColdReopen()
    {
        var root = Environment.GetEnvironmentVariable("COPPERSCREEN_COPPEROS_HDF_PROOF")!;
        var rom = Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_ROM")!;
        var output = Path.Combine(root, "hdf-proof", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        var disk = Path.Combine(output, "CopperOS.hdf");
        File.Copy(Path.Combine(root, "CopperOS.hdf"), disk);
        RunProof(root, rom, disk, "hdf-image.json", hardfile: true, writable: true);
        var writtenHash = Hash(disk);
        // RunProof disposes the entire session; this boot has no guest RAM or
        // filesystem cache from the writing session.
        RunProof(root, rom, disk, "hdf-image.json", hardfile: true, writable: true, reopened: true);
        Assert.Equal(writtenHash, Hash(disk));
    }

    [CopperOsHdfProofFact]
    public void ReadOnlyHdfBootsAndReportsNativeWriteProtectionWithoutChangingImage()
    {
        var root = Environment.GetEnvironmentVariable("COPPERSCREEN_COPPEROS_HDF_PROOF")!;
        RunProof(root, Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_ROM")!,
            Path.Combine(root, "CopperOS.hdf"), "hdf-image.json", hardfile: true);
    }

    private static void RunProof(string root, string rom, string disk, string receiptName,
        bool hardfile = false, bool writable = false, bool reopened = false, bool a1200 = false)
    {
        using var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, receiptName)));
        Assert.Equal("original-system-boot-proof", receipt.RootElement.GetProperty("kind").GetString());
        Assert.False(receipt.RootElement.GetProperty("shipping").GetBoolean());
        var initialHash = Hash(disk);
        if (!reopened) Assert.Equal(receipt.RootElement.GetProperty("sha256").GetString(), initialHash);
        var output = Path.Combine(root, "app-proof", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") +
            (a1200 ? "-a1200" : "") + (hardfile ? writable ? reopened ? "-hdf-reopen" : "-hdf-write" : "-hdf-readonly" : "-adf"));
        Directory.CreateDirectory(output);
        var userData = Path.Combine(output, "user-data");
        Directory.CreateDirectory(userData);
        string[] args = hardfile ? ["--copperstart", "--rom", rom,
            writable ? "--hdf-ffs-writable" : "--hdf-ffs", disk, "DH0:2:32:0:255"] : ["--copperstart", "--rom", rom, disk];
        var options = CopperScreenStartupOptions.Parse(args, AppContext.BaseDirectory,
            userDataDirectory: userData);
        Assert.Null(options.Error);
        if (a1200)
        {
            var draft = CopperScreenMachinePresets.Apply(CopperScreenSettingsDraft.FromStartupOptions(options), CopperScreenMachineModel.Amiga1200);
            options = draft.ToStartupOptions(AppContext.BaseDirectory);
        }
        using var created = CopperScreenSession.Create(options);
        var session = Assert.IsType<CopperScreenLightweightSession>(created);
        if (hardfile)
        {
            Assert.Null(session.DiskPath);
            Assert.False(session.Machine.IsDriveMounted(0));
            Assert.Equal((a1200 ? 2048 : 512) * 1024, session.Machine.ChipRam.Length);
            Assert.Equal(a1200, session.Machine.IsAga);
            Assert.Equal(a1200 ? M68kCpuModel.M68EC020 : M68kCpuModel.M68000, session.Machine.CpuModel);
            Assert.Single(options.HardDrives);
            // Also leave a normal, reusable app profile alongside the evidence.
            CopperScreenProfileStore.SaveToPath(CopperScreenSettingsDraft.FromStartupOptions(options), Path.Combine(output, "profile.json"));
        }
        var slow = (byte[])session.Machine.GetType().GetField("_slowRam", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session.Machine)!;
        Assert.Equal(a1200 ? 0 : 512 * 1024, slow.Length);
        var fields = 0;
        uint nativeControl = 0;
        byte Read(uint address) => address < session.Machine.ChipRam.Length ? session.Machine.ChipRam.Span[(int)address]
            : address >= 0xC00000 && address < 0xC00000 + slow.Length ? slow[address - 0xC00000] : (byte)0;
        uint Long(uint address) => ((uint)Read(address) << 24) | ((uint)Read(address + 1) << 16) | ((uint)Read(address + 2) << 8) | Read(address + 3);
        string Screen()
        {
            // Private diagnostics only: read the actual native terminal bitmap,
            // without injecting text, calling DOS or altering guest memory.
            var control = Long(0x880); var console = Long(control + 32); var bitmap = Long(console + 128);
            if (control == 0 || console == 0 || bitmap == 0 || bitmap + 16000 > session.Machine.ChipRam.Length) return "";
            var text = new StringBuilder();
            for (var row = 0; row < 25; row++)
            {
                for (var col = 0; col < 80; col++)
                {
                    ulong glyph = 0;
                    for (var y = 0; y < 8; y++) glyph = (glyph << 8) | Read(bitmap + (uint)((row * 8 + y) * 80 + col));
                    text.Append(Glyphs.GetValueOrDefault(glyph, '?'));
                }
                text.Append('\n');
            }
            return text.ToString();
        }
        void Capture(string label)
        {
            NativeCapture.WritePng(Path.Combine(output, label + ".png"), session.Framebuffer, session.Width, session.Height);
            File.WriteAllText(Path.Combine(output, label + ".txt"), Screen());
            File.WriteAllText(Path.Combine(output, label + ".state.json"), JsonSerializer.Serialize(new
            { fields, pc = session.Machine.Cpu.ProgramCounter.ToString("X8"), cycle = session.Machine.Cycle, dos = Long(Long(0x880)),
                sr = session.Machine.Cpu.StatusRegister, data = session.Machine.Cpu.D, address = session.Machine.Cpu.A }));
            File.WriteAllBytes(Path.Combine(output, label + ".chipram"), session.Machine.ChipRam.ToArray());
            File.WriteAllBytes(Path.Combine(output, label + ".slowram"), slow);
        }
        void Run(int count)
        {
            for (var i = 0; i < count; i++)
            {
                session.RenderNextFrame(session.Framebuffer); fields++;
                Assert.False(session.IsPaused); Assert.Null(session.FaultMessage);
                Assert.Null(session.Machine.UnsupportedActiveFeature);
                if (nativeControl != 0 && Long(0x880) != nativeControl)
                { Capture("memory-corruption"); Assert.Fail("Native diagnostic receipt was overwritten during Shell input."); }
            }
        }
        void WaitFor(string value, string label, int limit, bool exactLine = false, int minimumMatches = 1)
        {
            bool Present() => exactLine ? Screen().Split('\n').Any(line => line.Trim() == value) : Screen().Split(value).Length - 1 >= minimumMatches;
            for (var n = 0; n < limit && !Present(); n += 100)
            {
                Run(100);
                if (nativeControl == 0 && Screen().Contains("FAILED", StringComparison.Ordinal))
                { Capture(label + "-failed"); Assert.Fail("Native bootstrap failed:\n" + Screen()); }
                if ((n + 100) % 10000 == 0 || n == 900) Capture(label + "-" + fields);
            }
            Capture(label);
            Assert.True(Present(), "Native console did not display " + value + ":\n" + Screen());
        }
        void Type(string line)
        {
            // Command output can be visible before the following prompt is
            // fully drawn. Wait for the native Shell to finish it before keys.
            bool Ready() => Screen().Split('\n').LastOrDefault(row => !string.IsNullOrWhiteSpace(row))?.Trim() == "2.>";
            for (var n = 0; n < 3000 && !Ready(); n += 20) Run(20);
            Assert.True(Ready(), "Native Shell did not finish its prompt before input:\n" + Screen());
            foreach (var c in line)
            {
                var key = c == ' ' ? AmigaRawKey.Space : Enum.Parse<AmigaRawKey>(char.ToUpperInvariant(c).ToString());
                session.KeyDown(key); Run(4); session.KeyUp(key); Run(4);
            }
            Capture("typed-" + line.Split(' ')[0]);
            session.KeyDown(AmigaRawKey.Return); Run(4); session.KeyUp(AmigaRawKey.Return); Run(20);
        }
        WaitFor(">", "boot-prompt", 30000);
        Assert.Contains("COPPEROS SYSTEM PROOF", Screen());
        Assert.Contains("COPPEROS SHELL REGISTERED", Screen());
        nativeControl = Long(0x880);
        Type("alias proof echo copperos verified");
        WaitFor("2.>", "alias-defined", 3000, minimumMatches: 2);
        Type("proof");
        WaitFor("COPPEROS VERIFIED", "alias", 3000, exactLine: true);
        Type("demo");
        WaitFor("COMMAND RETURNED TO COPPEROS SHELL", "demo", 5000);
        WaitFor("2.>", "demo-return", 3000, minimumMatches: 4);
        Type("missingcommand"); WaitFor("2.>", "command-error-return", 5000, minimumMatches: 5);
        Type("why"); WaitFor("OBJECT NOT FOUND", "command-error", 3000);
        Type("echo recovery"); WaitFor("RECOVERY", "recovery", 3000, exactLine: true);
        Assert.Contains(">", Screen());
        if (hardfile)
        {
            Type("storage");
            WaitFor(writable ? reopened ? "COPPEROS STORAGE REOPEN VERIFIED" : "COPPEROS STORAGE CREATED AND VERIFIED"
                : "COPPEROS STORAGE WRITE PROTECTED", "storage", 10000);
            Type("echo storage returned");
            WaitFor("STORAGE RETURNED", "storage-return", 3000, exactLine: true);
            session.SavePersistentStorage();
            if (writable && !reopened) Assert.NotEqual(initialHash, Hash(disk));
            else Assert.Equal(initialHash, Hash(disk));
        }
        if (a1200 && !writable)
        {
            nativeControl = 0;
            session.Reset();
            Run(1000);
            WaitFor(">", "reset-prompt", 30000);
            Type("echo after reset");
            WaitFor("AFTER RESET", "reset-command", 3000, exactLine: true);
            Assert.Equal(initialHash, Hash(disk));
        }
        Assert.False(session.Machine.IsDriveDirty(0));
        if (!hardfile) Assert.Equal(initialHash, Hash(disk));
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1, protocol = hardfile ? "copperos-native-hdf-boot-v1" : "copperos-native-shell-boot-v1", passed = true, shipping = false,
            pairId = receipt.RootElement.GetProperty("pairId").GetString(),
            romSha256 = Hash(rom), initialImageSha256 = initialHash, imageSha256 = Hash(disk), fields,
            hardfile, writable, reopened, imagePath = disk,
            machine = a1200 ? "PAL Alice/Lisa 68EC020, 2 MiB Chip, no slow/Fast RAM, Lightweight standard session"
                : "PAL OCS 68000, 512 KiB Chip + 512 KiB slow, Lightweight standard session",
            a1200, resetVerified = a1200 && !writable,
            media = hardfile ? "8 MiB FFS DH0, no floppy" : "read-only DF0",
            storageObservation = !hardfile ? "not exercised" : !writable ? "native write protection; image unchanged"
                : reopened ? "settings and data read after full session disposal" : "settings/data, rename/delete, relative child and directory operations",
            observations = new[] { "native prompt", "Alias and internal command dispatch", "original native Demo and return", "command-error recovery" },
            binaries = new[] { typeof(CopperScreenSession).Assembly.Location, session.Machine.GetType().Assembly.Location,
                typeof(M68kCoreFactory).Assembly.Location, typeof(CopperOsBootProofTests).Assembly.Location }
                .Select(path => new { file = Path.GetFileName(path), sha256 = Hash(path) })
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static string Hash(string file)
    {
        // A live HDF retains its writable backing handle until session disposal.
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
    // Original native console's packed 8x8 glyphs, interpreted from its bitmap.
    private static readonly Dictionary<ulong, char> Glyphs = new()
    {
        [0x1c22262a32221c00UL]='0',[0x0818080808081c00UL]='1',[0x1c22020408103e00UL]='2',
        [0x3c02021c02023c00UL]='3',[0x040c14243e040400UL]='4',[0x3e20203c02023c00UL]='5',
        [0x1c20203c22221c00UL]='6',[0x3e02040810101000UL]='7',[0x1c22221c22221c00UL]='8',
        [0x1c22221e02021c00UL]='9',[0x0000000000000000UL]=' ',[0x1c22223e22222200UL]='A',
        [0x3c22223c22223c00UL]='B',[0x1e20202020201e00UL]='C',[0x3c22222222223c00UL]='D',
        [0x3e20203c20203e00UL]='E',[0x3e20203c20202000UL]='F',[0x1e20202e22221e00UL]='G',
        [0x2222223e22222200UL]='H',[0x3e08080808083e00UL]='I',[0x0e04040424241800UL]='J',
        [0x2224283028242200UL]='K',[0x2020202020203e00UL]='L',[0x22362a2a22222200UL]='M',
        [0x22322a2622222200UL]='N',[0x1c22222222221c00UL]='O',[0x3c22223c20202000UL]='P',
        [0x1c2222222a241a00UL]='Q',[0x3c22223c28242200UL]='R',[0x1e20201c02023c00UL]='S',
        [0x3e08080808080800UL]='T',[0x2222222222221c00UL]='U',[0x2222222222140800UL]='V',
        [0x2222222a2a2a1400UL]='W',[0x2222140814222200UL]='X',[0x2222140808080800UL]='Y',
        [0x3e02040810203e00UL]='Z',[0x00000000000c0c00UL]='.',[0x000c0c000c0c0000UL]=':',
        [0x0000003e00000000UL]='-',[0x0202040810202000UL]='/',[0x2010080408102000UL]='>'
    };
}
public sealed class CopperOsHdfProofFactAttribute : FactAttribute
{
    public CopperOsHdfProofFactAttribute(bool a1200 = false)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COPPERSCREEN_COPPEROS_HDF_PROOF")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(a1200 ? "COPPERSCREEN_COPPERSTART_A1200_ROM" : "COPPERSCREEN_COPPERSTART_ROM")))
            Skip = "Original CopperOS HDF proof and qualified ROM not supplied; native coverage unavailable.";
    }
}
public sealed class CopperOsProofFactAttribute : FactAttribute
{
    public CopperOsProofFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COPPERSCREEN_COPPEROS_PROOF")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_ROM")))
            Skip = "Original CopperOS system proof and qualified ROM not supplied; native coverage unavailable.";
    }
}
