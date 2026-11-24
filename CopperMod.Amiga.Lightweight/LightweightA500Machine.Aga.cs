// Copyright (C) 2026 Ilkka Lehtoranta. SPDX-License-Identifier: MIT
using Copper68k;

namespace CopperMod.Amiga.Lightweight;

public sealed partial class LightweightA500Machine
{
    public bool IsAga => _configuration.AgnusModel == LightweightAgnusModel.Mos8374Alice;
    private readonly LightweightA1200Io? _a1200Io;

    // With no PCMCIA card fitted, configured Fast RAM owns any overlap with
    // the common-memory window. Attribute and I/O space begin above it.
    private bool IsA1200IoAddress(uint address)
        => LightweightA1200Io.ContainsAddress(address) &&
            (address >= LightweightA1200Io.PcmciaMemoryEndExclusive || _fastRam?.ContainsRange(address, 1) != true);

    // AGA Chip RAM, A1200 ROM and Akiko data accesses use a native long bus.
    // This bounded bridge retains the canonical motherboard clock and arbitration.
    internal bool HasAgaLongBus(uint address)
        => IsAga && (address < 0x1FFFFD ? !_overlayEnabled || _akiko is null :
            address is >= RomBase and <= 0xFFFFFC ? _akiko is null : IsAkikoAddress(address));

    internal uint ReadAgaLong(uint address, ref long cycle)
    {
        GrantCpuWord(address, cycle, out var grant, out var complete);
        AdvanceHardwareTo(grant);
        var value = IsAkikoAddress(address) ? ReadAkiko(address, 4, grant) :
            ((uint)ReadWordRaw(address) << 16) | ReadWordRaw(address + 2);
        AdvanceHardwareTo(complete);
        cycle = complete;
        return value;
    }

    internal void WriteAgaBusLanes(uint address, uint value, int size, ref long cycle)
    {
        GrantCpuWord(address, cycle, out var grant, out var complete);
        AdvanceHardwareTo(grant);
        if (IsAkikoAddress(address)) WriteAkiko(address, size, value, grant);
        else if (size == 4)
        {
            WriteWordRaw(address, (ushort)(value >> 16), grant);
            WriteWordRaw(address + 2, (ushort)value, grant);
        }
        else if ((address & 1) == 0)
        {
            WriteWordRaw(address, (ushort)(value >> 8), grant);
            WriteByteRaw(address + 2, (byte)value, grant);
        }
        else
        {
            WriteByteRaw(address, (byte)(value >> 16), grant);
            WriteWordRaw(address + 1, (ushort)value, grant);
        }
        AdvanceHardwareTo(complete);
        cycle = complete;
    }

    internal void OnBitplaneWideOutput(int plane, ulong value, int bits, long cycle)
        => _video.AcceptAgaBitplane(plane, value, bits, cycle);

    internal ulong ReadAgaBitplane(uint address, int mode)
    {
        if (mode == 0) return ReadChipWordBus(address);
        var aligned = address & ~3u;
        uint Long(uint a) => ((uint)ReadChipWordBus(a) << 16) | ReadChipWordBus(a + 2);
        if (mode != 3)
        {
            var value = Long(aligned);
            if ((address & 2) != 0) { value &= 0xFFFF; return value | value << 16; }
            if (mode == 2) { value &= 0xFFFF0000; return value | value >> 16; }
            return value;
        }
        var first = address & ~7u;
        if ((address & 4) != 0) first += 4;
        var second = (address & ~7u) + 4;
        var a = Long(first); var b = Long(second);
        if ((address & 2) != 0) { a = (a & 0xFFFF) * 0x10001u; b = (b & 0xFFFF) * 0x10001u; }
        return ((ulong)a << 32) | b;
    }
}
