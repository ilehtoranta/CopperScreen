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
        // RDRAM changes Lisa's colour-table access direction even when the
        // initiating CPU/Copper transaction is a write.
        if ((_ecsBplcon2 & 0x100) != 0) return;
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
        Span<int> colors = stackalloc int[8];
        Span<int> placements = stackalloc int[8];
        for (var i = 0; i < resolution * 2; i++)
        {
            var code = codes[i] = ShiftAgaPixel(x * resolution + i, resolution);
            placements[i] = dual ? _agaDualPixels[code] >> 8 : code == 0 ? 4 : _effectiveSpritePlayfieldPlacement;
        }
        for (var i = 0; i < resolution * 2; i++)
        {
            var lowX = x + i / resolution;
            var code = codes[i];
            var inside = InEcsWindow(line, lowX);
            var index = (dual ? _agaDualPixels[code] & 255 : code) ^ (_agaBplcon4 >> 8);
            var color = _agaPalette[index];
            if (ham && inside) color = DecodeAgaHam(code);
            else if (_effectivePlaneCount == 6 && !dual && !ham &&
                (_ecsBplcon2 & 0x200) == 0 && (index & 32) != 0)
                color = unchecked((int)0xFF000000) | ((_agaPalette[index & 31] & 0xFEFEFE) >> 1);
            if (!inside)
            {
                _hamColor = _agaPalette[0];
                color = enabled && (_ecsBplcon3 & 0x20) != 0 ? unchecked((int)0xFF000000) : _agaPalette[0];
                if (enabled && (_ecsBplcon3 & 0x10) == 0) color &= 0xFFFFFF;
            }
            colors[i] = color;
        }
        // Sample sprites at Lisa's finest resolution independently of the
        // playfield. HAM is decoded only once per playfield pixel above.
        var spriteEnabled = IsSpriteOutputEnabled(line);
        for (var sample = 0; sample < 8; sample++)
        {
            var i = sample * resolution / 4;
            var lowX = x + sample / 4;
            var inside = InEcsWindow(line, lowX);
            var color = colors[i];
            if (spriteEnabled && (inside || enabled && (_ecsBplcon3 & 0x22) == 2))
            {
                var sprite = machine.ComposeAgaSpritePixel(line, x * 4 + sample,
                    inside ? placements[i] : 4, codes[i]);
                if (sprite >= 0) color = _agaPalette[sprite];
            }
            if (machine.IsRasterBlanked(line, lowX) || enabled && (_ecsBplcon3 & 1) != 0 && _externalBlank)
                color = unchecked((int)0xFF000000);
            var divisor = 4 / _outputScale;
            if (sample % divisor == 0) _rendering[start + sample / divisor] = color;
        }
    }

    internal ushort ReadAgaColor(int register)
    {
        if ((_ecsBplcon2 & 0x100) == 0) return 0xFFFF;
        var index = register + ((_ecsBplcon3 >> 13) & 7) * 32;
        return (_ecsBplcon3 & 0x200) != 0 ? _agaLow[index] : (ushort)(_agaHigh[index] & 0x8FFF);
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
    }

    internal void ReportUnsupportedAgaSpriteWrite()
        => UnsupportedActiveFeature = "AGA CPU/Copper page-mode sprite data bus residue (unverified)";
}
