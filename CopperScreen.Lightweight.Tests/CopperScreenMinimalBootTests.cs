using System.Buffers.Binary;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class CopperScreenMinimalBootTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "copperscreen-minimal-" + Guid.NewGuid().ToString("N"));
    public CopperScreenMinimalBootTests() => Directory.CreateDirectory(_directory);
    private static byte[] Disk(string code)
    {
        var disk = new byte[901120];
        "DOS\0"u8.CopyTo(disk);
        new CopperScreenBootAssembler(CopperScreenMinimalBoot.BootAddress + 12).Assemble(code).CopyTo(disk, 12);
        Seal(disk);
        return disk;
    }
    private static void Seal(byte[] disk)
    {
        disk.AsSpan(4, 4).Clear();
        uint sum = 0;
        for (var i = 0; i < 1024; i += 4)
        {
            var next = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(disk.AsSpan(i, 4)));
            if (next < sum) next++;
            sum = next;
        }
        BinaryPrimitives.WriteUInt32BigEndian(disk.AsSpan(4), ~sum);
    }
    private CopperScreenStartupOptions Options(byte[] disk, params string[] more)
    {
        var path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".adf");
        File.WriteAllBytes(path, disk);
        return CopperScreenStartupOptions.Parse(new[] { "--minimal-disk-boot", path }.Concat(more).ToArray(), AppContext.BaseDirectory);
    }
    private static uint Read(CopperScreenLightweightSession session, int address) => BinaryPrimitives.ReadUInt32BigEndian(session.Machine.ChipRam.Span.Slice(address, 4));
    private static void Run(CopperScreenLightweightSession session, int frames = 150)
    {
        for (var i = 0; i < frames; i++) session.RenderNextFrame(session.Framebuffer);
    }
    [Fact]
    public void OriginalBootblockReceivesIoRequestAndExecAndRunsWithoutRom()
    {
        var options = Options(Disk("""
            move.l a1,$800
            move.l a6,$804
            move.l $4,$808
            move.l #$12345678,$80c
            loop:
            bra loop
            """));
        Assert.Null(options.Error);
        Assert.Null(options.KickstartRomPath);
        using var session = new CopperScreenLightweightSession(options);
        Run(session);
        Assert.Equal(0x79000u, Read(session, 0x800));
        Assert.Equal(0x78000u, Read(session, 0x804));
        Assert.Equal(0x78000u, Read(session, 0x808));
        Assert.Equal(0x12345678u, Read(session, 0x80c));
    }
    [Fact]
    public void TrackdiskReadsCrossHeadAndCylinder()
    {
        var disk = Disk("""
            move.l a1,a3
            move.l #2048,d0
            move.l #$10002,d1
            jsr -198(a6)
            move.l d0,$800
            move.l d0,40(a3)
            move.l #2048,36(a3)
            move.l #10240,44(a3)
            move.l a3,a1
            jsr -456(a6)
            move.l d0,$804
            move.l 32(a3),$808
            move.l #$12345678,$80c
            loop:
            bra loop
            """);
        for (var i = 10240; i < 12288; i++) disk[i] = (byte)(i * 7 + (i >> 8));
        using var session = new CopperScreenLightweightSession(Options(disk));
        Run(session, 250);
        Assert.Equal(0x12345678u, Read(session, 0x80c));
        Assert.Equal(0u, Read(session, 0x804));
        Assert.Equal(2048u, Read(session, 0x808));
        Assert.Equal(disk.AsSpan(10240, 2048).ToArray(), session.Machine.ChipRam.Span.Slice((int)Read(session, 0x800), 2048).ToArray());
    }
    [Fact]
    public void LoadedSecondStageCanStillUseExecAndTrackdisk()
    {
        var disk = Disk("""
            move.l #$10000,40(a1)
            move.l #512,36(a1)
            move.l #5120,44(a1)
            jsr -456(a6)
            jmp $10000
            """);
        new CopperScreenBootAssembler(0x10000).Assemble("""
            move.l a1,a3
            move.l #512,d0
            moveq #2,d1
            jsr -198(a6)
            move.l d0,$800
            move.l d0,40(a3)
            move.l #512,36(a3)
            move.l #5632,44(a3)
            move.l a3,a1
            jsr -456(a6)
            move.l d0,$804
            move.l #$12345678,$80c
            loop:
            bra loop
            """).CopyTo(disk, 5120);
        for (var i = 5632; i < 6144; i++) disk[i] = (byte)(i * 13);
        using var session = new CopperScreenLightweightSession(Options(disk));
        Run(session, 250);
        Assert.Equal(0x12345678u, Read(session, 0x80c));
        Assert.Equal(0u, Read(session, 0x804));
        Assert.InRange(session.Machine.Cpu.ProgramCounter, 0x10000u, 0x10100u);
        Assert.Equal(disk.AsSpan(5632, 512).ToArray(), session.Machine.ChipRam.Span.Slice((int)Read(session, 0x800), 512).ToArray());
    }
    [Fact]
    public void FreeMemCoalescesAndMemoryHeaderStaysConsistent()
    {
        using var session = new CopperScreenLightweightSession(Options(Disk("""
            move.l #24,d0
            move.l #$10002,d1
            jsr -198(a6)
            move.l d0,a3
            move.l d0,$800
            move.l #40,d0
            jsr -198(a6)
            move.l d0,a4
            move.l a3,a1
            move.l #24,d1
            jsr -210(a6)
            move.l a4,a1
            move.l #40,d1
            jsr -210(a6)
            moveq #2,d1
            jsr -216(a6)
            move.l d0,$804
            move.l #64,d0
            jsr -198(a6)
            move.l d0,$808
            move.l #$12345678,$80c
            loop:
            bra loop
            """)));
        Run(session);
        Assert.Equal(0x12345678u, Read(session, 0x80c));
        Assert.Equal(0x69000u, Read(session, 0x804));
        Assert.Equal(Read(session, 0x800), Read(session, 0x808));
        var memoryHeader = (int)Read(session, 0x78000 + 322);
        memoryHeader = (int)Read(session, memoryHeader); // Default slow header precedes Chip.
        Assert.Equal(0x69000u - 64, Read(session, memoryHeader + 28));
        Assert.Equal(0x80000u, Read(session, memoryHeader + 24));
    }
    [Fact]
    public void SupervisorCallbackReturnsToUserBootContext()
    {
        using var session = new CopperScreenLightweightSession(Options(Disk("""
            lea supercode,a5
            jsr -30(a6)
            move.w sr,d0
            andi.l #$2000,d0
            move.l d0,$804
            move.l #$12345678,$80c
            loop:
            bra loop
            supercode:
            move.w sr,d0
            andi.l #$2000,d0
            move.l d0,$800
            rte
            """)));
        Run(session);
        Assert.Equal(0x2000u, Read(session, 0x800));
        Assert.Equal(0u, Read(session, 0x804));
        Assert.Equal(0x12345678u, Read(session, 0x80c));
    }
    [Theory]
    [InlineData(0x70000)]
    [InlineData(0x7c800)]
    public void ReadCanFillTakeoverLoaderMemoryOutsideResidentServices(int destination)
    {
        var disk = Disk($"move.l #${destination:x},40(a1)\nmove.l #5632,36(a1)\nmove.l #5632,44(a1)\njsr -456(a6)\nmove.l d0,$804\nmove.l #$12345678,$80c\nloop:\nbra loop");
        for (var i = 5632; i < 11264; i++) disk[i] = (byte)(i * 17);
        using var session = new CopperScreenLightweightSession(Options(disk));
        Run(session, 250);
        Assert.Equal(0u, Read(session, 0x804));
        Assert.Equal(0x12345678u, Read(session, 0x80c));
        Assert.Equal(disk.AsSpan(5632, 5632).ToArray(), session.Machine.ChipRam.Span.Slice(destination, 5632).ToArray());
    }
    [Fact]
    public void ResetReadsTheCurrentlyInsertedDisk()
    {
        using var session = new CopperScreenLightweightSession(Options(Disk("move.l #$11111111,$80c\nloop:\nbra loop")));
        Run(session);
        Assert.Equal(0x11111111u, Read(session, 0x80c));
        var replacement = Options(Disk("move.l #$22222222,$80c\nloop:\nbra loop")).DriveDiskPaths[0]!;
        Assert.True(session.InsertLoadedDisk(0, replacement, CopperScreenDiskImageArchive.LoadDiskImage(replacement), markChanged: true));
        session.Reset();
        Run(session);
        Assert.Equal(0x22222222u, Read(session, 0x80c));
    }
    [Fact]
    public void UnsupportedServiceReportsLvoAndCallerInsteadOfReturningSuccess()
    {
        using var session = new CopperScreenLightweightSession(Options(Disk("jsr -462(a6)\nloop:\nbra loop")));
        var error = Assert.Throws<NotSupportedException>(() => Run(session));
        Assert.Contains("LVO -462", error.Message);
        Assert.Contains("006810", error.Message);
        Assert.True(session.IsPaused);
    }
    [Theory]
    [InlineData("rts", "boot block returned")]
    [InlineData("dc.w $4afc", "guest exception")]
    public void BootFailuresAreVisible(string code, string message)
    {
        using var session = new CopperScreenLightweightSession(Options(Disk(code)));
        Assert.Contains(message, Assert.Throws<NotSupportedException>(() => Run(session)).Message);
    }
    [Fact]
    public void BadChecksumAndMissingDiskAreRejectedBeforeStartup()
    {
        var disk = Disk("loop:\nbra loop"); disk[42] ^= 1;
        Assert.Contains("checksum", Assert.Throws<NotSupportedException>(() => new CopperScreenLightweightSession(Options(disk))).Message);
        var options = CopperScreenStartupOptions.Parse(["--minimal-disk-boot"], AppContext.BaseDirectory);
        Assert.Contains("ADF startup disk", Assert.Throws<NotSupportedException>(() => new CopperScreenLightweightSession(options)).Message);
    }
    [Fact]
    public void BootChoiceRoundTripsWithoutLoadingRememberedNativeRom()
    {
        var draft = CopperScreenSettingsDraft.FromStartupOptions(Options(Disk("loop:\nbra loop")));
        draft.KickstartRomPath = Path.Combine(_directory, "remembered-kickstart.rom");
        draft.RomVersion = KickstartVersion.Kickstart31;
        var recent = new CopperScreenRecentSession(Path.Combine(_directory, "history"));
        recent.Save(draft, 100, false);
        var restored = Assert.IsType<CopperScreenSettingsDraft>(recent.LoadMachine(AppContext.BaseDirectory));
        Assert.Equal(CopperScreenKickstartSource.MinimalDiskBoot, restored.KickstartSource);
        Assert.Equal(draft.KickstartRomPath, restored.KickstartRomPath);
        Assert.Equal(KickstartVersion.Kickstart31, restored.RomVersion);
        Assert.Null(restored.ToStartupOptions(AppContext.BaseDirectory).KickstartRomPath);
        using var session = new CopperScreenLightweightSession(restored.ToStartupOptions(AppContext.BaseDirectory));
        Run(session, 60);
        Assert.Null(session.FaultMessage);
        var mixed = CopperScreenStartupOptions.Parse(["--minimal-disk-boot", "--rom", draft.KickstartRomPath], AppContext.BaseDirectory);
        Assert.Contains("one boot method", mixed.Error);
    }
    [Theory]
    [InlineData("AccurateM68020")]
    [InlineData("AccurateM68040")]
    public void UnqualifiedCpuIsRejected(string cpu)
    {
        Assert.Contains("68000", Assert.Throws<NotSupportedException>(() => new CopperScreenLightweightSession(Options(Disk("loop:\nbra loop"), "--cpu", cpu))).Message);
    }
    [Fact]
    public void WritesAndOverlappingReadsReportTheirBoundary()
    {
        using (var session = new CopperScreenLightweightSession(Options(Disk("move.w #3,28(a1)\njsr -456(a6)"))))
            Assert.Contains("trackdisk command 3", Assert.Throws<NotSupportedException>(() => Run(session)).Message);
        using (var session = new CopperScreenLightweightSession(Options(Disk("move.l #$78000,40(a1)\njsr -456(a6)"))))
            Assert.Contains("overlaps resident", Assert.Throws<NotSupportedException>(() => Run(session)).Message);
    }
    [Fact]
    public void PublicListVectorsUseTheExecRegisterAbi()
    {
        using var session = new CopperScreenLightweightSession(Options(Disk("""
            move.l #$904,$900
            clr.l $904
            move.l #$900,$908
            lea $900,a0
            lea $920,a1
            jsr -246(a6)
            lea $900,a0
            lea $940,a1
            jsr -246(a6)
            lea $920,a1
            jsr -252(a6)
            loop:
            bra loop
            """)));
        Run(session, 100);
        Assert.Equal(0x940u, Read(session, 0x900));
        Assert.Equal(0x940u, Read(session, 0x908));
        Assert.Equal(0x904u, Read(session, 0x940));
        Assert.Equal(0x900u, Read(session, 0x944));
    }
    public void Dispose() => Directory.Delete(_directory, true);
}
