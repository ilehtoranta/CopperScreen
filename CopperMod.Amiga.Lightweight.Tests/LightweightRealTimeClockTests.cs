using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightRealTimeClockTests
{
    private const uint Base = 0xDC0000;
    private static readonly DateTimeOffset Initial = new(2026, 6, 14, 21, 45, 37, TimeSpan.FromHours(3));

    private static LightweightA500Machine Machine(Func<DateTimeOffset>? now = null)
        => new(new() { RtcEnabled = true, RtcNowProvider = now ?? (() => Initial) });

    private static byte Read(LightweightA500Machine machine, int register, uint lane = 0)
    {
        var cycle = machine.Cycle;
        return machine.ReadByte(Base + (uint)register * 4 + lane, ref cycle, M68kBusAccessKind.CpuDataRead);
    }

    private static void Write(LightweightA500Machine machine, int register, byte value)
    {
        var cycle = machine.Cycle;
        machine.WriteByte(Base + (uint)register * 4, value, ref cycle, M68kBusAccessKind.CpuDataWrite);
    }

    // Expectations ported from Legacy AmigaRealTimeClockTests, CopperMod
    // 39b183d6. These certify Legacy compatibility, not physical RTC timing.
    [Fact]
    public void A501RegistersExposeLocalBcdDateAndDefault24HourControl()
    {
        using var machine = Machine();
        Assert.True(machine.RtcEnabled);
        byte[] expected = [7, 3, 5, 4, 1, 2, 4, 1, 6, 0, 6, 2, 0, 0, 0, 4];
        for (var register = 0; register < expected.Length; register++)
            Assert.Equal(expected[register], Read(machine, register));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(3u)]
    public void FourByteAliasesAndNibbleMaskMatchLegacy(uint lane)
    {
        using var machine = Machine();
        var cycle = machine.Cycle;
        machine.WriteByte(Base + 8 + lane, 0xF8, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(8, Read(machine, 2));
        Assert.Equal(4, Read(machine, 3, lane));
    }

    [Fact]
    public void WordAndLongAccessesKeepLegacyByteOrder()
    {
        using var machine = Machine();
        var cycle = machine.Cycle;
        Assert.Equal(0x0707, machine.ReadWord(Base, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(0x07070303u, machine.ReadLong(Base + 2, ref cycle, M68kBusAccessKind.CpuDataRead));
        machine.WriteWord(Base, 0x0109, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(9, Read(machine, 0)); // The second byte is the last write to the same register.
        machine.WriteLong(Base + 2, 0x00070002, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(7, Read(machine, 0));
        Assert.Equal(2, Read(machine, 1));
    }

    [Fact]
    public void GuestOffsetAdvancesWithHostTimeWithoutDeviceTicks()
    {
        var now = Initial;
        using var machine = Machine(() => now);
        Write(machine, 0, 9);
        Write(machine, 2, 8);
        now = now.AddSeconds(23);
        Assert.Equal(2, Read(machine, 0));
        Assert.Equal(0, Read(machine, 1));
        Assert.Equal(9, Read(machine, 2));
        Assert.Equal(4, Read(machine, 3));
        machine.AdvanceHardwareTo(1_000_000);
        Assert.Equal(2, Read(machine, 0)); // Emulated cycles cannot advance this battery clock.
    }

    [Theory]
    [InlineData(2, 6, 4)]
    [InlineData(7, 8, 0)]
    public void WrittenYearUsesLegacyAmiga1978Through2077Window(byte tens, byte ones, byte weekday)
    {
        using var machine = Machine(() => new(1978, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Write(machine, 11, tens);
        Write(machine, 10, ones);
        Assert.Equal(ones, Read(machine, 10));
        Assert.Equal(tens, Read(machine, 11));
        Assert.Equal(weekday, Read(machine, 12));
    }

    [Fact]
    public void MonthWriteClampsDayAndLeapDayRollsWithHostTime()
    {
        var now = new DateTimeOffset(2024, 1, 31, 23, 59, 59, TimeSpan.Zero);
        using var machine = Machine(() => now);
        Write(machine, 8, 2);
        Assert.Equal(9, Read(machine, 6));
        Assert.Equal(2, Read(machine, 7));
        now = now.AddSeconds(1);
        Assert.Equal(1, Read(machine, 6));
        Assert.Equal(3, Read(machine, 8));
    }

    [Theory]
    [InlineData(0, 2, 1)]
    [InlineData(12, 2, 5)]
    [InlineData(21, 9, 4)]
    public void TwelveHourReadbackIncludesPmBit(int hour, byte ones, byte tens)
    {
        using var machine = Machine(() => new(2026, 6, 14, hour, 0, 0, TimeSpan.Zero));
        Write(machine, 15, 0);
        Assert.Equal(ones, Read(machine, 4));
        Assert.Equal(tens, Read(machine, 5));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResetClearsControlsAndRetainsGuestOffset(bool external)
    {
        using var machine = Machine();
        Write(machine, 0, 9);
        Write(machine, 13, 0xFF);
        Write(machine, 14, 0xFE);
        Write(machine, 15, 0xFB);
        Assert.Equal(13, Read(machine, 13)); // Busy bit remains clear, as in Legacy.
        Assert.Equal(14, Read(machine, 14));
        if (external) machine.ResetExternalDevices(machine.Cycle);
        else machine.Reset();
        Assert.Equal(9, Read(machine, 0));
        Assert.Equal(0, Read(machine, 13));
        Assert.Equal(0, Read(machine, 14));
        Assert.Equal(4, Read(machine, 15));
    }

    [Fact]
    public void RestartCopiesOffsetWithoutSharingMutableClockState()
    {
        using var original = Machine();
        Write(original, 0, 9);
        using var replacement = original.CreateRestartCandidate(new() { RtcEnabled = true });
        Assert.Equal(9, Read(replacement, 0));
        Write(replacement, 0, 2);
        Assert.Equal(9, Read(original, 0));
        using var cold = Machine();
        Assert.Equal(7, Read(cold, 0));
        using var disabled = original.CreateRestartCandidate(new());
        Assert.False(disabled.RtcEnabled);
    }

    [Fact]
    public void OptionalClockDoesNotMapOutsideIts64ByteWindow()
    {
        using var machine = Machine();
        using var disabled = new LightweightA500Machine();
        Assert.False(disabled.RtcEnabled);
        var cycle = 0L;
        disabled.WriteWord(Base, 0, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(0xFFFF, disabled.ReadWord(Base, ref cycle, M68kBusAccessKind.CpuDataRead));
        cycle = 0;
        Assert.Equal(0xFF, machine.ReadByte(Base - 1, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(0xFF, machine.ReadByte(Base + 64, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(4, Read(machine, 15, 3));
    }

    [Fact]
    public void EnabledClockUsesExistingAgnusBusGrant()
    {
        using var rtc = Machine();
        using var chip = new LightweightA500Machine();
        long rtcCycle = 0, chipCycle = 0;
        _ = rtc.ReadByte(Base, ref rtcCycle, M68kBusAccessKind.CpuDataRead);
        _ = chip.ReadByte(0x1000, ref chipCycle, M68kBusAccessKind.CpuDataRead);
        Assert.True(rtcCycle > 0);
        Assert.Equal(chipCycle, rtcCycle);
    }

    [Theory]
    [InlineData(13, 1)] // CD.HOLD
    [InlineData(15, 6)] // CF.STOP + 24-hour mode
    public void CalendarWriteRetainsLeapDayUntilAllDigitsAreWritten(int control, byte heldValue)
    {
        using var machine = Machine();
        Write(machine, control, heldValue);
        Write(machine, 6, 9); Write(machine, 7, 2);
        Write(machine, 8, 2); Write(machine, 9, 0);
        Write(machine, 10, 8); Write(machine, 11, 2);
        Write(machine, control, control == 13 ? (byte)0 : (byte)4);
        Assert.Equal(9, Read(machine, 6));
        Assert.Equal(2, Read(machine, 7));
        Assert.Equal(2, Read(machine, 8));
        Assert.Equal(8, Read(machine, 10));
        Assert.Equal(2, Read(machine, 11));
        Assert.Equal(2, Read(machine, 12)); // Tuesday, 29 February 2028.
    }

    [Fact]
    public void HoldFreezesOneCalendarAndReleaseResumesHostProgress()
    {
        var now = Initial;
        using var machine = Machine(() => now);
        Write(machine, 13, 1);
        now = now.AddSeconds(90);
        Assert.Equal(7, Read(machine, 0));
        Assert.Equal(5, Read(machine, 2));
        Write(machine, 13, 0);
        now = now.AddSeconds(23);
        Assert.Equal(0, Read(machine, 0));
        Assert.Equal(0, Read(machine, 1));
        Assert.Equal(6, Read(machine, 2));
    }
}
