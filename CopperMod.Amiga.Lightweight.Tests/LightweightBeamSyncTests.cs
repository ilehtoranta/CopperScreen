using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightBeamSyncTests
{
    // Independent expectation: vAmigaTS ersy2, A500 OCS5 photograph and
    // source at 0489f55d22ef7560d924a304e30998aea6864264: $40xx -> $4000.
    [Fact]
    public void MissingExternalHsyncHoldsBothCountersAndCpuCanReleaseIt()
    {
        using var m = new LightweightA500Machine();
        m.AdvanceHardwareTo(64 * 454 + 360);
        Write(m, 0x100, 2);
        m.AdvanceHardwareTo(65 * 454 - 2);
        Assert.True(m.BeamSyncRunning);
        Assert.Equal(226, m.BeamColorClock);
        m.AdvanceHardwareTo(65 * 454);
        Assert.False(m.BeamSyncRunning);
        Assert.Equal(64, m.BeamLine);
        for (var i = 0; i < 16; i++)
        {
            var cycle = m.Cycle;
            var value = m.ReadWord(0xDFF006, ref cycle, M68kBusAccessKind.CpuDataRead);
            Assert.Equal(0x4000, value);
            Assert.Equal(0, m.BeamColorClock);
        }
        var writeCycle = m.Cycle;
        m.WriteWord(0xDFF100, 0, ref writeCycle, M68kBusAccessKind.CpuDataWrite);
        Assert.True(m.BeamSyncRunning);
        Assert.Equal(64, m.BeamLine);
        Assert.True(m.BeamColorClock > 0);
        var nextLine = m.Cycle + 454 - m.BeamColorClock * 2;
        m.AdvanceHardwareTo(nextLine);
        Assert.Equal(65, m.BeamLine);
        Assert.Equal(0, m.BeamColorClock);
    }

    [Fact]
    public void ExternalSyncToggleWithinLinePreservesOrdinaryOutput()
    {
        using var control = new LightweightA500Machine();
        using var toggled = new LightweightA500Machine();
        control.AdvanceHardwareTo(64 * 454 + 200);
        Write(control, 0x100, 0);
        toggled.AdvanceHardwareTo(64 * 454 + 200);
        Write(toggled, 0x100, 2);
        toggled.AdvanceHardwareTo(64 * 454 + 300);
        Write(toggled, 0x100, 0);
        control.AdvanceHardwareTo(313 * 454);
        toggled.AdvanceHardwareTo(313 * 454);
        Assert.True(toggled.BeamSyncRunning);
        Assert.Equal(control.CompletedFrames, toggled.CompletedFrames);
        Assert.Equal(control.Framebuffer.ToArray(), toggled.Framebuffer.ToArray());
        Assert.Equal(control.AudioSamples.ToArray(), toggled.AudioSamples.ToArray());
        Assert.Equal(control.Intreq, toggled.Intreq);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongHoldReturnsBoundedOutputWithoutInventingVerticalBlank(bool stoppedCpu)
    {
        using var m = new LightweightA500Machine();
        if (stoppedCpu)
        {
            m.WriteChipWordDma(0x1000, 0x4E72); // STOP #$2700
            m.WriteChipWordDma(0x1002, 0x2700);
        }
        m.Reset();
        Write(m, 0x100, 2);
        for (var i = 1; i <= 4; i++)
        {
            m.ExecuteFrame();
            Assert.Equal(i, m.CompletedFrames);
            var deadline = i * LightweightA500Machine.MaximumOutputIntervalCycles;
            Assert.InRange(m.Cycle, deadline, deadline + 32);
            Assert.False(m.BeamSyncRunning);
            Assert.Equal(0, m.BeamLine);
            Assert.Equal(0, m.Intreq & 0x20);
            Assert.InRange(m.AudioSamples.Length, 2020, 2022);
        }
        Write(m, 0x100, 0);
        for (var i = 0; i < 3; i++) m.ExecuteFrame();
        Assert.True(m.BeamSyncRunning);
        Assert.NotEqual(0, m.Intreq & 0x20);
    }

    [Fact]
    public void TodStopsButCiaTimerContinuesDuringMissingSync()
    {
        using var m = new LightweightA500Machine();
        CiaWrite(m, 0xBFE801, 0); // CIA-A TOD low starts counter.
        CiaWrite(m, 0xBFD800, 0); // CIA-B TOD low.
        CiaWrite(m, 0xBFD400, 0xFF);
        CiaWrite(m, 0xBFD500, 0xFF);
        CiaWrite(m, 0xBFDD00, 0x81); // Make Timer A's deadline active.
        CiaWrite(m, 0xBFDE00, 0x11); // Timer A start + load.
        Write(m, 0x100, 2);
        m.AdvanceHardwareTo(800_000);
        Assert.Equal(0u, m.CiaATod);
        Assert.Equal(0u, m.CiaBTod);
        Assert.NotEqual(0, m.CiaBPendingInterrupts & 1);
        Assert.Equal(0, m.Intreq & 0x20);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void AudioDmaResumesAtBeamRelativeSlotAfterNonIntegralLineHold(int channel)
    {
        using var m = new LightweightA500Machine();
        var offset = (ushort)(0xA0 + channel * 16);
        Write(m, (ushort)(offset + 2), 0x2000);
        Write(m, (ushort)(offset + 4), 4);
        Write(m, (ushort)(offset + 6), 8);
        Write(m, 0x96, (ushort)(0x8200 | 1 << channel));
        m.AdvanceHardwareTo(400);
        Write(m, 0x100, 2);
        m.AdvanceHardwareTo(1000);
        Assert.Equal(0x2002u, m.GetPaulaChannelSnapshot(channel).CurrentAddress);
        Assert.Equal(long.MaxValue, m.GetPaulaChannelSnapshot(channel).DmaInputCycle);
        Write(m, 0x100, 0);
        var input = 1000 + 30 + 4 * channel;
        Assert.Equal(input, m.GetPaulaChannelSnapshot(channel).DmaInputCycle);
        m.AdvanceHardwareTo(input + 2);
        Assert.Equal(0x2004u, m.GetPaulaChannelSnapshot(channel).CurrentAddress);
        Assert.Equal(16 + channel * 2, m.BeamColorClock);
    }

    [Fact]
    public void ResetReleasesMissingSyncAndRetainsCanonicalTime()
    {
        using var m = new LightweightA500Machine();
        Write(m, 0x100, 2);
        m.AdvanceHardwareTo(1000);
        m.ResetExternalDevices(1000);
        Assert.True(m.BeamSyncRunning);
        Assert.Equal(1000, m.Cycle);
        Write(m, 0xA4, 1);
        Write(m, 0x96, 0x8201);
        Assert.Equal(1030, m.GetPaulaChannelSnapshot(0).DmaInputCycle);
        m.AdvanceHardwareTo(1454);
        Assert.Equal(1, m.BeamLine);
        m.Reset();
        Assert.Equal(0, m.Cycle);
        Write(m, 0x96, 0x8201);
        Assert.Equal(30, m.GetPaulaChannelSnapshot(0).DmaInputCycle);
    }

    [Fact]
    public void OddCpuPhaseReleaseDoesNotMoveDmaOffPhysicalCckBoundaries()
    {
        using var m = new LightweightA500Machine();
        Write(m, 0x100, 2);
        m.AdvanceHardwareTo(1001);
        Write(m, 0x100, 0);
        Write(m, 0x96, 0x8201);
        Assert.Equal(1030, m.GetPaulaChannelSnapshot(0).DmaInputCycle);
        m.AdvanceHardwareTo(1454);
        Assert.Equal(1, m.BeamLine);
        Assert.Equal(0, m.BeamColorClock);
    }

    [Fact]
    public void EnablingExternalSyncDoesNotRevokeAcceptedAudioTransfer()
    {
        using var m = new LightweightA500Machine();
        m.WriteChipWordDma(0x2000, 0xCAFE);
        Write(m, 0xA2, 0x2000);
        Write(m, 0xA4, 2);
        Write(m, 0x96, 0x8201);
        m.AdvanceHardwareTo(30);
        Write(m, 0x100, 2);
        m.AdvanceHardwareTo(32);
        Assert.Equal((ushort)0xCAFE, m.GetPaulaChannelSnapshot(0).PendingData);
        Assert.Equal(0x2002u, m.GetPaulaChannelSnapshot(0).CurrentAddress);
        Assert.True(m.BeamSyncRunning);
    }

    [Fact]
    public void OddCpuPhaseBitplaneControlInputDoesNotLeaveAnUnreachableDeadline()
    {
        using var m = new LightweightA500Machine();
        m.AdvanceHardwareTo(1);
        Write(m, 0x100, 0x1000);
        Assert.Equal(4, m.BitplaneNextCycle);
        m.AdvanceHardwareTo(2);
        Assert.Equal((ushort)0, m.BitplaneEffectiveBplcon0);
        m.AdvanceHardwareTo(4);
        Assert.Equal((ushort)0x1000, m.BitplaneEffectiveBplcon0);
        Assert.Equal(long.MaxValue, m.BitplaneNextCycle);
    }

    [Fact]
    public void DiskAndSpriteSlotsRestartAtTheirBeamPhase()
    {
        using var m = new LightweightA500Machine();
        m.AdvanceHardwareTo(25 * 454 + 400);
        Write(m, 0x100, 2);
        m.AdvanceHardwareTo(26 * 454 + 102);
        Write(m, 0x120, 1); // Sprite 0 at $10000.
        Write(m, 0x122, 0);
        Write(m, 0x96, 0x8230);
        Write(m, 0x20, 1); // Disk buffer at $10000.
        Write(m, 0x22, 0);
        Write(m, 0x24, 0xC004);
        Write(m, 0x24, 0xC004);
        m.AdvanceHardwareTo(m.Cycle + 100);
        Assert.Equal(long.MaxValue, m.SpriteDmaNextCycle);
        Assert.Equal(0x10000u, m.GetDiskPointer());
        var resume = m.Cycle;
        Write(m, 0x100, 0);
        Assert.Equal(resume + 46, m.SpriteDmaNextCycle);
        m.AdvanceHardwareTo(resume + 14);
        Assert.Equal(0x10002u, m.GetDiskPointer());
        m.AdvanceHardwareTo(resume + 48);
        Assert.Equal(resume + 48, m.SpriteDmaLastOutputCycle);
        Assert.Equal(0, m.SpriteDmaLastChannel);
    }

    [Fact]
    public void BlitterDoesNotRunWhileBeamHeldAndResumesAfterRelease()
    {
        using var m = new LightweightA500Machine();
        Write(m, 0x100, 2);
        m.AdvanceHardwareTo(500);
        Write(m, 0x40, 0x0100); // D-only zero minterm, one word.
        Write(m, 0x54, 1);
        Write(m, 0x56, 0);
        Write(m, 0x96, 0x8240);
        Write(m, 0x58, 0x0041);
        m.WriteChipWordDma(0x10000, 0xCAFE);
        m.AdvanceHardwareTo(1000);
        Assert.Equal((ushort)0xCAFE, m.ReadChipWordDma(0x10000));
        Assert.True(m.BlitterActive);
        Write(m, 0x100, 0);
        m.AdvanceHardwareTo(1100);
        Assert.Equal((ushort)0, m.ReadChipWordDma(0x10000));
        Assert.False(m.BlitterActive);
    }

    [Fact]
    public void CoarseAndSingleCpuCycleAdvancementAgreeAcrossRepeatedHolds()
    {
        using var coarse = new LightweightA500Machine();
        using var fine = new LightweightA500Machine();
        foreach (var target in new long[] { 100, 1001, 1200, 2303, 2500, 4001 })
        {
            coarse.AdvanceHardwareTo(target);
            while (fine.Cycle < target) fine.AdvanceHardwareTo(fine.Cycle + 1);
            var value = (ushort)(coarse.GetCustomRegister(0x100) ^ 2);
            Write(coarse, 0x100, value);
            Write(fine, 0x100, value);
            Assert.Equal(coarse.BeamLine, fine.BeamLine);
            Assert.Equal(coarse.BeamColorClock, fine.BeamColorClock);
            Assert.Equal(coarse.NextFrameCycle, fine.NextFrameCycle);
        }
        coarse.AdvanceHardwareTo(300_000);
        while (fine.Cycle < 300_000) fine.AdvanceHardwareTo(fine.Cycle + 1);
        Assert.Equal(coarse.CompletedFrames, fine.CompletedFrames);
        Assert.Equal(coarse.Framebuffer.ToArray(), fine.Framebuffer.ToArray());
        Assert.Equal(coarse.AudioSamples.ToArray(), fine.AudioSamples.ToArray());
    }

    [Fact]
    public void ResumingSyncDoesNotInventAnEarlyPeriodModulationRequest()
    {
        using var m = new LightweightA500Machine();
        Write(m, 0xA2, 0x2000);
        Write(m, 0xA4, 4);
        Write(m, 0xA6, 2000);
        Write(m, 0x9E, 0x8010); // Channel zero period modulation: request on low byte.
        Write(m, 0x96, 0x8201);
        m.AdvanceHardwareTo(500); // First real word starts at 488; high byte lasts 4,000 cycles.
        Assert.Equal(long.MaxValue, m.GetPaulaChannelSnapshot(0).DmaInputCycle);
        Write(m, 0x100, 2);
        m.AdvanceHardwareTo(1000);
        Write(m, 0x100, 0);
        Assert.Equal(long.MaxValue, m.GetPaulaChannelSnapshot(0).DmaInputCycle);
        m.AdvanceHardwareTo(4486);
        Assert.Equal(0x2004u, m.GetPaulaChannelSnapshot(0).CurrentAddress);
        m.AdvanceHardwareTo(4488);
        Assert.NotEqual(long.MaxValue, m.GetPaulaChannelSnapshot(0).DmaInputCycle);
    }

    [Fact]
    public void SustainedSyncLossDoesNotAllocateDuringOutputDelivery()
    {
        using var m = new LightweightA500Machine();
        m.WriteChipWordDma(0x1000, 0x4E72);
        m.WriteChipWordDma(0x1002, 0x2700);
        m.Reset();
        Write(m, 0x100, 2);
        for (var i = 0; i < 4; i++) m.ExecuteFrame();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 60; i++) m.ExecuteFrame();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    private static void Write(LightweightA500Machine m, ushort offset, ushort value)
        => m.WriteCustomRegisterFromCopper(offset, value, m.Cycle);

    private static void CiaWrite(LightweightA500Machine m, uint address, byte value)
    {
        var cycle = m.Cycle;
        m.WriteByte(address, value, ref cycle, M68kBusAccessKind.CpuDataWrite);
    }
}
