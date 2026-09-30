// Copyright (C) 2026 Ilkka Lehtoranta. SPDX-License-Identifier: MIT
// Adapted from Legacy DisplayGeometryDecoder; no scheduler or renderer dependency.
namespace CopperMod.Amiga.Lightweight;

internal readonly record struct LightweightDisplayWindow(int HorizontalStart, int HorizontalStop, int VerticalStart, int VerticalStop)
{
    internal static LightweightDisplayWindow Decode(ushort start, ushort stop, ushort high, bool extended)
    {
        var hs = start & 255;
        var he = stop & 255;
        var vs = start >> 8;
        var ve = stop >> 8;
        if (extended)
        {
            hs |= (high & 0x20) << 3;
            he |= (high & 0x2000) >> 5;
            vs |= (high & 15) << 8;
            ve |= high & 0x0F00;
            if (he <= hs) he += 512;
            if (ve <= vs) ve += 4096;
        }
        else
        {
            he += 256;
            if (ve < 128) ve += 256;
            if (ve <= vs) ve += 256;
        }
        return new(hs, he, vs, ve);
    }
}
