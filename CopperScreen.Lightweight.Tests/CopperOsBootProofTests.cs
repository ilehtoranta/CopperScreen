using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Copper68k;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class CopperOsBootProofTests
{
    [CopperOsProofFact]
    public void OriginalSystemBootRegistersShellAndRunsInteractiveCommandsThroughLightweight()
    {
        var root = Environment.GetEnvironmentVariable("COPPERSCREEN_COPPEROS_PROOF")!;
        var rom = Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_ROM")!;
        var disk = Path.Combine(root, "System.adf");
        using var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "system-image.json")));
        Assert.Equal("original-system-boot-proof", receipt.RootElement.GetProperty("kind").GetString());
        Assert.False(receipt.RootElement.GetProperty("shipping").GetBoolean());
        Assert.Equal(receipt.RootElement.GetProperty("sha256").GetString(), Hash(disk));
        var output = Path.Combine(root, "app-proof", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));
        Directory.CreateDirectory(output);
        var userData = Path.Combine(output, "user-data");
        Directory.CreateDirectory(userData);
        var options = CopperScreenStartupOptions.Parse(["--copperstart", "--rom", rom, disk], AppContext.BaseDirectory,
            userDataDirectory: userData);
        Assert.Null(options.Error);
        using var created = CopperScreenSession.Create(options);
        var session = Assert.IsType<CopperScreenLightweightSession>(created);
        var slow = (byte[])session.Machine.GetType().GetField("_slowRam", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session.Machine)!;
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
                if ((n + 100) % 10000 == 0) Capture(label + "-" + fields);
            }
            Capture(label);
            Assert.True(Present(), "Native console did not display " + value + ":\n" + Screen());
        }
        void Type(string line)
        {
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
        Assert.False(session.Machine.IsDriveDirty(0));
        Assert.Equal(receipt.RootElement.GetProperty("sha256").GetString(), Hash(disk));
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1, protocol = "copperos-native-shell-boot-v1", passed = true, shipping = false,
            pairId = receipt.RootElement.GetProperty("pairId").GetString(),
            romSha256 = Hash(rom), imageSha256 = Hash(disk), fields,
            machine = "PAL OCS 68000, 512 KiB Chip + 512 KiB slow, read-only DF0, Lightweight standard session",
            observations = new[] { "native prompt", "Alias and internal command dispatch", "original native Demo and return", "command-error recovery" },
            binaries = new[] { typeof(CopperScreenSession).Assembly.Location, session.Machine.GetType().Assembly.Location,
                typeof(M68kCoreFactory).Assembly.Location, typeof(CopperOsBootProofTests).Assembly.Location }
                .Select(path => new { file = Path.GetFileName(path), sha256 = Hash(path) })
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
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
public sealed class CopperOsProofFactAttribute : FactAttribute
{
    public CopperOsProofFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COPPERSCREEN_COPPEROS_PROOF")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_ROM")))
            Skip = "Original CopperOS system proof and qualified ROM not supplied; native coverage unavailable.";
    }
}
