using System.Runtime.CompilerServices;

namespace CopperMod.Amiga.Lightweight;

internal sealed class LightweightCia
{
    internal const byte TimerAInterrupt = 0x01;
    internal const byte TimerBInterrupt = 0x02;
    internal const byte TodInterrupt = 0x04;
    internal const int CpuCyclesPerTick = 10;

    private readonly byte[] _registers = new byte[16];
    private readonly CiaTimer _timerA = new(isTimerB: false);
    private readonly CiaTimer _timerB = new(isTimerB: true);
    private byte _interruptMask;
    private byte _pendingInterrupts;
    private uint _tod;
    private uint _todAlarm;
    private uint _todReadLatch;
    private bool _todReadLatched;
    internal bool TodRunning { get; private set; }
    internal uint TodCounter => _tod;
    private byte _serialShift, _serialBits;
    private bool _serialOutputPending;
    private byte _serialOutputShift, _serialOutputBits;
    private bool _spOutputHigh, _cntOutputHigh = true;
    private bool _spInputHigh = true, _cntInputHigh = true, _flagHigh = true;
    // At most one PRB access per E-clock: retain the active pulse plus the
    // three delayed pulses. No device-loop event is needed for this output pin.
    [InlineArray(4)] private struct PcPulses { private long _first; }
    private PcPulses _pcPulses;
    private int _pcIndex;
    internal bool SerialOutput => (_timerA.Control & 0x40) != 0;
    internal bool SerialSpHigh => !SerialOutput || _spOutputHigh;
    internal bool SerialCntHigh => !SerialOutput || _cntOutputHigh;
    internal bool SerialDataPinHigh => _spInputHigh && SerialSpHigh;
    internal bool SerialClockPinHigh => CntHigh;
    private bool CntHigh => _cntInputHigh && SerialCntHigh;
    private bool SerialActive => _serialOutputPending || _serialOutputBits != 0;
    internal bool PcHigh(long cycle)
    {
        for (var i = 0; i < 4; i++)
            if (cycle >= _pcPulses[i] && cycle - _pcPulses[i] < CpuCyclesPerTick) return false;
        return true;
    }

    private void StrobePc(long cycle)
    {
        _pcPulses[_pcIndex] = cycle + 3 * CpuCyclesPerTick;
        _pcIndex = (_pcIndex + 1) & 3;
    }

    internal byte InterruptMask => _interruptMask;
    internal byte PendingInterrupts => _pendingInterrupts;
    // /IRQ is latched until an ICR read, not a pulse per timer underflow.
    internal bool InterruptAsserted { get; private set; }
    internal ushort TimerACounter => (ushort)_timerA.Counter;
    internal ushort TimerBCounter => (ushort)_timerB.Counter;

    internal void Reset(byte initialPortA = 0, byte initialPortADataDirection = 0, bool preserveInputPins = false)
    {
        Array.Clear(_registers);
        _registers[0] = initialPortA;
        _registers[2] = initialPortADataDirection;
        _timerA.Reset();
        _timerB.Reset();
        _interruptMask = 0;
        _pendingInterrupts = 0;
        InterruptAsserted = false;
        _tod = _todAlarm = _todReadLatch = 0;
        _todReadLatched = false;
        TodRunning = false;
        _serialShift = _serialBits = 0;
        _serialOutputPending = _spOutputHigh = false;
        _serialOutputShift = _serialOutputBits = 0;
        _cntOutputHigh = true;
        if (!preserveInputPins) _spInputHigh = _cntInputHigh = _flagHigh = true;
        for (var i = 0; i < 4; i++) _pcPulses[i] = long.MaxValue;
        _pcIndex = 0;
    }

    internal byte ReadRegister(int register, byte inputPins, long cycle, out long interruptCycle)
    {
        interruptCycle = AdvanceTo(cycle);
        register &= 0x0F;
        if (register == 1) StrobePc(cycle);
        return register switch
        {
            0x00 or 0x01 => ReadPort(register, inputPins, cycle),
            0x04 => (byte)_timerA.Counter,
            0x05 => (byte)(_timerA.Counter >> 8),
            0x06 => (byte)_timerB.Counter,
            0x07 => (byte)(_timerB.Counter >> 8),
            0x08 or 0x09 or 0x0A => ReadTod(register),
            0x0D => ReadInterruptControl(),
            0x0E => _timerA.Control,
            0x0F => _timerB.Control,
            _ => _registers[register]
        };
    }

    internal void WriteRegister(int register, byte value, long cycle, out long interruptCycle)
    {
        interruptCycle = AdvanceTo(cycle);
        register &= 0x0F;
        if (register == 1) StrobePc(cycle);
        switch (register)
        {
            case 0x04:
                _timerA.WriteLatchLow(value);
                break;
            case 0x05:
                _timerA.WriteLatchHigh(value, cycle);
                break;
            case 0x06:
                _timerB.WriteLatchLow(value);
                break;
            case 0x07:
                _timerB.WriteLatchHigh(value, cycle);
                break;
            case 0x08:
            case 0x09:
            case 0x0A:
                WriteTod(register, value);
                break;
            case 0x0D:
                {
                    var bits = (byte)(value & 0x1F);
                    if ((value & 0x80) != 0)
                    {
                        _interruptMask |= bits;
                        if ((_pendingInterrupts & bits & _interruptMask) != 0)
                        {
                            InterruptAsserted = true;
                            interruptCycle = Math.Min(interruptCycle, cycle);
                        }
                    }
                    else
                    {
                        _interruptMask = (byte)(_interruptMask & ~bits);
                    }
                    break;
                }
            case 0x0E:
                if (((value ^ _timerA.Control) & 0x40) != 0)
                {
                    _serialShift = _serialBits = 0;
                    _serialOutputPending = false;
                    _serialOutputBits = 0;
                    _cntOutputHigh = true;
                }
                _timerA.WriteControl(value, cycle);
                break;
            case 0x0C:
                _registers[register] = value;
                if (SerialOutput) _serialOutputPending = true;
                break;
            case 0x0F:
                _timerB.WriteControl(value, cycle);
                break;
            default:
                _registers[register] = value;
                break;
        }
    }

    internal long AdvanceTo(long targetCycle)
    {
        var interruptCycle = long.MaxValue;
        // Only an active transmitter needs individual half-bit edges. At most
        // two buffered bytes are visited; ordinary timers remain arithmetic.
        while (SerialActive)
        {
            var edge = _timerA.GetNextUnderflowCycle();
            if (edge > targetCycle || edge == long.MaxValue) break;
            interruptCycle = Math.Min(interruptCycle, AdvanceTimersTo(edge));
            interruptCycle = Math.Min(interruptCycle, ClockSerialOutput(edge));
        }
        return Math.Min(interruptCycle, AdvanceTimersTo(targetCycle));
    }

    private long AdvanceTimersTo(long targetCycle)
    {
        var cascade = _timerB.CountsTimerAUnderflows && (!_timerB.CntGated || CntHigh);
        var interruptCycle = _timerA.AdvanceTo(
            targetCycle,
            this,
            TimerAInterrupt,
            cascade ? _timerB : null);
        interruptCycle = Math.Min(interruptCycle,
            _timerB.AdvanceTo(targetCycle, this, TimerBInterrupt, null));

        return interruptCycle;
    }

    internal long GetNextActiveInterruptCycle()
    {
        // The serial boundary is observable even when all CIA IRQs are masked.
        var cycle = SerialActive ? _timerA.GetNextUnderflowCycle() : long.MaxValue;
        cycle = Math.Min(cycle, _timerA.GetNextPortTransition());
        cycle = Math.Min(cycle, _timerB.GetNextPortTransition());
        if ((_interruptMask & TimerAInterrupt) != 0 &&
            (_pendingInterrupts & TimerAInterrupt) == 0)
        {
            cycle = Math.Min(cycle, _timerA.GetNextUnderflowCycle());
        }

        if ((_interruptMask & TimerBInterrupt) != 0 &&
            (_pendingInterrupts & TimerBInterrupt) == 0)
        {
            var timerB = _timerB.CountsTimerAUnderflows
                ? (!_timerB.CntGated || CntHigh ? _timerA.GetUnderflowCycleAfterEvents(_timerB.Counter) : long.MaxValue)
                : _timerB.GetNextUnderflowCycle();
            cycle = Math.Min(cycle, timerB);
        }

        if (_timerB.PortEnabled && _timerB.CountsTimerAUnderflows && (!_timerB.CntGated || CntHigh))
            cycle = Math.Min(cycle, _timerA.GetUnderflowCycleAfterEvents(_timerB.Counter));

        return cycle;
    }

    internal byte ReadPortLatch(int register)
        => _registers[register & 1];

    // One accepted external pulse, not a second independently advancing clock.
    // Board pulse placement / CIA debounce are tracked in LWA-CIA-001.
    internal long PulseTod(long cycle)
    {
        if (!TodRunning) return long.MaxValue;
        _tod = (_tod + 1) & 0x00FF_FFFF;
        return _tod == _todAlarm ? SetPending(TodInterrupt, cycle) : long.MaxValue;
    }

    internal long GetNextTodInterruptCycle(long nextPulse, int interval)
    {
        if (nextPulse == long.MaxValue || !TodRunning || (_interruptMask & TodInterrupt) == 0 ||
            (_pendingInterrupts & TodInterrupt) != 0)
            return long.MaxValue;
        var pulses = (_todAlarm - _tod) & 0x00FF_FFFF;
        if (pulses == 0) pulses = 0x0100_0000;
        // A field length can change at VPOSW/LACE. Only predict the next
        // actual field boundary; frame completion revisits more distant alarms.
        if (interval == 0) return pulses == 1 ? nextPulse : long.MaxValue;
        return nextPulse + (pulses - 1L) * interval;
    }

    private byte ReadTod(int register)
    {
        // Alarm selection inhibits a new read latch (8520); reads still
        // return the counter. An already active latch lasts until LSB read.
        if (register == 0x0A && !_todReadLatched && (_timerB.Control & 0x80) == 0)
        {
            _todReadLatch = _tod;
            _todReadLatched = true;
        }
        var value = (byte)((_todReadLatched ? _todReadLatch : _tod) >> ((register - 8) * 8));
        if (register == 8) _todReadLatched = false;
        return value;
    }

    private void WriteTod(int register, byte value)
    {
        var shift = (register - 8) * 8;
        var mask = 0xFFu << shift;
        if ((_timerB.Control & 0x80) != 0)
            _todAlarm = (_todAlarm & ~mask) | ((uint)value << shift);
        else
        {
            _tod = (_tod & ~mask) | ((uint)value << shift);
            if (register == 0x0A) TodRunning = false;
            if (register == 8) TodRunning = true;
        }
    }

    internal long LatchFlag(long cycle) => SetPending(0x10, cycle);

    internal long SetFlagPin(bool high, long cycle)
    {
        var interrupt = AdvanceTo(cycle);
        if (_flagHigh && !high) interrupt = Math.Min(interrupt, LatchFlag(cycle));
        _flagHigh = high;
        return interrupt;
    }

    internal long SetSerialPins(bool spHigh, bool cntHigh, long cycle)
    {
        var interrupt = AdvanceTo(cycle);
        var wasHigh = CntHigh;
        _spInputHigh = spHigh;
        _cntInputHigh = cntHigh;
        return !wasHigh && CntHigh ? Math.Min(interrupt, RisingCnt(cycle)) : interrupt;
    }

    private long RisingCnt(long cycle)
    {
        var interrupt = ReceiveSerialBit(_spInputHigh, cycle);
        if (_timerA.CountsCnt)
        {
            var underflow = _timerA.Running && _timerA.Counter == 1;
            interrupt = Math.Min(interrupt, _timerA.CountExternalEvents(cycle, 0, 1, this, TimerAInterrupt));
            if (underflow && _timerB.CountsTimerAUnderflows)
                interrupt = Math.Min(interrupt, _timerB.CountExternalEvents(cycle, 0, 1, this, TimerBInterrupt));
            if (underflow && SerialActive)
                interrupt = Math.Min(interrupt, ClockSerialOutput(cycle));
        }
        if (_timerB.CountsCnt)
            interrupt = Math.Min(interrupt, _timerB.CountExternalEvents(cycle, 0, 1, this, TimerBInterrupt));
        return interrupt;
    }

    private long ClockSerialOutput(long cycle)
    {
        if (_cntOutputHigh)
        {
            if (_serialOutputBits == 0)
            {
                _serialOutputShift = _registers[12];
                _serialOutputBits = 8;
                _serialOutputPending = false;
            }
            _spOutputHigh = (_serialOutputShift & 0x80) != 0;
            _cntOutputHigh = false;
            return long.MaxValue;
        }
        _cntOutputHigh = true;
        _serialOutputShift <<= 1;
        var interrupt = --_serialOutputBits == 0 ? SetPending(8, cycle) : long.MaxValue;
        if (_cntInputHigh) interrupt = Math.Min(interrupt, RisingCnt(cycle));
        return interrupt;
    }

    internal long ReceiveSerialBit(bool high, long cycle)
    {
        if (SerialOutput) return long.MaxValue;
        _serialShift = (byte)((_serialShift << 1) | (high ? 1 : 0));
        if (++_serialBits != 8) return long.MaxValue;
        _serialBits = 0;
        _registers[12] = _serialShift;
        return SetPending(8, cycle);
    }

    internal byte ReadDataDirection(int register)
        => _registers[(register & 1) + 2];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal byte ReadPort(int register, byte inputPins, long cycle)
    {
        var latch = _registers[register];
        var direction = _registers[register + 2];
        var pins = (byte)((latch & direction) | (inputPins & ~direction));
        if (register == 1)
        {
            if (_timerA.PortEnabled) pins = (byte)((pins & ~0x40) | (_timerA.OutputHigh(cycle) ? 0x40 : 0));
            if (_timerB.PortEnabled) pins = (byte)((pins & ~0x80) | (_timerB.OutputHigh(cycle) ? 0x80 : 0));
        }
        return pins;
    }

    private byte ReadInterruptControl()
    {
        var value = _pendingInterrupts;
        if (InterruptAsserted)
            value |= 0x80;
        _pendingInterrupts = 0;
        InterruptAsserted = false;
        return value;
    }

    private long SetPending(byte bits, long cycle)
    {
        bits &= 0x1F;
        var newBits = (byte)(bits & ~_pendingInterrupts);
        _pendingInterrupts |= bits;
        if ((newBits & _interruptMask) != 0) InterruptAsserted = true;
        return (newBits & _interruptMask) != 0 ? cycle : long.MaxValue;
    }

    private sealed class CiaTimer
    {
        private readonly bool _isTimerB;
        private long _nextTickCycle;
        private long _pulseEnd = long.MinValue;
        private long _advancedCycle;
        private bool _toggleHigh;

        internal CiaTimer(bool isTimerB)
        {
            _isTimerB = isTimerB;
        }

        internal ushort Latch { get; private set; }
        internal int Counter { get; private set; }
        internal byte Control { get; private set; }
        internal bool CountsTimerAUnderflows => _isTimerB && (Control & 0x40) != 0;
        internal bool CntGated => (Control & 0x60) == 0x60;
        internal bool CountsCnt => _isTimerB ? (Control & 0x60) == 0x20 : (Control & 0x20) != 0;
        internal bool PortEnabled => (Control & 2) != 0;
        internal bool OutputHigh(long cycle) => (Control & 4) != 0 ? _toggleHigh : cycle < _pulseEnd;
        internal long GetNextPortTransition() => !PortEnabled ? long.MaxValue :
            Math.Min(GetNextUnderflowCycle(), (Control & 4) == 0 && _pulseEnd > _advancedCycle ? _pulseEnd : long.MaxValue);

        internal bool Running => (Control & 0x01) != 0;
        private bool OneShot => (Control & 0x08) != 0;
        private bool CountsCpu => _isTimerB
            ? (Control & 0x60) == 0
            : (Control & 0x20) == 0;
        private int LatchTicks => Latch == 0 ? 65_536 : Latch;

        internal void Reset()
        {
            Latch = 0;
            Counter = 0;
            Control = 0;
            _nextTickCycle = 0;
            _pulseEnd = long.MinValue;
            _advancedCycle = 0;
            _toggleHigh = false;
        }

        internal void WriteLatchLow(byte value)
        {
            Latch = (ushort)((Latch & 0xFF00) | value);
            if (!Running)
                Counter = LatchTicks;
        }

        internal void WriteLatchHigh(byte value, long cycle)
        {
            Latch = (ushort)((value << 8) | (Latch & 0x00FF));
            if (OneShot)
            {
                Load(cycle);
                if (!Running) _toggleHigh = true;
                Control |= 0x01;
            }
            else if (!Running)
            {
                Load(cycle);
            }
        }

        internal void WriteControl(byte value, long cycle)
        {
            var wasRunning = Running;
            var wasCpu = CountsCpu;
            if ((value & 0x10) != 0)
                Load(cycle);

            Control = (byte)(value & 0xEF);
            if (!wasRunning && Running)
            {
                _toggleHigh = true;
                if (Counter <= 0)
                    Counter = LatchTicks;
                _nextTickCycle = NextTickAfter(cycle);
            }
            else if (Running && !wasCpu && CountsCpu) _nextTickCycle = NextTickAfter(cycle);
        }

        internal long GetNextUnderflowCycle()
        {
            if (!Running || !CountsCpu || Counter <= 0)
                return long.MaxValue;
            return _nextTickCycle + ((Counter - 1L) * CpuCyclesPerTick);
        }

        internal long GetUnderflowCycleAfterEvents(int eventCount)
        {
            if (!Running || !CountsCpu || Counter <= 0 || eventCount <= 0 ||
                (OneShot && eventCount > 1))
            {
                return long.MaxValue;
            }

            var first = _nextTickCycle + ((Counter - 1L) * CpuCyclesPerTick);
            return first + ((long)eventCount - 1L) * LatchTicks * CpuCyclesPerTick;
        }

        internal long AdvanceTo(
            long targetCycle,
            LightweightCia cia,
            byte interruptBit,
            CiaTimer? timerBUnderflowCounter)
        {
            _advancedCycle = targetCycle;
            if (!Running || !CountsCpu || targetCycle < _nextTickCycle)
                return long.MaxValue;

            var interruptCycle = long.MaxValue;
            var underflowCycle = GetNextUnderflowCycle();
            if (underflowCycle <= targetCycle)
            {
                interruptCycle = cia.SetPending(interruptBit, underflowCycle);
                var intervalCycles = (long)LatchTicks * CpuCyclesPerTick;
                var underflows = OneShot
                    ? 1L
                    : 1L + ((targetCycle - underflowCycle) / intervalCycles);
                if (timerBUnderflowCounter != null)
                {
                    interruptCycle = Math.Min(
                        interruptCycle,
                        timerBUnderflowCounter.CountExternalEvents(
                            underflowCycle,
                            intervalCycles,
                            underflows,
                            cia, TimerBInterrupt));
                }

                Counter = LatchTicks;
                var lastUnderflowCycle = underflowCycle + ((underflows - 1L) * intervalCycles);
                UnderflowOutputs(lastUnderflowCycle, underflows);
                _nextTickCycle = lastUnderflowCycle + CpuCyclesPerTick;
                if (OneShot)
                {
                    Control &= 0xFE;
                    return interruptCycle;
                }
            }

            if (targetCycle >= _nextTickCycle)
            {
                var ticks = (int)(((targetCycle - _nextTickCycle) / CpuCyclesPerTick) + 1);
                Counter = Math.Max(0, Counter - ticks);
                _nextTickCycle += ticks * CpuCyclesPerTick;
            }

            return interruptCycle;
        }

        internal long CountExternalEvents(
            long firstCycle,
            long intervalCycles,
            long eventCount,
            LightweightCia cia, byte interruptBit)
        {
            if (!Running || Counter <= 0 || eventCount <= 0)
                return long.MaxValue;

            if (eventCount < Counter)
            {
                Counter -= (int)eventCount;
                if (intervalCycles > 0)
                    _nextTickCycle = NextTickAfter(firstCycle + ((eventCount - 1L) * intervalCycles));
                return long.MaxValue;
            }

            var underflowCycle = firstCycle + ((Counter - 1L) * Math.Max(1, intervalCycles));
            var interruptCycle = cia.SetPending(interruptBit, underflowCycle);
            var underflows = OneShot ? 1 : 1 + (eventCount - Counter) / LatchTicks;
            UnderflowOutputs(underflowCycle + (underflows - 1) * LatchTicks * intervalCycles, underflows);
            if (OneShot)
            {
                Counter = LatchTicks;
                _nextTickCycle = NextTickAfter(underflowCycle);
                Control &= 0xFE;
                return interruptCycle;
            }

            var remainingEvents = eventCount - Counter;
            if (remainingEvents == 0)
            {
                Counter = LatchTicks;
            }
            else
            {
                var remainder = remainingEvents % LatchTicks;
                Counter = remainder == 0 ? LatchTicks : (int)(LatchTicks - remainder);
            }

            if (intervalCycles > 0)
                _nextTickCycle = NextTickAfter(firstCycle + ((eventCount - 1L) * intervalCycles));

            return interruptCycle;
        }

        private void Load(long cycle)
        {
            Counter = LatchTicks;
            _nextTickCycle = NextTickAfter(cycle);
        }

        private void UnderflowOutputs(long lastCycle, long count)
        {
            if ((count & 1) != 0) _toggleHigh = !_toggleHigh;
            _pulseEnd = lastCycle + CpuCyclesPerTick;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long NextTickAfter(long cycle)
            => ((cycle / CpuCyclesPerTick) + 1) * CpuCyclesPerTick;
    }
}
