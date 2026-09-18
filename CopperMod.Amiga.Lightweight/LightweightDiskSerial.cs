using CopperDisk;

namespace CopperMod.Amiga.Lightweight;

// H6b1: ideal standard-ADF read input, not an analog Paula PLL model.
// Nominal 300 RPM is independent of ADKCON.FAST and of drive selection.
// All external bit arrivals are sampled on the canonical clock's next CCK.
internal struct LightweightDiskSerial
{
    public LightweightDiskSerial() { }

    internal const int TrackBits = AmigaDosTrackEncoder.EncodedTrackByteCount * 8;
    private LightweightDiskReceiver _receiver;
    internal long NextRecoveredCycle => _receiver.Active ? _receiver.NextCycle : long.MaxValue;
    private int _byteBits;
    private ushort _shift;
    private byte _data;
    private bool _byteReady;
    private bool _wordEqual;
    private byte _slowPhase;
    private int _slowFirstBit;

    internal ushort Shift => _shift;

    internal void Reset()
    {
        _receiver.Reset();
        _byteBits = 0;
        _shift = 0;
        _data = 0;
        _byteReady = _wordEqual = false;
        _slowPhase = 0;
        _slowFirstBit = 0;
    }

    internal void OnDriveChanged(LightweightFloppyDrive drive, long cycle, bool mediaChanged = false)
    {
        if (drive.UpdateRotation(cycle, mediaChanged) && drive.Selected)
            _slowPhase = 0;
    }

    internal void Step(long cycle, LightweightFloppyDrive drive, LightweightA500Machine machine, bool receive = true)
    {
        if (drive.Selected && receive)
        {
            var bit = drive.ReadBit();
            if (drive.Format == LightweightFloppyFormat.Ipf)
            {
                if (_receiver.Active)
                {
                    AdvanceReceiver(cycle, machine);
                    if (bit != 0) _receiver.Pulse();
                }
            }
            else
            {
                if ((machine.Adkcon & 0x0100) == 0 || _slowPhase != 0) bit = RecoverSlowCell(bit);
                if (bit >= 0) Receive(bit, cycle, machine);
            }
        }

        if (drive.AdvanceRotation() && drive.Selected)
            machine.LatchDiskIndex(cycle);
    }

    internal void ConfigureReceiver(long cycle, bool active, bool fast)
    {
        if (active) { _receiver.Enable(cycle, fast); _receiver.SetFast(fast); }
        else if (_receiver.Active) _receiver.Reset();
    }

    internal void AdvanceReceiver(long cycle, LightweightA500Machine machine)
    {
        if (!_receiver.Active) return;
        var bit = _receiver.Advance(cycle);
        _receiver.SetFast((machine.Adkcon & 0x0100) != 0);
        if (bit >= 0) Receive(bit, cycle, machine);
    }

    private void Receive(int bit, long cycle, LightweightA500Machine machine)
    {
        if (_byteBits == 0 && bit == 0 && (machine.Adkcon & 0x0200) != 0) return;
        _shift = (ushort)((_shift << 1) | bit);
        if (++_byteBits == 8)
        {
            _byteBits = 0;
            _data = (byte)_shift;
            _byteReady = true;
        }
        CompareSync(cycle, machine);
        if (machine.DiskDmaActive) machine.ReceiveDiskBit(_shift, _wordEqual, cycle);
    }

    internal void CompareSync(long cycle, LightweightA500Machine machine)
    {
        var equal = _shift == machine.GetCustomRegister(LightweightRegisters.Dsksync);
        if (equal && !_wordEqual)
            machine.LatchDiskSync(cycle);
        _wordEqual = equal;
    }

    // Bounded ideal-ADF model, not a Paula PLL implementation (LWA-DISK-010).
    // WinUAE's 2us-to-4us grouping uses two source cells, or three if the
    // second contains a pulse. Consume them causally, without looking ahead
    // or changing rotation. Publish at completion; an accepted window survives
    // FAST writes. That transition phase still requires hardware verification.
    private int RecoverSlowCell(int bit)
    {
        if (_slowPhase == 0)
        {
            _slowFirstBit = bit;
            _slowPhase = 1;
            return -1;
        }
        if (_slowPhase == 1 && bit != 0)
        {
            _slowPhase = 2;
            return -1;
        }
        var recovered = _slowPhase == 2 ? 1 : _slowFirstBit;
        _slowPhase = 0;
        return recovered;
    }

    internal ushort ReadByteStatus(LightweightRegisters registers)
    {
        var length = registers.Read(LightweightRegisters.Dsklen);
        var value = (ushort)(_data | (_byteReady ? 0x8000 : 0) |
            (_wordEqual ? 0x1000 : 0) | ((length & 0x4000) >> 1) |
            ((length & 0x8000) != 0 && (registers.Dmacon & 0x0210) == 0x0210 ? 0x4000 : 0));
        _byteReady = false;
        return value;
    }

}
