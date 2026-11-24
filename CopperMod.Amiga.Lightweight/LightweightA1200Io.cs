/*
 * Copyright (C) 2026 Ilkka Lehtoranta
 * SPDX-License-Identifier: MIT
 */

namespace CopperMod.Amiga.Lightweight;

/// <summary>
/// A1200 motherboard decode with an optional read-only IDE master and no PCMCIA
/// card. Unfitted devices float high; no boot-delay shortcuts are used.
/// </summary>
internal sealed class LightweightA1200Io : IDisposable
{
    internal const uint PcmciaStart = 0x0060_0000;
    internal const uint PcmciaMemoryEndExclusive = 0x00A0_0000;
    internal const uint PcmciaEndExclusive = 0x00A8_0000;
    internal const uint IdeStart = 0x00DA_0000;
    internal const uint GayleEndExclusive = 0x00DB_0000;
    internal const uint MotherboardResourcesStart = 0x00DE_0000;
    internal const uint MotherboardResourcesEndExclusive = 0x00DF_0000;
    internal const uint GayleCardStatus = 0x00DA_8000;
    internal const uint GayleInterruptRequest = 0x00DA_9000;
    internal const uint GayleInterruptEnable = 0x00DA_A000;
    internal const uint GayleConfiguration = 0x00DA_B000;
    internal const uint GayleIdentification = 0x00DE_1000;

    private byte _cardControl;
    private byte _interruptRequest;
    private byte _interruptEnable;
    private byte _configuration;
    private int _identificationBit;
    private LightweightAtaDrive? _ide;
    private bool _ideInterruptLine;
    private long _lastAccessEnd;
    internal bool HasDrive => _ide is not null;
    internal long NextCycle => _ide?.NextCycle ?? long.MaxValue;
    internal void Attach(LightweightIdeDriveConfiguration configuration, ILightweightStorageObserver? observer)
        => _ide = new(configuration, observer);
    internal void Step(long cycle) { _ide?.Step(cycle); SyncInterrupt(); }
    internal void Grant(long cycle, out long grant, out long end)
    { grant = Math.Max(cycle, _lastAccessEnd); end = checked(grant + 5); _lastAccessEnd = end; }
    private void SyncInterrupt()
    {
        // Keep the drive's live status separate from Gayle's change latch.
        // Acknowledging Gayle before ATA must not immediately relatch a held
        // input; the next assertion is a new interrupt event.
        var line=_ide?.InterruptAsserted==true;
        if (line && !_ideInterruptLine) _interruptRequest|=0x80;
        _ideInterruptLine=line;
    }
    // A5 and A13 select aliases. All other bits above the byte lanes select the register.
    private static int IdeRegister(uint address) => (int)(((address - IdeStart) & ~0x2020u) >> 2);
    private const int DeviceControl = 0x406;
    private static bool TaskFileLane(uint address) => (address & 3) == ((address & 0x2000) != 0 ? 0u : 2u);

    internal byte CardControl => _cardControl;
    internal byte InterruptRequest => _interruptRequest;
    internal byte InterruptEnable => _interruptEnable;
    internal byte Configuration => _configuration;
    internal bool InterruptAsserted => (_interruptRequest & _interruptEnable & 0x80) != 0;

    internal static bool ContainsAddress(uint address)
        => address is >= PcmciaStart and < PcmciaEndExclusive ||
            address is >= IdeStart and < GayleEndExclusive ||
            address is >= MotherboardResourcesStart and < MotherboardResourcesEndExclusive;

    internal byte PeekByte(uint address) => ReadByte(address, false);

    internal byte ReadByte(uint address, bool strobe = true, long cycle = 0)
    {
        address &= 0x00FF_FFFF;
        if (address == GayleCardStatus)
        {
            return (byte)(_cardControl | (_ideInterruptLine ? 0x80 : 0));
        }

        if (address == GayleInterruptRequest)
        {
            return _interruptRequest;
        }

        if (address == GayleInterruptEnable)
        {
            return _interruptEnable;
        }

        if (address == GayleConfiguration)
        {
            return (byte)(_configuration & 0x0F);
        }

        if (address == GayleIdentification)
        {
            // AA Gayle identification is D1, shifted out most-significant bit first.
            var value = (byte)((0xD1 << _identificationBit) & 0x80);
            if (strobe) _identificationBit = (_identificationBit + 1) & 7;
            return value;
        }

        if (address is >= IdeStart and < GayleCardStatus)
        {
            if (_ide is not null && address < IdeStart+0x4000)
            {
                var register = IdeRegister(address);
                if (register is >= 1 and <= 7 && !TaskFileLane(address)) return 0xFF;
                var alternate = register == DeviceControl;
                // Gayle only clocks the PIO data FIFO on a word transfer.
                var value = _ide.ReadRegister(alternate ? 7 : register, alternate, strobe);
                SyncInterrupt();
                return value;
            }
            // An unattached ATA bus floats high. In particular, status is FF rather
            // than the emulator-specific 7F fast-boot value used by some emulators.
            return 0xFF;
        }

        // Empty PCMCIA and unpopulated motherboard-resource lanes float high.
        return 0xFF;
    }

    internal ushort ReadWord(uint address, long cycle, bool strobe = true)
    {
        if (_ide is not null && address is >= IdeStart and < GayleCardStatus && IdeRegister(address) == 0)
        { var result = _ide.ReadData(cycle, strobe); SyncInterrupt(); return result; }
        return (ushort)((ReadByte(address, strobe, cycle) << 8) | ReadByte(address + 1, strobe, cycle));
    }

    internal void WriteByte(uint address, byte value, long cycle = 0)
    {
        address &= 0x00FF_FFFF;
        if (address == GayleCardStatus)
        {
            _cardControl = (byte)(value & 0x03);
        }
        else if (address == GayleInterruptRequest)
        {
            _interruptRequest = (byte)((_interruptRequest & value) | (value & 0x03));
        }
        else if (address == GayleInterruptEnable)
        {
            _interruptEnable = value;
        }
        else if (address == GayleConfiguration)
        {
            _configuration = (byte)(value & 0x0F);
        }
        else if (address is >= MotherboardResourcesStart and < MotherboardResourcesEndExclusive)
        {
            _identificationBit = 0;
        }
        else if (_ide is not null && address is >= IdeStart and < IdeStart+0x4000)
        {
            var register = IdeRegister(address);
            if (register == DeviceControl) _ide.WriteControl(value, cycle);
            else if (register is >= 1 and <= 7 && TaskFileLane(address)) _ide.WriteRegister(register, value, cycle);
            SyncInterrupt();
        }
    }

    internal void Reset()
    {
        _cardControl = 0;
        _interruptRequest = 0;
        _interruptEnable = 0;
        _configuration = 0;
        _identificationBit = 0;
        _lastAccessEnd = 0; _ideInterruptLine=false; _ide?.Reset();
    }
    public void Dispose() => _ide?.Dispose();
}
