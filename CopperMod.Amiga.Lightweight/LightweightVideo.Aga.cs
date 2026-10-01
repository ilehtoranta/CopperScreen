// Copyright (C) 2026 Ilkka Lehtoranta. SPDX-License-Identifier: MIT
using System.Runtime.CompilerServices;

namespace CopperMod.Amiga.Lightweight;

internal sealed partial class LightweightVideo
{
    private readonly bool _aga;
    private readonly int[] _agaPalette = new int[256];
    private readonly ushort[] _agaHigh = new ushort[256], _agaLow = new ushort[256];
    private readonly ulong[] _agaData = new ulong[8], _agaIntermediate = new ulong[8], _agaShift = new ulong[8];
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
        var start = (line * _ecsLineClocks * 2 + x) * _outputScale;
        for (var i = 0; i < resolution * 2; i++)
        {
            var lowX = x + i / resolution;
            var code = ShiftAgaPixel(x * resolution + i, resolution);
            var inside = InEcsWindow(line, lowX);
            var index = code;
            // Ordinary single playfield is the first AGA display milestone.
            // The existing sprite path supplies legacy-width, legacy-resolution
            // sprites; widened/resolution modes are explicitly reported below.
            var sprite = inside && IsSpriteOutputEnabled(line)
                ? machine.ComposeSpriteColorIndex(line, lowX, code, _effectiveSpritePlayfieldPlacement, code & 63) : -1;
            if (sprite >= 0) index = (sprite & 15) | ((_agaBplcon4 & 15) << 4);
            else index ^= _agaBplcon4 >> 8;
            var color = _agaPalette[index];
            if (_effectivePlaneCount == 6 && (_effectiveBplcon0 & 0xC00) == 0 &&
                (_ecsBplcon2 & 0x200) == 0 && sprite < 0 && (code & 32) != 0)
                color = unchecked((int)0xFF000000) | ((_agaPalette[(code & 31) ^ (_agaBplcon4 >> 8)] & 0xFEFEFE) >> 1);
            if (!inside)
            {
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

    private void CheckAgaMode()
    {
        if ((_effectiveBplcon0 & 0xC00) != 0) UnsupportedActiveFeature = "AGA HAM/dual-playfield composition (next milestone)";
        else if ((_ecsBplcon2 & 0x100) != 0) UnsupportedActiveFeature = "AGA palette readback (next milestone)";
        else if ((_agaFmode & 0xC00C) != 0) UnsupportedActiveFeature = "AGA wide sprites/scan doubling (next milestone)";
        else if ((_agaBplcon4 & 15) != ((_agaBplcon4 >> 4) & 15)) UnsupportedActiveFeature = "AGA independent even/odd sprite palette banks (next milestone)";
        // SPRES=01 explicitly selects the existing 140 ns sprite path.
        else if ((_ecsBplcon3 & 0x80) != 0) UnsupportedActiveFeature = "AGA higher sprite resolution (next milestone)";
    }
}
