// Copyright (C) 2026 Ilkka Lehtoranta. SPDX-License-Identifier: MIT
// ECS color-pair and window semantics adapted from Legacy, retaining this engine's CCK pipeline.
using System.Runtime.CompilerServices;

namespace CopperMod.Amiga.Lightweight;

internal sealed partial class LightweightVideo
{
    private readonly bool _ecsAgnus, _ecsDenise, _ecsDisplay;
    private readonly ushort[] _ecsColors = new ushort[32];
    private ushort _ecsBplcon2, _ecsBplcon3, _ecsDiwHigh;
    private bool _ecsDiwHighValid;
    private int _ecsLineClocks = 227, _ecsHeight = 313;
    private bool _externalBlank;

    internal void SetExternalBlank(bool blank) => _externalBlank = blank;

    private void ResetEcs()
    {
        Array.Clear(_ecsColors);
        _ecsBplcon2 = _ecsBplcon3 = _ecsDiwHigh = 0;
        _ecsDiwHighValid = _externalBlank = false;
        _ecsLineClocks = 227;
        _ecsHeight = 313;
        FramebufferWidth = _ecsLineClocks * 2 * _outputScale;
        FramebufferHeight = _ecsHeight;
    }

    private void PrepareEcsField(LightweightA500Machine machine)
    {
        _ecsLineClocks = machine.MaximumLineColorClocks;
        var beam = machine.GetCustomRegister(LightweightRegisters.Beamcon0);
        _ecsHeight = (beam & 0x80) != 0
            ? machine.GetCustomRegister(LightweightRegisters.Vtotal) + 1 + (machine.InterlaceEnabled ? 1 : 0)
            : (beam & 0x20) != 0 || !_ecsAgnus ? 313 : 263;
        var length = _ecsLineClocks * 2 * _outputScale * _ecsHeight;
        if (_rendering.Length != length) _rendering = new int[length];
        else Array.Clear(_rendering);
    }

    private void CompleteEcsFrame(long boundary, int lines, LightweightA500Machine machine)
    {
        if (_active && lines > 0)
        {
            RenderEcsCck(lines - 1, (machine.PreviousLineColorClocks - 2) * 2, machine);
            ApplyPendingInputs(boundary, machine);
            RenderEcsCck(lines - 1, (machine.PreviousLineColorClocks - 1) * 2, machine);
        }
        (_completed, _rendering) = (_rendering, _completed);
        FramebufferWidth = _ecsLineClocks * 2 * _outputScale;
        FramebufferHeight = _ecsHeight;
        PrepareEcsField(machine);
        _spriteOutputEnableLine = -1;
        _renderCursorInitialized = false;
        NextCycle = _active ? boundary + 2 : long.MaxValue;
    }

    private void RenderEcsPriorCck(LightweightA500Machine machine)
    {
        var horizontal = machine.BeamColorClock - 2;
        var line = machine.BeamLine;
        if (horizontal < 0) { horizontal += machine.PreviousLineColorClocks; line--; }
        RenderEcsCck(line, horizontal * 2, machine);
    }

    private bool InEcsWindow(int line, int x)
        => (line >= _verticalWindowStart && line < _verticalWindowStop ||
            _verticalWindowStop > 4096 && line < _verticalWindowStop - 4096) &&
           (x >= _horizontalWindowStart && x < _horizontalWindowStop ||
            _horizontalWindowStop > 512 && x < _horizontalWindowStop - 512);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void RenderEcsCck(int line, int x, LightweightA500Machine machine)
    {
        if (line < 0 || line >= _ecsHeight || x < 0 || x + 1 >= _ecsLineClocks * 2) return;
        var super = _ecsDenise && (_effectiveBplcon0 & 0x8040) == 0x40;
        var hires = (_effectiveBplcon0 & 0x8000) != 0;
        var resolution = super ? 4 : hires ? 2 : 1;
        var enabled = _ecsDenise && (_effectiveBplcon0 & 1) != 0;
        var dual = (_effectiveBplcon0 & 0x400) != 0;
        var ham = (_effectiveBplcon0 & 0x800) != 0 && !super;
        var outputIndex = (line * _ecsLineClocks * 2 + x) * _outputScale;
        Span<int> codes = stackalloc int[8];
        Span<int> sprites = stackalloc int[8];
        sprites.Fill(-1);
        for (var i = 0; i < resolution * 2; i++) codes[i] = ShiftPlayfieldPixel(x * resolution + i);
        for (var i = 0; i < resolution * 2; i += hires || super ? 2 : 1)
        {
            var lowX = x + i / resolution;
            if (machine.IsRasterBlanked(line, lowX) ||
                !InEcsWindow(line, lowX) && !(enabled && (_ecsBplcon3 & 2) != 0)) continue;
            var showSprite = IsSpriteOutputEnabled(line) || enabled && (_ecsBplcon3 & 2) != 0;
            if (hires || super)
            {
                var a = dual ? _dualPlayfieldPixels[codes[i]] : 0;
                var b = dual ? _dualPlayfieldPixels[codes[i + 1]] : 0;
                if (dual)
                    machine.ComposeDualHiresSpriteColorIndexes(line, super ? x * 2 + i / 2 : lowX, a >> 4, b >> 4, codes[i], codes[i + 1],
                        out sprites[i], out sprites[i + 1]);
                else
                    machine.ComposeHiresSpriteColorIndexes(line, super ? x * 2 + i / 2 : lowX,
                        codes[i], codes[i + 1], _effectiveSpritePlayfieldPlacement, out sprites[i], out sprites[i + 1]);
            }
            else
            {
                var pixel = _dualPlayfieldPixels[codes[i]];
                sprites[i] = dual ? machine.ComposeSpriteColorIndex(line, lowX, 1, pixel >> 4, codes[i]) :
                    machine.ComposeSpriteColorIndex(line, lowX, codes[i], _effectiveSpritePlayfieldPlacement);
            }
            if (!showSprite) { sprites[i] = -1; if (hires || super) sprites[i + 1] = -1; }
        }
        for (var i = 0; i < resolution * 2; i++)
        {
            var lowX = x + i / resolution;
            var blank = machine.IsRasterBlanked(line, lowX) || enabled && (_ecsBplcon3 & 1) != 0 && _externalBlank;
            var inside = InEcsWindow(line, lowX);
            var code = codes[i];
            var index = dual ? _dualPlayfieldPixels[code] & 15 : code;
            var sprite = sprites[i];
            var colorRegister = (sprite >= 0 ? sprite : index) & 31;
            if (!inside && !(enabled && (_ecsBplcon3 & 2) != 0)) sprite = -1;
            int color;
            if (super)
            {
                var first = codes[i & ~1] & 3;
                var second = codes[i | 1] & 3;
                colorRegister = (second << 2) | first;
                if (sprite >= 0)
                {
                    var sa = Math.Max(0, sprites[i & ~1]) & 3;
                    var sb = Math.Max(0, sprites[i | 1]) & 3;
                    colorRegister = 16 + (sb << 2) + sa;
                }
                color = SuperHiresColor(_ecsColors[colorRegister], (i & 1) == 0);
            }
            else if (ham && inside)
            {
                var component = (index & 15) * 17;
                _hamColor = (code >> 4) switch
                {
                    0 => Palette[index & 15], 1 => (_hamColor & ~255) | component,
                    2 => (_hamColor & ~0xFF0000) | (component << 16),
                    _ => (_hamColor & ~0xFF00) | (component << 8)
                };
                color = sprite >= 0 ? Palette[sprite] : _hamColor;
            }
            else color = Palette[sprite >= 0 ? sprite : (_ecsDenise && (_ecsBplcon2 & 0x200) != 0 ? index & 31 : index)];
            if (!inside && sprite < 0)
            {
                _hamColor = Palette[0];
                color = enabled && (_ecsBplcon3 & 0x20) != 0 ? unchecked((int)0xFF000000) : Palette[0];
                if (enabled && (_ecsBplcon3 & 0x10) == 0) color &= 0x00FFFFFF;
            }
            else if (enabled && ((_ecsBplcon2 & 0x800) != 0 && (code & (1 << ((_ecsBplcon2 >> 12) & 7))) != 0 ||
                ((_ecsBplcon2 & 0x400) != 0 ? (_ecsColors[colorRegister] & 0x8000) != 0 : colorRegister == 0) ||
                (_ecsBplcon3 & 2) != 0 && _externalBlank))
                color &= 0x00FFFFFF;
            if (blank) color = enabled && (_ecsBplcon3 & 2) != 0 && _externalBlank && !machine.IsRasterBlanked(line, lowX)
                ? 0 : unchecked((int)0xFF000000);
            var repeats = Math.Max(1, _outputScale / resolution);
            if (_outputScale >= resolution)
                for (var j = 0; j < repeats; j++) _rendering[outputIndex + i * repeats + j] = color;
            else if (i % (resolution / _outputScale) == 0)
                _rendering[outputIndex + i / (resolution / _outputScale)] = color;
        }
    }

    // Legacy/Commodore ECS palette multiplex: one register encodes adjacent 35 ns samples.
    private static int SuperHiresColor(ushort encoded, bool high)
    {
        var shift = high ? 2 : 0;
        var r = (encoded >> (8 + shift) & 3) * 85;
        var g = (encoded >> (4 + shift) & 3) * 85;
        var b = (encoded >> shift & 3) * 85;
        return unchecked((int)0xFF000000) | r << 16 | g << 8 | b;
    }
}
