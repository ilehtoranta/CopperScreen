// Copyright (C) 2026 Ilkka Lehtoranta. SPDX-License-Identifier: MIT
using Copper68k;

namespace CopperMod.Amiga.Lightweight;

public sealed partial class LightweightA500Machine
{
    public bool IsAga => _configuration.AgnusModel == LightweightAgnusModel.Mos8374Alice;
    private readonly LightweightA1200Io? _a1200Io;

    // A1200 RAM and ROM use a native long bus. Custom/CIA/Gayle remain narrower.
    // This bounded bridge retains the canonical motherboard clock and arbitration.
    internal bool HasAgaLongBus(uint address)
        => IsAga && (address < 0x1FFFFD || address is >= RomBase and <= 0xFFFFFC);

    internal uint ReadAgaLong(uint address, ref long cycle)
    {
        GrantCpuWord(address, cycle, out var grant, out var complete);
        AdvanceHardwareTo(grant);
        var value = ((uint)ReadWordRaw(address) << 16) | ReadWordRaw(address + 2);
        AdvanceHardwareTo(complete);
        cycle = complete;
        return value;
    }

    internal void WriteAgaLong(uint address, uint value, ref long cycle)
    {
        GrantCpuWord(address, cycle, out var grant, out var complete);
        AdvanceHardwareTo(grant);
        WriteWordRaw(address, (ushort)(value >> 16), grant);
        WriteWordRaw(address + 2, (ushort)value, grant);
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
