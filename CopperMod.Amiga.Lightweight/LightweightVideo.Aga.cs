// Copyright (C) 2026 Ilkka Lehtoranta. SPDX-License-Identifier: MIT
using System.Runtime.CompilerServices;

namespace CopperMod.Amiga.Lightweight;

internal sealed partial class LightweightVideo
{
    private readonly bool _aga;
    private readonly int[] _agaPalette = new int[256];
    private readonly ushort[] _agaHigh = new ushort[256], _agaLow = new ushort[256];
    private readonly ulong[] _agaData = new ulong[8], _agaIntermediate = new ulong[8], _agaShift = new ulong[8];
    private readonly ushort[] _agaDualPixels = new ushort[256];
    private ulong _agaPendingData;
    private int _agaPendingBits = 16, _agaBits = 16;
    private ushort _agaBplcon4 = 0x11, _agaFmode;

    private void ResetAga()
    {
        Array.Fill(_agaPalette, unchecked((int)0xFF000000));
        Array.Clear(_agaHigh); Array.Clear(_agaLow); Array.Clear(_agaData);
        Array.Clear(_agaIntermediate); Array.Clear(_agaShift);
        _agaPendingData = 0; _agaPendingBits = _agaBits = 16;
        _agaBplcon4 = 0x11; _agaFmode = 0;
        if (_aga) _ecsBplcon3 = 0x0C00;
        UpdateAgaDualPixels();
    }

    // Lisa's high-nibble write also initializes the low nibbles for RGB4
    // compatibility. LOCT subsequently replaces only the low half; BANK selects
    // one of eight blocks. No EHB palette aliases may overwrite entries 32..63.
    private void WriteAgaColor(int register, ushort value)
    {
        var index = register + ((_ecsBplcon3 >> 13) & 7) * 32;
        if ((_ecsBplcon3 & 0x200) != 0) _agaLow[index] = (ushort)(value & 0xFFF);
        else { _agaHigh[index] = value; _agaLow[index] = (ushort)(value & 0xFFF); }
        var high = _agaHigh[index]; var low = _agaLow[index];
        _agaPalette[index] = unchecked((int)0xFF000000) |
            (((high >> 8 & 15) * 16 + (low >> 8 & 15)) << 16) |
            (((high >> 4 & 15) * 16 + (low >> 4 & 15)) << 8) |
            ((high & 15) * 16 + (low & 15));
    }

    internal void AcceptAgaBitplane(int plane, ulong value, int bits, long outputCycle)
    {
        _pendingDataPlane = plane; _agaPendingData = value; _agaPendingBits = bits;
        _pendingDataCycle = outputCycle + 2; ActivateAfter(outputCycle);
    }

    private void ApplyAgaData(LightweightA500Machine machine)
    {
        var plane = _pendingDataPlane;
        _agaData[plane] = _agaPendingData;
        if (plane == 0)
        {
            Array.Copy(_agaData, _agaIntermediate, 8);
            _agaBits = _agaPendingBits; _pendingReloadParity = 3;
            _spriteOutputEnableLine = machine.BeamLine;
        }
    }

    private int ShiftAgaPixel(int x, int resolution)
    {
        var code = 0;
        for (var p = 0; p < 8; p++)
        {
            code |= (int)(_agaShift[p] >> 63) << p;
            _agaShift[p] <<= 1;
        }
        if (_pendingReloadParity != 0)
        {
            var scroll = _effectiveBplcon1;
            for (var parity = 0; parity < 2; parity++)
            {
                if ((_pendingReloadParity & (1 << parity)) == 0) continue;
                var fine = ((scroll >> (parity * 4) & 15) << 2) |
                    (scroll >> (8 + parity * 4) & 3) | ((scroll >> (10 + parity * 4) & 3) << 6);
                var delay = fine * resolution / 4 + resolution - 1;
                if ((x & (_agaBits - 1)) != (delay & (_agaBits - 1))) continue;
                for (var p = parity; p < _effectivePlaneCount; p += 2)
                {
                    var tailMask = _agaBits == 64 ? 0UL : ulong.MaxValue >> _agaBits;
                    _agaShift[p] = (_agaShift[p] & tailMask) | (_agaIntermediate[p] << (64 - _agaBits));
                }
                _pendingReloadParity &= ~(1 << parity);
            }
        }
        return code & _effectivePlaneMask;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void RenderAgaCck(int line, int x, LightweightA500Machine machine)
    {
        var resolution = (_effectiveBplcon0 & 0x8000) != 0 ? 2 : (_effectiveBplcon0 & 0x40) != 0 ? 4 : 1;
        var enabled = (_effectiveBplcon0 & 1) != 0;
        var dual = (_effectiveBplcon0 & 0x400) != 0;
        var ham = (_effectiveBplcon0 & 0x800) != 0 && (_effectivePlaneCount == 6 || _effectivePlaneCount == 8);
        var start = (line * _ecsLineClocks * 2 + x) * _outputScale;
        Span<int> codes = stackalloc int[8];
        Span<int> sprites = stackalloc int[8];
        Span<int> placements = stackalloc int[8];
        sprites.Fill(-1);
        for (var i = 0; i < resolution * 2; i++)
        {
            var code = codes[i] = ShiftAgaPixel(x * resolution + i, resolution);
            placements[i] = dual ? _agaDualPixels[code] >> 8 : code == 0 ? 4 : _effectiveSpritePlayfieldPlacement;
        }
        var spriteResolution = resolution == 4 && (_ecsBplcon3 & 0xC0) == 0 ? 2 : 1;
        for (var i = 0; i < resolution * 2;)
        {
            var lowX = x + i / resolution;
            if (InEcsWindow(line, lowX) && IsSpriteOutputEnabled(line))
            {
                if (resolution == 1)
                    sprites[i] = machine.ComposeSpriteColorIndex(line, lowX, 1, placements[i], codes[i] & 63);
                else
                {
                    // One sprite sample covers two or four playfield samples.
                    // Resolve each priority separately but advance its shifter
                    // once, including when only planes 7/8 are opaque.
                    var count = resolution / spriteResolution;
                    var placement = count == 4 ? Math.Max(placements[i], placements[i + 1]) : placements[i];
                    var second = count == 4 ? Math.Max(placements[i + 2], placements[i + 3]) : placements[i + 1];
                    machine.ComposeAgaSpriteColorIndexes(line,
                        spriteResolution == 2 ? x * 2 + i / 2 : lowX,
                        placement, second, codes[i] & 63, codes[i + count / 2] & 63,
                        out var a, out var b, out var group,
                        count == 4 ? codes[i + 1] & 63 : -1, count == 4 ? codes[i + 3] & 63 : -1);
                    sprites[i] = a; sprites[i + count / 2] = b;
                    if (count == 4)
                    {
                        // The shared 140 ns sprite sample is independently
                        // masked at each 35 ns playfield pixel.
                        sprites[i + 1] = a; sprites[i + 3] = b;
                        for (var j = 0; j < 4; j++)
                            if (group >= placements[i + j]) sprites[i + j] = -1;
                    }
                }
            }
            i += resolution == 1 ? 1 : resolution / spriteResolution;
        }
        for (var i = 0; i < resolution * 2; i++)
        {
            var lowX = x + i / resolution;
            var code = codes[i];
            var inside = InEcsWindow(line, lowX);
            var sprite = sprites[i];
            var index = (dual ? _agaDualPixels[code] & 255 : code) ^ (_agaBplcon4 >> 8);
            var color = _agaPalette[index];
            if (ham && inside) color = DecodeAgaHam(code);
            else if (_effectivePlaneCount == 6 && !dual && !ham &&
                (_ecsBplcon2 & 0x200) == 0 && (index & 32) != 0)
                color = unchecked((int)0xFF000000) | ((_agaPalette[index & 31] & 0xFEFEFE) >> 1);
            if (sprite >= 0) color = _agaPalette[(sprite & 15) | ((_agaBplcon4 & 15) << 4)];
            if (!inside)
            {
                _hamColor = _agaPalette[0];
                color = enabled && (_ecsBplcon3 & 0x20) != 0 ? unchecked((int)0xFF000000) : _agaPalette[0];
                if (enabled && (_ecsBplcon3 & 0x10) == 0) color &= 0xFFFFFF;
            }
            if (machine.IsRasterBlanked(line, lowX) || enabled && (_ecsBplcon3 & 1) != 0 && _externalBlank)
                color = unchecked((int)0xFF000000);
            var repeats = Math.Max(1, _outputScale / resolution);
            if (_outputScale >= resolution)
                for (var j = 0; j < repeats; j++) _rendering[start + i * repeats + j] = color;
            else if (i % (resolution / _outputScale) == 0)
                _rendering[start + i / (resolution / _outputScale)] = color;
        }
    }

    private int DecodeAgaHam(int code)
    {
        var addressed = code ^ (_agaBplcon4 >> 8);
        if (_effectivePlaneCount == 8)
        {
            // AA functional specification p.4: BP2/BP1 select direct/B/R/G;
            // modification replaces only the high six bits of a component.
            _hamColor = (addressed & 3) switch
            {
                0 => _agaPalette[addressed >> 2],
                1 => (_hamColor & ~0xFC) | (code & 0xFC),
                2 => (_hamColor & ~0xFC0000) | ((code & 0xFC) << 16),
                _ => (_hamColor & ~0xFC00) | ((code & 0xFC) << 8)
            };
        }
        else
        {
            var component = (code & 15) * 17;
            _hamColor = (addressed & 0x30) switch
            {
                0 => _agaPalette[addressed & 15],
                0x10 => (_hamColor & ~255) | component,
                0x20 => (_hamColor & ~0xFF0000) | (component << 16),
                _ => (_hamColor & ~0xFF00) | (component << 8)
            };
        }
        return _hamColor;
    }

    private void UpdateAgaDualPixels()
    {
        var priority1 = Math.Min(_ecsBplcon2 & 7, 4);
        var priority2 = Math.Min(_ecsBplcon2 >> 3 & 7, 4);
        var pf2First = (_ecsBplcon2 & 0x40) != 0;
        var selectedBit = _ecsBplcon3 >> 10 & 7;
        var offset = selectedBit == 0 ? 0 : 1 << selectedBit;
        for (var code = 0; code < 256; code++)
        {
            var a = (code & 1) | (code >> 1 & 2) | (code >> 2 & 4) | (code >> 3 & 8);
            var b = (code >> 1 & 1) | (code >> 2 & 2) | (code >> 3 & 4) | (code >> 4 & 8);
            var index = b != 0 && (pf2First || a == 0) ? b + offset : a;
            var placement = Math.Min(a != 0 ? priority1 : 4, b != 0 ? priority2 : 4);
            _agaDualPixels[code] = (ushort)(index | placement << 8);
        }
    }

    private void CheckAgaMode()
    {
        if ((_effectiveBplcon0 & 0xC00) == 0xC00) UnsupportedActiveFeature = "AGA combined HAM/dual playfield (unverified)";
        else if ((_effectiveBplcon0 & 0x800) != 0 && _effectivePlaneCount != 0 && _effectivePlaneCount != 6 && _effectivePlaneCount != 8)
            UnsupportedActiveFeature = "AGA HAM with nonstandard plane count (unverified)";
        else if ((_effectiveBplcon0 & 0x400) != 0 && ((_ecsBplcon2 & 7) >= 5 || (_ecsBplcon2 >> 3 & 7) >= 5))
            UnsupportedActiveFeature = "AGA nonstandard dual-playfield priority (unverified)";
        else if ((_ecsBplcon2 & 0x100) != 0) UnsupportedActiveFeature = "AGA palette readback (next milestone)";
        else if ((_agaFmode & 0xC00C) != 0) UnsupportedActiveFeature = "AGA wide sprites/scan doubling (next milestone)";
        else if ((_agaBplcon4 & 15) != ((_agaBplcon4 >> 4) & 15)) UnsupportedActiveFeature = "AGA independent even/odd sprite palette banks (next milestone)";
        // SPRES=01 explicitly selects the existing 140 ns sprite path.
        else if ((_ecsBplcon3 & 0x80) != 0) UnsupportedActiveFeature = "AGA higher sprite resolution (next milestone)";
    }
}
