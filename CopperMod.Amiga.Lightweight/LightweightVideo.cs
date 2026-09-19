using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace CopperMod.Amiga.Lightweight;

/// <summary>
/// Direct OCS Denise output for one uncropped PAL field. The completed and
/// rendering arrays are reused and exchanged at the field boundary.
/// </summary>
internal sealed class LightweightVideo
{
    internal const int PalRasterWidth =
        LightweightClock.CpuCyclesPerLine / LightweightClock.CpuCyclesPerColorClock * 2;
    internal const int PalRasterHeight = LightweightClock.PalLongFieldLines;
    private const int VisibleRasterXStart = 98;
    private const int VisibleRasterYStart = 26;
    private const int VisibleRasterYStop = 311;
    private const int MaxPlanes = 6;
    private const ulong PlaneZeroLowMask = 0x1041_0410_4104_1041UL;
    private const uint PlaneZeroHighMask = 0x0410_4104u;

    private readonly int[] _bufferA;
    private readonly int[] _bufferB;
    private readonly int _outputScale;
    private readonly ushort[] _dataLatches = new ushort[MaxPlanes];
    private readonly ushort[] _intermediate = new ushort[MaxPlanes];
    // Six bits per physical pixel, with the next pixel at bits 90..95.
    // Reloads replace only their plane's sixteen bits; parity timing is unchanged.
    private ulong _pixelShiftersLow;
    private uint _pixelShiftersHigh;
    private readonly int[] _palette = new int[64];
    private int[] _completed;
    private int[] _rendering;
    private ushort _effectiveBplcon0;
    private ushort _effectiveBplcon1;
    private int _hamColor;
    private int _effectiveSpritePlayfieldPlacement;
    private ushort _effectiveBplcon2;
    // Raw six-plane code -> palette index (low nibble), sprite placement (high).
    // Only the dual path reads this register-derived, 64-byte table.
    private readonly byte[] _dualPlayfieldPixels = new byte[64];
    private ushort _effectiveDiwStart;
    private ushort _effectiveDiwStop;
    private int _effectivePlaneCount;
    private int _effectivePlaneMask;
    private int _effectivePlanePairMask;
    private int _horizontalWindowStart;
    private int _horizontalWindowStop;
    private int _verticalWindowStart;
    private int _verticalWindowStop;
    private ushort _pendingControlValue;
    private ushort _pendingControlOffset;
    private long _pendingControlCycle;
    private ushort _pendingDataValue;
    private int _pendingDataPlane;
    private long _pendingDataCycle;
    private int _pendingReloadParity;
    private int _spriteOutputEnableLine;
    private int _renderLine;
    private int _renderX;
    private int _renderIndex;
    private bool _renderLineVerticallyBlanked;
    private bool _renderLineVerticallyInWindow;
    private bool _renderLineSpriteOutputEnabled;
    private bool _renderCursorInitialized;
    private bool _active;

    internal LightweightVideo(LightweightA500Configuration configuration)
    {
        if (configuration.FramebufferWidth != PalRasterWidth && configuration.FramebufferWidth != 2 * PalRasterWidth ||
            configuration.FramebufferHeight != PalRasterHeight)
        {
            throw new ArgumentException(
                $"OCS output requires an uncropped 454- or 908-pixel-wide PAL raster of height {PalRasterHeight}.",
                nameof(configuration));
        }
        _outputScale = configuration.FramebufferWidth / PalRasterWidth;
        _bufferA = new int[configuration.FramebufferWidth * PalRasterHeight];
        _bufferB = new int[configuration.FramebufferWidth * PalRasterHeight];
        _completed = _bufferA;
        _rendering = _bufferB;
        Reset();
    }

    internal ReadOnlyMemory<int> Framebuffer => _completed;
    internal long NextCycle { get; private set; }
    internal ushort EffectiveBplcon0 => _effectiveBplcon0;
    internal bool Active => _active;
    internal string? UnsupportedActiveFeature { get; private set; }

    internal void Reset()
    {
        Array.Clear(_bufferA);
        Array.Clear(_bufferB);
        Array.Clear(_dataLatches);
        Array.Clear(_intermediate);
        _pixelShiftersLow = 0;
        _pixelShiftersHigh = 0;
        Array.Fill(_palette, unchecked((int)0xFF000000));
        _completed = _bufferA;
        _rendering = _bufferB;
        _effectiveBplcon0 = 0;
        _effectiveBplcon1 = 0;
        _hamColor = unchecked((int)0xFF000000);
        _effectiveSpritePlayfieldPlacement = 0;
        _effectiveBplcon2 = 0;
        UpdateDualPlayfieldPixels();
        _effectiveDiwStart = 0;
        _effectiveDiwStop = 0;
        _effectivePlaneCount = 0;
        _effectivePlaneMask = 0;
        _effectivePlanePairMask = 0;
        UpdateWindowBounds();
        _pendingControlValue = 0;
        _pendingControlOffset = 0;
        _pendingControlCycle = long.MaxValue;
        _pendingDataValue = 0;
        _pendingDataPlane = -1;
        _pendingDataCycle = long.MaxValue;
        _pendingReloadParity = 0;
        _spriteOutputEnableLine = -1;
        _renderLine = 0;
        _renderX = 0;
        _renderIndex = 0;
        _renderLineVerticallyBlanked = true;
        _renderLineVerticallyInWindow = false;
        _renderLineSpriteOutputEnabled = false;
        _renderCursorInitialized = false;
        _active = false;
        NextCycle = long.MaxValue;
        UnsupportedActiveFeature = null;
    }

    internal void OnRegisterWrite(ushort offset, ushort value, long cycle)
    {
        if (offset is >= LightweightRegisters.ColorFirst and <= LightweightRegisters.ColorLast)
        {
            UpdatePalette((offset - LightweightRegisters.ColorFirst) >> 1, value);
        }
        if (offset is LightweightRegisters.Bplcon0 or LightweightRegisters.Bplcon1 or
            LightweightRegisters.Bplcon2 or LightweightRegisters.Clxcon or
            LightweightRegisters.Diwstrt or LightweightRegisters.Diwstop)
        {
            _pendingControlOffset = offset;
            _pendingControlValue = value;
            _pendingControlCycle = cycle + LightweightClock.CpuCyclesPerColorClock;
        }
        else if (offset >= LightweightRegisters.BpldatFirst &&
            offset <= LightweightRegisters.BpldatLast &&
            ((offset - LightweightRegisters.BpldatFirst) & 1) == 0)
        {
            AcceptBitplaneWord(
                (offset - LightweightRegisters.BpldatFirst) >> 1,
                value,
                cycle);
        }

        if (offset == LightweightRegisters.DmaconWrite ||
            offset is >= LightweightRegisters.ColorFirst and <= LightweightRegisters.ColorLast ||
            offset is LightweightRegisters.Bplcon0 or LightweightRegisters.Bplcon1 or
                LightweightRegisters.Bplcon2 or LightweightRegisters.Clxcon or
                LightweightRegisters.Diwstrt or LightweightRegisters.Diwstop ||
            offset is >= LightweightRegisters.BpldatFirst and <= LightweightRegisters.BpldatLast)
        {
            ActivateAfter(cycle);
        }
    }

    internal void AcceptBitplaneWord(int plane, ushort value, long outputCycle)
    {
        if ((uint)plane >= MaxPlanes)
        {
            return;
        }
        _pendingDataPlane = plane;
        _pendingDataValue = value;
        _pendingDataCycle = outputCycle + LightweightClock.CpuCyclesPerColorClock;
        ActivateAfter(outputCycle);
    }

    internal void ActivateForSprite(long cycle)
        => ActivateAfter(cycle);

    internal void Step(long cycle, LightweightA500Machine machine)
    {
        System.Diagnostics.Debug.Assert(
            cycle == NextCycle,
            "Video must advance at its published CCK.");
        RenderPriorPhysicalCck(machine);
        ApplyPendingInputs(cycle, machine);
        NextCycle = cycle + LightweightClock.CpuCyclesPerColorClock;
    }

    internal void CompleteFrame(
        long boundaryCycle,
        int completedFieldLines,
        LightweightA500Machine machine)
    {
        if (_active)
        {
            var finalRow = (completedFieldLines - 1) * PalRasterWidth;
            _renderLine = completedFieldLines - 1;
            UpdateRenderLineState();
            RenderPixelPair(
                completedFieldLines - 1,
                PalRasterWidth - 4,
                finalRow + PalRasterWidth - 4,
                machine);
            ApplyPendingInputs(boundaryCycle, machine);
            RenderPixelPair(
                completedFieldLines - 1,
                PalRasterWidth - 2,
                finalRow + PalRasterWidth - 2,
                machine);
        }

        (_completed, _rendering) = (_rendering, _completed);
        if (completedFieldLines < PalRasterHeight)
        {
            Array.Clear(
                _completed,
                completedFieldLines * PalRasterWidth * _outputScale,
                (PalRasterHeight - completedFieldLines) * PalRasterWidth * _outputScale);
        }
        _spriteOutputEnableLine = -1;
        _renderCursorInitialized = false;
        NextCycle = _active
            ? boundaryCycle + LightweightClock.CpuCyclesPerColorClock
            : long.MaxValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplyPendingInputs(long cycle, LightweightA500Machine machine)
    {
        if (_pendingControlCycle <= cycle || _pendingDataCycle <= cycle)
            ApplyDueInputs(cycle, machine);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ApplyDueInputs(
        long cycle,
        LightweightA500Machine machine)
    {
        var refreshRenderLine = false;
        if (_pendingControlCycle <= cycle)
        {
            switch (_pendingControlOffset)
            {
                case LightweightRegisters.Bplcon0:
                    _effectiveBplcon0 = _pendingControlValue;
                    _effectivePlaneCount = GetDecodePlaneCount(_effectiveBplcon0);
                    machine.SetCollisionMode((_effectiveBplcon0 & 0x0400) != 0, _effectivePlaneCount != 0);
                    _effectivePlaneMask = (1 << _effectivePlaneCount) - 1;
                    _effectivePlanePairMask = _effectivePlaneMask | (_effectivePlaneMask << 6);
                    UpdateUnsupportedMode();
                    refreshRenderLine = true;
                    break;
                case LightweightRegisters.Bplcon1:
                    _effectiveBplcon1 = _pendingControlValue;
                    break;
                case LightweightRegisters.Clxcon:
                    machine.SetCollisionControl(_pendingControlValue);
                    break;
                case LightweightRegisters.Bplcon2:
                    if ((_effectiveBplcon2 & 0x7F) != (_pendingControlValue & 0x7F))
                    {
                        _effectiveBplcon2 = _pendingControlValue;
                        UpdateDualPlayfieldPixels();
                    }
                    _effectiveSpritePlayfieldPlacement = Math.Min(
                        (_pendingControlValue >> 3) & 7,
                        4);
                    if ((_effectiveBplcon0 & 0x0400) != 0) UpdateUnsupportedMode();
                    break;
                case LightweightRegisters.Diwstrt:
                    _effectiveDiwStart = _pendingControlValue;
                    UpdateWindowBounds();
                    refreshRenderLine = true;
                    break;
                case LightweightRegisters.Diwstop:
                    _effectiveDiwStop = _pendingControlValue;
                    UpdateWindowBounds();
                    refreshRenderLine = true;
                    break;
            }
            _pendingControlCycle = long.MaxValue;
        }

        if (_pendingDataCycle <= cycle)
        {
            var plane = _pendingDataPlane;
            _dataLatches[plane] = _pendingDataValue;
            if (plane == 0)
            {
                _intermediate[0] = _dataLatches[0];
                _intermediate[1] = _dataLatches[1];
                _intermediate[2] = _dataLatches[2];
                _intermediate[3] = _dataLatches[3];
                _intermediate[4] = _dataLatches[4];
                _intermediate[5] = _dataLatches[5];
                _pendingReloadParity = 3;
                _spriteOutputEnableLine = machine.BeamLine;
                refreshRenderLine = true;
            }
            _pendingDataCycle = long.MaxValue;
        }

        if (refreshRenderLine && _renderCursorInitialized)
        {
            UpdateRenderLineState();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RenderPriorPhysicalCck(LightweightA500Machine machine)
    {
        if (!_renderCursorInitialized)
        {
            var currentHorizontal = machine.BeamColorClock;
            var previousHorizontal = currentHorizontal == 0
                ? (LightweightClock.CpuCyclesPerLine /
                    LightweightClock.CpuCyclesPerColorClock) - 1
                : currentHorizontal - 1;
            _renderLine = currentHorizontal <= 1
                ? machine.BeamLine - 1
                : machine.BeamLine;
            _renderX = previousHorizontal == 0
                ? PalRasterWidth - 2
                : (previousHorizontal - 1) * 2;
            _renderIndex = (_renderLine * PalRasterWidth) + _renderX;
            _renderCursorInitialized = true;
            UpdateRenderLineState();
        }

        if ((uint)_renderLine < PalRasterHeight)
        {
            RenderPixelPair(
                _renderLine,
                _renderX,
                _renderIndex,
                machine);
        }

        _renderX += 2;
        _renderIndex += 2;
        if (_renderX == PalRasterWidth)
        {
            _renderX = 0;
            _renderLine++;
            UpdateRenderLineState();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RenderPixelPair(
        int line, int x, int index, LightweightA500Machine machine)
    {
        if (_outputScale == 1)
        {
            RenderLowResPair(line, x, index, machine);
            return;
        }
        index *= 2;
        if ((_effectiveBplcon0 & 0x8000) != 0)
        {
            RenderHiresCck(line, x, index, machine);
            return;
        }
        RenderLowResPair(line, x, index, machine, wide: true);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void RenderHiresCck(int line, int x, int index, LightweightA500Machine machine)
    {
        // No device input occurs between these samples within one CCK.
        // Shift even in blanking/borders, retaining pending reload phases.
        var firstPair = ShiftPlayfieldPair(x * 2);
        var secondPair = ShiftPlayfieldPair(x * 2 + 2);
        if (_renderLineVerticallyBlanked || x + 1 < VisibleRasterXStart)
        {
            FillHiresCck(index, unchecked((int)0xFF000000));
            return;
        }
        if (x >= VisibleRasterXStart && (!_renderLineVerticallyInWindow ||
            x + 1 < _horizontalWindowStart || x >= _horizontalWindowStop))
        {
            FillHiresCck(index, _palette[0]);
            return;
        }
        if (x >= VisibleRasterXStart && x >= _horizontalWindowStart &&
            x + 1 < _horizontalWindowStop)
        {
            RenderHiresVisiblePair(line, x, index, firstPair, machine);
            RenderHiresVisiblePair(line, x + 1, index + 2, secondPair, machine);
            return;
        }
        RenderHiresBoundaryCck(line, x, index, firstPair, secondPair, machine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FillHiresCck(int index, int color)
    {
        ref var destination = ref _rendering[index];
        Vector128.Create(color).StoreUnsafe(ref destination);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RenderHiresVisiblePair(int line, int x, int index, int pair,
        LightweightA500Machine machine)
    {
        if ((_effectiveBplcon0 & 0x0400) != 0)
        {
            RenderDualHiresVisiblePair(line, x, index, pair, machine);
            return;
        }
        var first = pair >> 6;
        var second = pair & 0x3F;
        machine.ComposeHiresSpriteColorIndexes(line, x, first, second,
            _effectiveSpritePlayfieldPlacement, out var sprite1, out var sprite2);
        if (_renderLineSpriteOutputEnabled)
        {
            if (sprite1 >= 0) first = sprite1;
            if (sprite2 >= 0) second = sprite2;
        }
        _rendering[index] = _palette[first];
        _rendering[index + 1] = _palette[second];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void RenderHiresBoundaryCck(int line, int x, int index, int firstPair,
        int secondPair, LightweightA500Machine machine)
    {
        for (var pixel = 0; pixel < 2; pixel++)
        {
            var lowX = x + pixel;
            var destination = index + pixel * 2;
            if (lowX >= VisibleRasterXStart && _renderLineVerticallyInWindow &&
                lowX >= _horizontalWindowStart && lowX < _horizontalWindowStop)
                RenderHiresVisiblePair(line, lowX, destination,
                    pixel == 0 ? firstPair : secondPair, machine);
            else
            {
                var color = lowX < VisibleRasterXStart ? unchecked((int)0xFF000000) : _palette[0];
                _rendering[destination] = color;
                _rendering[destination + 1] = color;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RenderLowResPair(
        int line,
        int x,
        int index,
        LightweightA500Machine machine,
        bool wide = false)
    {
        var playfieldPair = ShiftPlayfieldPair(x);
        if ((_effectiveBplcon0 & 0x0C00) != 0)
        {
            RenderSpecialLowResPair(line, x, index, playfieldPair, machine, wide);
            return;
        }
        var firstPlayfield = playfieldPair >> 6;
        var secondPlayfield = playfieldPair & 0x3F;

        if (_renderLineVerticallyBlanked || x + 1 < VisibleRasterXStart)
        {
            WriteLowResPair(index, unchecked((int)0xFF000000), unchecked((int)0xFF000000), wide);
            return;
        }

        if (!_renderLineVerticallyInWindow ||
            x + 1 < _horizontalWindowStart ||
            x >= _horizontalWindowStop)
        {
            var border = _palette[0];
            WriteLowResPair(index, border, border, wide);
            return;
        }

        if (x >= _horizontalWindowStart && x + 1 < _horizontalWindowStop)
        {
            machine.ComposeSpriteColorIndexes(
                line,
                x,
                firstPlayfield,
                secondPlayfield,
                _effectiveSpritePlayfieldPlacement,
                out var firstSprite,
                out var secondSprite);
            if (_renderLineSpriteOutputEnabled && firstSprite >= 0)
            {
                firstPlayfield = firstSprite;
            }
            if (_renderLineSpriteOutputEnabled && secondSprite >= 0)
            {
                secondPlayfield = secondSprite;
            }

            WriteLowResPair(index, _palette[firstPlayfield], _palette[secondPlayfield], wide);
            return;
        }

        RenderWindowBoundaryPair(
            line,
            x,
            index,
            firstPlayfield,
            secondPlayfield,
            machine,
            wide);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void RenderSpecialLowResPair(int line, int x, int index, int pair,
        LightweightA500Machine machine, bool wide)
    {
        if ((_effectiveBplcon0 & 0x0800) == 0)
        {
            var first = RenderDualLowResPixel(line, x, pair >> 6, machine);
            var second = RenderDualLowResPixel(line, x + 1, pair & 63, machine);
            WriteLowResPair(index, first, second, wide);
            return;
        }
        // HAM still updates its held color in physical pixel order; sprite
        // overlays never feed that hold register.
        var hamFirst = RenderHamPixel(line, x, pair >> 6, machine);
        var hamSecond = RenderHamPixel(line, x + 1, pair & 63, machine);
        WriteLowResPair(index, hamFirst, hamSecond, wide);
    }

    private int RenderDualLowResPixel(int line, int x, int code, LightweightA500Machine machine)
    {
        if (_renderLineVerticallyBlanked || x < VisibleRasterXStart)
            return unchecked((int)0xFF000000);
        if (!_renderLineVerticallyInWindow || x < _horizontalWindowStart || x >= _horizontalWindowStop)
            return _palette[0];
        var pixel = _dualPlayfieldPixels[code];
        var sprite = machine.ComposeSpriteColorIndex(line, x, 1, pixel >> 4, code);
        return _palette[_renderLineSpriteOutputEnabled && sprite >= 0 ? sprite : pixel & 15];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void RenderDualHiresVisiblePair(int line, int x, int index, int pair,
        LightweightA500Machine machine)
    {
        var first = _dualPlayfieldPixels[pair >> 6];
        var second = _dualPlayfieldPixels[pair & 63];
        machine.ComposeDualHiresSpriteColorIndexes(line, x, first >> 4, second >> 4, pair >> 6, pair & 63,
            out var sprite1, out var sprite2);
        _rendering[index] = _palette[_renderLineSpriteOutputEnabled && sprite1 >= 0 ? sprite1 : first & 15];
        _rendering[index + 1] = _palette[_renderLineSpriteOutputEnabled && sprite2 >= 0 ? sprite2 : second & 15];
    }

    private void UpdateDualPlayfieldPixels()
    {
        var placement1 = Math.Min(_effectiveBplcon2 & 7, 4);
        var placement2 = Math.Min((_effectiveBplcon2 >> 3) & 7, 4);
        var pf2First = (_effectiveBplcon2 & 0x40) != 0;
        for (var code = 0; code < 64; code++)
        {
            var pf1 = (code & 1) | ((code >> 1) & 2) | ((code >> 2) & 4);
            var pf2 = ((code >> 1) & 1) | ((code >> 2) & 2) | ((code >> 3) & 4);
            var color = pf2 != 0 && (pf2First || pf1 == 0) ? 8 + pf2 : pf1;
            // HRM ch.7: even an obscured opaque playfield can mask a sprite.
            var placement = Math.Min(pf1 != 0 ? placement1 : 4, pf2 != 0 ? placement2 : 4);
            _dualPlayfieldPixels[code] = (byte)(color | (placement << 4));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int RenderHamPixel(int line, int x, int code, LightweightA500Machine machine)
    {
        if (_renderLineVerticallyBlanked || x < VisibleRasterXStart)
        {
            _hamColor = _palette[0];
            return unchecked((int)0xFF000000);
        }
        if (!_renderLineVerticallyInWindow ||
            x < _horizontalWindowStart || x >= _horizontalWindowStop)
            return _hamColor = _palette[0];

        // HRM: plane 6/5 = 00 palette, 01 blue, 10 red, 11 green.
        var component = (code & 15) * 17;
        var color = (code >> 4) switch
        {
            0 => _palette[code],
            1 => (_hamColor & ~0xFF) | component,
            2 => (_hamColor & ~0xFF0000) | (component << 16),
            _ => (_hamColor & ~0xFF00) | (component << 8)
        };
        _hamColor = color;
        var sprite = machine.ComposeSpriteColorIndex(line, x, code,
            _effectiveSpritePlayfieldPlacement);
        return _renderLineSpriteOutputEnabled && sprite >= 0 ? _palette[sprite] : color;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteLowResPair(int index, int first, int second, bool wide)
    {
        // The caller reserves one physical CCK: two narrow or four wide pixels.
        // Both call sites pass a constant width, so the hot paths specialize.
        ref var destination = ref _rendering[index];
        if (wide)
        {
            var colors = Vector128.CreateScalar(first).WithElement(1, second);
            Vector128.Shuffle(colors, Vector128.Create(0, 0, 1, 1)).StoreUnsafe(ref destination);
        }
        else
        {
            destination = first;
            Unsafe.Add(ref destination, 1) = second;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void RenderWindowBoundaryPair(
        int line,
        int x,
        int index,
        int firstPlayfield,
        int secondPlayfield,
        LightweightA500Machine machine,
        bool wide)
    {
        var secondX = x + 1;
        var firstBlanked = x < VisibleRasterXStart;
        var secondBlanked = secondX < VisibleRasterXStart;
        var firstInWindow = _renderLineVerticallyInWindow &&
            x >= _horizontalWindowStart && x < _horizontalWindowStop;
        var secondInWindow = _renderLineVerticallyInWindow &&
            secondX >= _horizontalWindowStart && secondX < _horizontalWindowStop;
        var firstOutput = firstInWindow ? firstPlayfield : 0;
        var secondOutput = secondInWindow ? secondPlayfield : 0;
        var firstSpriteEligible = !firstBlanked && firstInWindow;
        var secondSpriteEligible = !secondBlanked && secondInWindow;
        var spriteOutputEnabled = _renderLineSpriteOutputEnabled;

        if (firstSpriteEligible && secondSpriteEligible)
        {
            machine.ComposeSpriteColorIndexes(
                line,
                x,
                firstPlayfield,
                secondPlayfield,
                _effectiveSpritePlayfieldPlacement,
                out var firstSprite,
                out var secondSprite);
            if (spriteOutputEnabled && firstSprite >= 0)
            {
                firstOutput = firstSprite;
            }
            if (spriteOutputEnabled && secondSprite >= 0)
            {
                secondOutput = secondSprite;
            }
        }
        else
        {
            if (firstSpriteEligible)
            {
                var sprite = machine.ComposeSpriteColorIndex(
                    line,
                    x,
                    firstPlayfield,
                    _effectiveSpritePlayfieldPlacement);
                if (spriteOutputEnabled && sprite >= 0)
                {
                    firstOutput = sprite;
                }
            }
            if (secondSpriteEligible)
            {
                var sprite = machine.ComposeSpriteColorIndex(
                    line,
                    secondX,
                    secondPlayfield,
                    _effectiveSpritePlayfieldPlacement);
                if (spriteOutputEnabled && sprite >= 0)
                {
                    secondOutput = sprite;
                }
            }
        }

        WriteLowResPair(index,
            firstBlanked ? unchecked((int)0xFF000000) : _palette[firstOutput],
            secondBlanked ? unchecked((int)0xFF000000) : _palette[secondOutput], wide);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateRenderLineState()
    {
        var line = _renderLine;
        _renderLineVerticallyBlanked =
            line < VisibleRasterYStart || line >= VisibleRasterYStop;
        _renderLineVerticallyInWindow =
            line >= _verticalWindowStart && line < _verticalWindowStop;
        _renderLineSpriteOutputEnabled = IsSpriteOutputEnabled(line);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ShiftPlayfieldPair(int x)
    {
        if (_pendingReloadParity != 0)
        {
            return ShiftPlayfieldPairWithReload(x);
        }

        var pixels = (int)(_pixelShiftersHigh >> 20);
        _pixelShiftersHigh = (_pixelShiftersHigh << 12) | (uint)(_pixelShiftersLow >> 52);
        _pixelShiftersLow <<= 12;
        // Retain the shifter's adjacent six-bit lanes: first pixel above second.
        // This avoids repacking to bytes only for the renderer to unpack them.
        return pixels & _effectivePlanePairMask;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private int ShiftPlayfieldPairWithReload(int x)
    {
        var first = ShiftPlayfieldPixel(x);
        var second = ShiftPlayfieldPixel(x + 1);
        return (first << 6) | second;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ShiftPlayfieldPixel(int x)
    {
        var colorIndex = (int)(_pixelShiftersHigh >> 26) & _effectivePlaneMask;
        _pixelShiftersHigh = (_pixelShiftersHigh << 6) | (uint)(_pixelShiftersLow >> 58);
        _pixelShiftersLow <<= 6;

        var pendingReload = _pendingReloadParity;
        if (pendingReload != 0)
        {
            var scrolls = (int)_effectiveBplcon1;
            if ((_effectiveBplcon0 & 0x8000) != 0)
                scrolls = ((scrolls & 0x77) << 1) | 0x11;
            var position = x & 15;
            if ((pendingReload & 1) != 0 && position == (scrolls & 15))
            {
                if (_effectivePlaneCount > 0)
                {
                    ReloadPlane(0, _intermediate[0]);
                }
                if (_effectivePlaneCount > 2)
                {
                    ReloadPlane(2, _intermediate[2]);
                }
                if (_effectivePlaneCount > 4)
                {
                    ReloadPlane(4, _intermediate[4]);
                }
                pendingReload &= ~1;
            }
            if ((pendingReload & 2) != 0 && position == ((scrolls >> 4) & 15))
            {
                if (_effectivePlaneCount > 1)
                {
                    ReloadPlane(1, _intermediate[1]);
                }
                if (_effectivePlaneCount > 3)
                {
                    ReloadPlane(3, _intermediate[3]);
                }
                if (_effectivePlaneCount > 5)
                {
                    ReloadPlane(5, _intermediate[5]);
                }
                pendingReload &= ~2;
            }
            _pendingReloadParity = pendingReload;
        }

        return colorIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReloadPlane(int plane, ushort word)
    {
        // Plane is constant at every call site. Deposit at reload time instead
        // of gathering six separate lane bits on every output pixel.
        var lowMask = PlaneZeroLowMask << plane;
        var highMask = plane == 0 ? PlaneZeroHighMask :
            (PlaneZeroHighMask << plane) | (uint)(PlaneZeroLowMask >> (64 - plane));
        ulong low;
        uint high;
        if (Bmi2.X64.IsSupported)
        {
            low = Bmi2.X64.ParallelBitDeposit(word, lowMask);
            high = Bmi2.ParallelBitDeposit((uint)word >> (plane < 4 ? 11 : 10), highMask);
        }
        else
        {
            low = SpreadPlaneBits(word) << plane;
            high = (uint)(SpreadPlaneBits((uint)word >> (plane < 4 ? 11 : 10)) <<
                (plane < 4 ? plane + 2 : plane - 4));
        }
        _pixelShiftersLow = (_pixelShiftersLow & ~lowMask) | low;
        _pixelShiftersHigh = (_pixelShiftersHigh & ~highMask) | high;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong SpreadPlaneBits(uint word)
    {
        ulong result = 0;
        for (var bit = 0; bit < 11; bit++)
            result |= (ulong)((word >> bit) & 1) << (bit * 6);
        return result;
    }

    private void UpdateWindowBounds()
    {
        _horizontalWindowStart = _effectiveDiwStart & 0x00FF;
        _horizontalWindowStop = (_effectiveDiwStop & 0x00FF) + 0x100;
        _verticalWindowStart = (_effectiveDiwStart >> 8) & 0x00FF;
        _verticalWindowStop = (_effectiveDiwStop >> 8) & 0x00FF;
        if (_verticalWindowStop < 0x80)
        {
            _verticalWindowStop += 0x100;
        }
        if (_verticalWindowStop <= _verticalWindowStart)
        {
            _verticalWindowStop += 0x100;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsSpriteOutputEnabled(int line)
    {
        var requestedPlanes = (_effectiveBplcon0 >> 12) & 7;
        return requestedPlanes == 0
            ? (_effectiveBplcon0 & 0x0200) == 0
            : _spriteOutputEnableLine == line;
    }

    private void UpdatePalette(int colorIndex, ushort encoded)
    {
        var r = ((encoded >> 8) & 0x0F) * 17;
        var g = ((encoded >> 4) & 0x0F) * 17;
        var b = (encoded & 0x0F) * 17;
        _palette[colorIndex] = unchecked((int)(0xFF000000u |
            ((uint)r << 16) | ((uint)g << 8) | (uint)b));
        _palette[colorIndex + 32] = unchecked((int)(0xFF000000u |
            ((uint)(r >> 1) << 16) | ((uint)(g >> 1) << 8) | (uint)(b >> 1)));
    }

    private void UpdateUnsupportedMode()
    {
        var hires = (_effectiveBplcon0 & 0x8000) != 0;
        var feature = hires && _outputScale != 2
            ? "OCS hires output"
            : hires && ((_effectiveBplcon0 >> 12) & 7) > 4
                ? "OCS hires BPU above four"
            : (_effectiveBplcon0 & 0x0800) != 0 &&
                (hires || _effectivePlaneCount is < 5 or > 6)
                ? "OCS HAM outside five/six-plane lores"
                : (_effectiveBplcon0 & 0x0C00) == 0x0C00
                    ? "OCS dual-playfield HAM output"
                : (_effectiveBplcon0 & 0x0400) != 0 &&
                    ((_effectiveBplcon2 & 7) > 4 || ((_effectiveBplcon2 >> 3) & 7) > 4)
                    ? "OCS dual-playfield priority codes above four"
                    : ((_effectiveBplcon0 >> 12) & 7) == 7
                        ? "OCS BPU=7 output"
                        : null;
        if (feature is not null)
        {
            UnsupportedActiveFeature = feature;
        }
    }

    private void ActivateAfter(long cycle)
    {
        if (!_active)
        {
            Array.Clear(_rendering);
            _active = true;
        }
        var next = LightweightBusArbiter.AlignToSlot(cycle + 1);
        NextCycle = Math.Min(NextCycle, next);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetDecodePlaneCount(ushort bplcon0)
    {
        var count = (bplcon0 >> 12) & 7;
        return Math.Min(count, (bplcon0 & 0x8000) != 0 ? 4 : MaxPlanes);
    }
}
