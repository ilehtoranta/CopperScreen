// Copyright (C) 2026 Ilkka Lehtoranta. SPDX-License-Identifier: MIT
using System.Numerics;

namespace CopperMod.Amiga.Lightweight;

internal sealed partial class LightweightSprites
{
    private readonly bool _aga;
    private readonly AgaChannel[] _agaChannels;
    private readonly byte[] _agaCollisionMatches;
    private ushort _agaCollisionControl, _agaBanks = 0x11;
    private int _agaStep = 4, _agaFetch;
    private bool _agaScanDouble;
    private ulong _agaPendingPayload;
    private int _agaPendingWidth;

    internal LightweightSprites(bool aga = false)
    {
        _aga = aga;
        _agaChannels = aga ? new AgaChannel[8] : [];
        _agaCollisionMatches = aga ? new byte[256] : [];
    }

    private void ResetAgaSprites()
    {
        Array.Clear(_agaChannels);
        _agaCollisionControl = 0; _agaBanks = 0x11;
        _agaStep = 4; _agaFetch = 0;
        _agaScanDouble = false;
        _agaPendingPayload = 0; _agaPendingWidth = 16;
    }

    internal void ConfigureAga(ushort bplcon0, ushort bplcon3, ushort bplcon4, ushort fmode)
    {
        var mode = bplcon3 >> 6 & 3;
        _agaStep = mode switch { 1 => 4, 2 => 2, 3 => 1,
            _ => (bplcon0 & 0x8040) == 0x40 ? 2 : 4 };
        _agaBanks = bplcon4;
        _agaFetch = fmode >> 2 & 3;
        var scanDouble = (fmode & 0x8000) != 0;
        if (_agaScanDouble != scanDouble) _dirtyMask |= 255;
        _agaScanDouble = scanDouble;
    }

    internal void OnAgaDmaData(int sprite, bool dataA, ulong value, int bits, long cycle)
    {
        QueueDmaInput(dataA ? PendingInputKind.DmaDataA : PendingInputKind.DmaDataB,
            sprite, (ushort)(value >> (bits - 16)), 0, cycle);
        _agaPendingPayload = value << (64 - bits);
        _agaPendingWidth = bits;
    }

    private void ApplyAgaDmaData(int sprite, bool dataA)
    {
        ref var state = ref _agaChannels[sprite];
        if (dataA) state.DataA = _agaPendingPayload;
        else state.DataB = _agaPendingPayload;
        state.Bits = _agaPendingWidth;
    }

    private void ApplyAgaRegister(LightweightA500Machine machine)
    {
        var relative = _pendingOffset - LightweightRegisters.SpritePosFirst;
        var register = relative & 7;
        if (register != 4 && register != 6) return;
        ref var state = ref _agaChannels[relative >> 3];
        // A narrow CPU/Copper write in SPR32 mode repeats the word. Page-mode
        // writes also involve bus residue, which has no verified oracle here.
        if (_agaFetch >= 2) machine.ReportUnsupportedAgaSpriteWrite();
        var payload = _agaFetch == 0 ? (ulong)_pendingValue << 48 :
            (ulong)_pendingValue * 0x0001000100010001UL;
        if (register == 4) state.DataA = payload; else state.DataB = payload;
        state.Bits = _agaFetch == 0 ? 16 : _agaFetch == 3 ? 64 : 32;
    }

    // x is the existing raster position expressed in 35 ns units. Sampling
    // every output subpixel lets sprite and playfield resolutions differ,
    // without adding a device deadline or a second horizontal clock.
    internal int ComposeAgaPixel(int line, int x, int placement, int code)
    {
        if (_outputLine != line || x <= _lastOutputX) BeginAgaLine(line, x);
        else if ((_dirtyMask & 255) != 0) UpdateAgaComparators(line, x, false);
        if (_nextStart <= x) LatchAgaComparators(x);
        _lastOutputX = x;
        var matches = _agaCollisionMatches[code];
        if ((_dirtyMask & 0x100) != 0 && (matches & 1) != 0)
        { _collisionData |= 1; _dirtyMask &= 255; }
        if (_shiftingMask == 0) return -1;

        Span<int> pixels = stackalloc int[8];
        pixels.Clear();
        var active = (uint)_shiftingMask;
        var groups = 0;
        while (active != 0)
        {
            var sprite = BitOperations.TrailingZeroCount(active);
            active &= active - 1;
            ref var state = ref _agaChannels[sprite];
            var index = (x - state.Start) / state.Step;
            if (index >= state.LatchedBits)
            { _shiftingMask &= (byte)~(1 << sprite); continue; }
            var pixel = (int)(state.LatchedA >> (63 - index) & 1) |
                (int)(state.LatchedB >> (63 - index) & 1) << 1;
            pixels[sprite] = pixel;
            if (pixel != 0 && (_collisionSpriteEnable & (1 << sprite)) != 0)
                groups |= 1 << (sprite >> 1);
        }
        if (groups != 0)
        {
            var bits = (int)SpritePairCollisions[groups];
            if ((matches & 2) != 0) bits |= groups << 1;
            if ((matches & 4) != 0) bits |= groups << 5;
            _collisionData |= (ushort)bits;
        }
        for (var even = 0; even < 8; even += 2)
        {
            var odd = even + 1;
            // ATT is a pair control, even before the odd comparator starts or
            // when its data are transparent. The whole pair uses OSPRM.
            var attached = (_channels[odd].Ctl & 0x80) != 0;
            var pixel = attached ? ((_lineArmedMask & (1 << even)) != 0 ?
                pixels[even] | pixels[odd] << 2 : 0) :
                pixels[even] != 0 ? pixels[even] : pixels[odd];
            if (pixel == 0) continue;
            var group = even >> 1;
            if (group >= placement) return -1;
            var bank = attached || pixels[even] == 0 ? _agaBanks & 15 : _agaBanks >> 4 & 15;
            return bank * 16 + (attached ? pixel : group * 4 + pixel);
        }
        return -1;
    }

    private void BeginAgaLine(int line, int x)
    {
        if (!_collisionPlanesEnabled && _collisionTailLine != line)
        { _dirtyMask &= 255; _collisionTailLine = -1; }
        _outputLine = line; _lastOutputX = x - 1;
        _lineArmedMask = _upcomingMask = _shiftingMask = _latchedAttachedMask = 0;
        UpdateAgaComparators(line, x, true);
    }

    private int AgaStart(in SpriteChannel state)
        => ((((state.Pos & (_agaScanDouble ? 127 : 255)) << 1) | (state.Ctl & 1)) + 1) * 4 +
            (state.Ctl >> 4 & 1) * 2 + (state.Ctl >> 3 & 1);

    private void UpdateAgaComparators(int line, int x, bool begin)
    {
        var dirty = begin ? 255 : _dirtyMask & 255;
        _dirtyMask &= 0x100;
        for (var sprite = 0; sprite < 8; sprite++)
        {
            var bit = (byte)(1 << sprite);
            if ((dirty & bit) == 0 || (_shiftingMask & bit) != 0) continue;
            var wasUpcoming = (_upcomingMask & bit) != 0;
            _upcomingMask &= (byte)~bit;
            if ((_armedMask & bit) != 0 && IsVerticallyActive(in _channels[sprite], line))
            {
                var start = AgaStart(in _channels[sprite]);
                if (start >= x) { _lineArmedMask |= bit; _upcomingMask |= bit; }
                else if (begin && x < start + _agaChannels[sprite].Bits * _agaStep)
                { _lineArmedMask |= bit; LatchAgaSprite(sprite, start); }
            }
            else if (wasUpcoming) _lineArmedMask &= (byte)~bit;
        }
        RefreshAgaStart();
    }

    private void LatchAgaComparators(int x)
    {
        for (var sprite = 0; sprite < 8; sprite++)
        {
            var bit = (byte)(1 << sprite);
            if ((_upcomingMask & bit) == 0) continue;
            var start = AgaStart(in _channels[sprite]);
            if (start > x) continue;
            _upcomingMask &= (byte)~bit;
            LatchAgaSprite(sprite, start);
        }
        RefreshAgaStart();
    }

    private void LatchAgaSprite(int sprite, int start)
    {
        ref var state = ref _agaChannels[sprite];
        state.Start = start; state.Step = _agaStep; state.LatchedBits = state.Bits;
        state.LatchedA = state.DataA; state.LatchedB = state.DataB;
        var bit = (byte)(1 << sprite);
        _shiftingMask |= bit;
        if ((_channels[sprite].Ctl & 0x80) != 0) _latchedAttachedMask |= bit;
        else _latchedAttachedMask &= (byte)~bit;
    }

    private void RefreshAgaStart()
    {
        _nextStart = int.MaxValue;
        for (var sprite = 0; sprite < 8; sprite++)
            if ((_upcomingMask & (1 << sprite)) != 0)
                _nextStart = Math.Min(_nextStart, AgaStart(in _channels[sprite]));
    }

    internal void SetAgaCollisionControl(ushort value)
    { _agaCollisionControl = value; UpdateAgaCollisionMatches(); }

    private void UpdateAgaCollisionMatches()
    {
        var enabled = (_collisionControl >> 6 & 63) | (_agaCollisionControl & 0xC0);
        var match = (_collisionControl & 63) | (_agaCollisionControl & 3) << 6;
        for (var code = 0; code < 256; code++)
        {
            var mismatch = (code ^ match) & enabled;
            var odd = (mismatch & 0x55) == 0; var even = (mismatch & 0xAA) == 0;
            _agaCollisionMatches[code] = (byte)((odd && even ? 1 : 0) |
                (odd && (even || _collisionDual) ? 2 : 0) | (even ? 4 : 0));
        }
    }

    private struct AgaChannel
    {
        internal ulong DataA, DataB, LatchedA, LatchedB;
        internal int Bits, LatchedBits, Start, Step;
    }
}
