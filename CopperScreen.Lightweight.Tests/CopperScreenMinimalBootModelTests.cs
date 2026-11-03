using System.Buffers.Binary;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class CopperScreenMinimalBootTests
{
    private CopperScreenStartupOptions ModelOptions(int model, byte[] disk)
    {
        var original = Options(disk);
        var draft = CopperScreenLaunchWorkflow.SelectStartChoice(new(), 6 + model);
        draft.DriveDiskPaths[0] = original.DriveDiskPaths[0];
        return draft.ToStartupOptions(AppContext.BaseDirectory);
    }

    [Theory]
    [InlineData(0, 524288)]
    [InlineData(1, 1048576)]
    [InlineData(2, 2097152)]
    public void ModelBootUsesGuestDmaAndCorrectSupervisorFrames(int model, int chipBytes)
    {
        var disk = Disk("""
            move.l a1,$800
            move.l a6,$804
            lea privileged,a5
            jsr -30(a6)
            move.l #$12345678,$80c
            loop:
            bra loop
            privileged:
            move.w sr,$810
            move.w (sp),$812
            move.w 6(sp),$814
            move.l sp,$818
            rte
            """);
        using var session = new CopperScreenLightweightSession(ModelOptions(model, disk));
        Run(session, 200);
        Assert.Equal(chipBytes, session.Machine.ChipRam.Length);
        Assert.Equal(model == 2 ? Copper68k.M68kCpuModel.M68EC020 : Copper68k.M68kCpuModel.M68000, session.Machine.CpuModel);
        Assert.Equal(model == 2, session.Machine.IsAga);
        Assert.Equal(0x79000u, Read(session, 0x800));
        Assert.Equal(0x78000u, Read(session, 0x804));
        Assert.Equal(0x12345678u, Read(session, 0x80c));
        Assert.Equal(0x20000000u, Read(session, 0x810) & 0x20002000u); // Live supervisor SR, stacked user SR.
        Assert.Equal(0, session.Machine.Cpu.StatusRegister & 0x2000);
        Assert.Equal(model == 2 ? 0x3f8u : 0x3fau, Read(session, 0x818));
        if (model == 2) Assert.Equal(0x0080, BinaryPrimitives.ReadUInt16BigEndian(session.Machine.ChipRam.Span.Slice(0x814, 2)));
        Assert.Equal((uint)chipBytes, Read(session, 0x78000 + 62));
        Assert.Equal(model == 2 ? 3 : 0, BinaryPrimitives.ReadUInt16BigEndian(session.Machine.ChipRam.Span.Slice(0x78000 + 296, 2)));
        Assert.Null(session.FaultMessage);
        Assert.Null(session.Machine.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void TakeoverVerticalBlankInterruptsReturnToBootCode(int model)
    {
        var disk = Disk("""
            lea takeover,a5
            jsr -30(a6)
            loop:
            addi.l #1,$80c
            bra loop
            takeover:
            move.l #vblank,$6c
            move.w #$7fff,$dff09c
            move.w #$c020,$dff09a
            rte
            vblank:
            addi.l #1,$800
            move.l sp,$804
            move.w #$0f0,$dff180
            move.w #$20,$dff09c
            move.w #$20,$dff09c
            rte
            """);
        using var session = new CopperScreenLightweightSession(ModelOptions(model, disk));
        Run(session, 200);
        Assert.InRange(Read(session, 0x800), 100u, 200u);
        Assert.Equal(model == 2 ? 0x3f8u : 0x3fau, Read(session, 0x804));
        var progress = Read(session, 0x80c);
        Run(session, 10);
        Assert.True(Read(session, 0x80c) > progress);
        Assert.Null(session.FaultMessage);
    }

    [Fact]
    public void A500InstructionStreamRetainsFrozenIdentity()
    {
        var code = new CopperScreenBootAssembler(CopperScreenMinimalBoot.RomBase).Assemble(CopperScreenMinimalBoot.BuildSource());
        Assert.Equal("D6141AEAC4380561FA1831EB5D15BF7C064CF2476F3D623D441BA4F88A4ACE48", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(code)));
    }

    [Theory]
    [InlineData(1, 1048576)]
    [InlineData(2, 2097152)]
    public void ExtendedChipAllocationClearFreeAndCoalescingRespectResidentMemory(int model, int chipBytes)
    {
        var upperBytes = chipBytes - 0x80000;
        var disk = Disk($"""
            move.l #$69000,d0
            moveq #2,d1
            jsr -198(a6)
            move.l d0,$800
            move.l #${upperBytes:x},d0
            move.l #$10002,d1
            jsr -198(a6)
            move.l d0,$804
            move.l d0,a3
            move.l ${chipBytes - 4:x},$808
            move.l #$aabbccdd,${chipBytes - 4:x}
            move.l a3,a1
            move.l #${upperBytes:x},d1
            jsr -210(a6)
            moveq #24,d0
            moveq #2,d1
            jsr -198(a6)
            move.l d0,a3
            moveq #40,d0
            jsr -198(a6)
            move.l d0,a4
            move.l a3,a1
            moveq #24,d1
            jsr -210(a6)
            move.l a4,a1
            moveq #40,d1
            jsr -210(a6)
            moveq #2,d1
            jsr -216(a6)
            move.l d0,$810
            moveq #64,d0
            jsr -198(a6)
            move.l d0,$814
            move.l #$12345678,$80c
            loop:
            bra loop
            """);
        using var session = new CopperScreenLightweightSession(ModelOptions(model, disk));
        Run(session, 400);
        Assert.Equal(0x12345678u, Read(session, 0x80c));
        Assert.Equal(0x7000u, Read(session, 0x800));
        Assert.Equal(0x80000u, Read(session, 0x804));
        Assert.Equal(0u, Read(session, 0x808)); // CLEAR reached the last fitted word.
        Assert.Equal(0xaabbccddu, Read(session, chipBytes - 4));
        Assert.Equal((uint)upperBytes, Read(session, 0x810));
        Assert.Equal(0x80000u, Read(session, 0x814));
        var header = (int)Read(session, 0x78000 + 322);
        Assert.Equal((uint)chipBytes, Read(session, header + 24));
        Assert.Equal((uint)upperBytes - 64, Read(session, header + 28));
        Assert.Null(session.FaultMessage);
    }

    [Theory]
    [InlineData(1, 1048576)]
    [InlineData(2, 2097152)]
    public void GuestDmaLoadsExecutableSecondStageAtTopOfChipRamAcrossHeadsAndCylinders(int model, int chipBytes)
    {
        var destination = chipBytes - 2048;
        var disk = Disk($"""
            move.l #${destination:x},40(a1)
            move.l #2048,36(a1)
            move.l #10240,44(a1)
            jsr -456(a6)
            move.l d0,$800
            move.l 32(a1),$804
            jmp ${destination:x}
            """);
        for (var i = 10240; i < 12288; i++) disk[i] = (byte)(i * 7 + (i >> 8));
        new CopperScreenBootAssembler((uint)destination).Assemble("""
            moveq #64,d0
            move.l #$10002,d1
            jsr -198(a6)
            move.l d0,$808
            move.l #$12345678,$80c
            loop:
            bra loop
            """).CopyTo(disk, 10240);
        using var session = new CopperScreenLightweightSession(ModelOptions(model, disk));
        Run(session, 300);
        Assert.Equal(0x12345678u, Read(session, 0x80c));
        Assert.Equal(0u, Read(session, 0x800));
        Assert.Equal(2048u, Read(session, 0x804));
        Assert.Equal(0x7000u, Read(session, 0x808));
        Assert.Equal(disk.AsSpan(10240, 2048).ToArray(), session.Machine.ChipRam.Span.Slice(destination, 2048).ToArray());
        Assert.InRange(session.Machine.Cpu.ProgramCounter, (uint)destination, (uint)destination + 512);
        Assert.Null(session.FaultMessage);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ExtendedModelsResetAndRereadReplacementDisk(int model)
    {
        using var session = new CopperScreenLightweightSession(ModelOptions(model, Disk("move.l #$11111111,$80c\nloop:\nbra loop")));
        Run(session, 200);
        Assert.Equal(0x11111111u, Read(session, 0x80c));
        var replacement = ModelOptions(model, Disk("move.l #$22222222,$80c\nloop:\nbra loop")).DriveDiskPaths[0]!;
        Assert.True(session.InsertLoadedDisk(0, replacement, CopperScreenDiskImageArchive.LoadDiskImage(replacement), markChanged: true));
        session.Reset(); Run(session, 200);
        Assert.Equal(0x22222222u, Read(session, 0x80c));
        Assert.Null(session.FaultMessage);
    }

    [Theory]
    [InlineData(1, "jsr -462(a6)", "LVO -462")]
    [InlineData(2, "jsr -462(a6)", "LVO -462")]
    [InlineData(1, "move.l #$78000,40(a1)\njsr -456(a6)", "overlaps resident")]
    [InlineData(2, "move.l #$78000,40(a1)\njsr -456(a6)", "overlaps resident")]
    [InlineData(1, "rts", "boot block returned")]
    [InlineData(2, "rts", "boot block returned")]
    [InlineData(1, "dc.w $4afc", "guest exception at $00680C")]
    [InlineData(2, "dc.w $4afc", "guest exception at $00680C")]
    public void ExtendedModelsRetainExplicitBootFaults(int model, string code, string message)
    {
        using var session = new CopperScreenLightweightSession(ModelOptions(model, Disk(code)));
        Assert.Contains(message, Assert.Throws<NotSupportedException>(() => Run(session, 200)).Message);
        Assert.True(session.IsPaused);
    }
}
