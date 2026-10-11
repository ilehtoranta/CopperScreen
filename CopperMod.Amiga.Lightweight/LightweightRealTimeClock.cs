/*
 * Copyright (C) 2026 Ilkka Lehtoranta
 * SPDX-License-Identifier: MIT
 */

using System;

namespace CopperMod.Amiga.Lightweight
{
    // Ported from Legacy Machine/RealTimeClock.cs (CopperMod 39b183d6).
    // Bounded A501 register model: host local time plus a guest-set session
    // offset. HOLD/STOP retain a coherent calendar while native software writes
    // its digits. Physical BUSY/divider/interrupt phases remain unmodeled.
    internal sealed class LightweightRealTimeClock
    {
        public const uint BaseAddress = 0x00DC_0000;
        public const uint ByteLength = 0x40;

        private const byte ControlF24HourMode = 0x04;

        private readonly Func<DateTimeOffset> _nowProvider;
        private TimeSpan _offset;
        private byte _controlD;
        private byte _controlE;
        private byte _controlF = ControlF24HourMode;
        private DateTimeOffset? _heldTime;
        private readonly byte[] _heldRegisters = new byte[13];
        private bool _heldDirty;

        public LightweightRealTimeClock(Func<DateTimeOffset>? nowProvider = null)
        {
            _nowProvider = nowProvider ?? (() => DateTimeOffset.Now);
        }

        internal LightweightRealTimeClock CreateRestartClock(Func<DateTimeOffset>? nowProvider)
        {
            var copy = new LightweightRealTimeClock(nowProvider ?? _nowProvider)
            {
                _offset = _offset, _heldTime = _heldTime, _heldDirty = _heldDirty,
                _controlD = _controlD, _controlE = _controlE, _controlF = _controlF
            };
            _heldRegisters.CopyTo(copy._heldRegisters, 0);
            return copy;
        }

        public void ResetControlRegisters()
        {
            if (_heldTime.HasValue) EndHold();
            _controlD = 0;
            _controlE = 0;
            _controlF = ControlF24HourMode;
        }

        public static bool ContainsAddress(uint address)
        {
            address &= 0x00FF_FFFF;
            return address >= BaseAddress && address < BaseAddress + ByteLength;
        }

        public bool TryReadByte(uint address, out byte value)
        {
            if (!ContainsAddress(address))
            {
                value = 0;
                return false;
            }

            value = ReadRegister(GetRegister(address));
            return true;
        }

        public bool TryWriteByte(uint address, byte value)
        {
            if (!ContainsAddress(address))
            {
                return false;
            }

            WriteRegister(GetRegister(address), (byte)(value & 0x0F));
            return true;
        }

        private static int GetRegister(uint address)
            => (int)((address >> 2) & 0x0F);

        private byte ReadRegister(int register)
        {
            if (register <= 0xC && _heldTime.HasValue) return _heldRegisters[register];
            var now = GetCurrentDateTime();
            var year = now.Year % 100;
            return register switch
            {
                0x0 => Ones(now.Second),
                0x1 => Tens(now.Second),
                0x2 => Ones(now.Minute),
                0x3 => Tens(now.Minute),
                0x4 => Ones(GetDisplayHour(now.Hour)),
                0x5 => ReadHourTens(now.Hour),
                0x6 => Ones(now.Day),
                0x7 => Tens(now.Day),
                0x8 => Ones(now.Month),
                0x9 => Tens(now.Month),
                0xA => Ones(year),
                0xB => Tens(year),
                0xC => (byte)((int)now.DayOfWeek & 0x07),
                0xD => (byte)(_controlD & 0x0D),
                0xE => (byte)(_controlE & 0x0F),
                0xF => (byte)(_controlF & 0x0F),
                _ => 0
            };
        }

        private void WriteRegister(int register, byte value)
        {
            if (register <= 0x0B)
            {
                if (_heldTime.HasValue)
                {
                    _heldRegisters[register] = value;
                    _heldDirty = true;
                    return;
                }
                WriteTimeRegister(register, value);
                return;
            }

            if (register == 0x0D)
            {
                _controlD = (byte)(value & 0x0D);
            }
            else if (register == 0x0E)
            {
                _controlE = value;
            }
            else if (register == 0x0F)
            {
                _controlF = value;
            }
            // OKI MSM6242B CD.HOLD and CF.STOP inhibit calendar updates.
            // Stage individual digits so an intermediate non-leap year cannot
            // truncate a guest's valid February 29 before its year is written.
            var held = (_controlD & 1) != 0 || (_controlF & 2) != 0;
            if (held && !_heldTime.HasValue) BeginHold();
            else if (!held && _heldTime.HasValue) EndHold();
        }

        private void BeginHold()
        {
            // Sample once for the complete calendar, before publishing HOLD.
            var time = _nowProvider() + _offset;
            var hour = GetDisplayHour(time.Hour);
            byte[] digits = _heldRegisters;
            digits[0] = Ones(time.Second); digits[1] = Tens(time.Second);
            digits[2] = Ones(time.Minute); digits[3] = Tens(time.Minute);
            digits[4] = Ones(hour); digits[5] = ReadHourTens(time.Hour);
            digits[6] = Ones(time.Day); digits[7] = Tens(time.Day);
            digits[8] = Ones(time.Month); digits[9] = Tens(time.Month);
            digits[10] = Ones(time.Year % 100); digits[11] = Tens(time.Year % 100);
            digits[12] = (byte)time.DayOfWeek;
            _heldTime = time;
            _heldDirty = false;
        }

        private void EndHold()
        {
            var time = _heldTime!.Value;
            if (_heldDirty)
            {
                int Number(int ones) => Math.Min(_heldRegisters[ones], (byte)9) +
                    10 * Math.Min(_heldRegisters[ones + 1], (byte)9);
                var year = ExpandAmigaClockYear(Number(10));
                var month = Math.Clamp(Number(8), 1, 12);
                var day = Math.Clamp(Number(6), 1, DateTime.DaysInMonth(year, month));
                var hour = Uses24HourMode ? Math.Clamp(Number(4), 0, 23) :
                    Math.Clamp(_heldRegisters[4] + 10 * (_heldRegisters[5] & 1), 1, 12) % 12 +
                    ((_heldRegisters[5] & 4) != 0 ? 12 : 0);
                time = new DateTimeOffset(year, month, day, hour,
                    Math.Clamp(Number(2), 0, 59), Math.Clamp(Number(0), 0, 59), time.Offset);
            }
            _offset = time - _nowProvider();
            _heldTime = null;
            _heldDirty = false;
        }

        private DateTime GetCurrentDateTime()
            => (_heldTime ?? (_nowProvider() + _offset)).DateTime;

        private void WriteTimeRegister(int register, byte value)
        {
            var hostNow = _nowProvider();
            var current = hostNow + _offset;
            var dateTime = current.DateTime;
            var year = dateTime.Year;
            var month = dateTime.Month;
            var day = dateTime.Day;
            var hour = dateTime.Hour;
            var minute = dateTime.Minute;
            var second = dateTime.Second;

            switch (register)
            {
                case 0x0:
                    second = ReplaceBcdOnes(second, value, 0, 59);
                    break;
                case 0x1:
                    second = ReplaceBcdTens(second, value, 0, 59);
                    break;
                case 0x2:
                    minute = ReplaceBcdOnes(minute, value, 0, 59);
                    break;
                case 0x3:
                    minute = ReplaceBcdTens(minute, value, 0, 59);
                    break;
                case 0x4:
                    hour = ReplaceBcdOnes(hour, value, 0, 23);
                    break;
                case 0x5:
                    hour = ReplaceBcdTens(hour, (byte)(value & 0x03), 0, 23);
                    break;
                case 0x6:
                    day = ReplaceBcdOnes(day, value, 1, 31);
                    break;
                case 0x7:
                    day = ReplaceBcdTens(day, value, 1, 31);
                    break;
                case 0x8:
                    month = ReplaceBcdOnes(month, value, 1, 12);
                    break;
                case 0x9:
                    month = ReplaceBcdTens(month, value, 1, 12);
                    break;
                case 0xA:
                    year = ExpandAmigaClockYear(ReplaceBcdOnes(year % 100, value, 0, 99));
                    break;
                case 0xB:
                    year = ExpandAmigaClockYear(ReplaceBcdTens(year % 100, value, 0, 99));
                    break;
            }

            day = Math.Min(day, DateTime.DaysInMonth(year, month));
            var updated = new DateTimeOffset(year, month, day, hour, minute, second, current.Offset);
            _offset = updated - hostNow;
        }

        private int GetDisplayHour(int hour)
        {
            if (Uses24HourMode)
            {
                return hour;
            }

            var display = hour % 12;
            return display == 0 ? 12 : display;
        }

        private byte ReadHourTens(int hour)
        {
            if (Uses24HourMode)
            {
                return Tens(hour);
            }

            var display = GetDisplayHour(hour);
            var pm = hour >= 12 ? 0x04 : 0x00;
            return (byte)(Tens(display) | pm);
        }

        private bool Uses24HourMode => (_controlF & ControlF24HourMode) != 0;

        private static byte Ones(int value)
            => (byte)(Math.Abs(value) % 10);

        private static byte Tens(int value)
            => (byte)((Math.Abs(value) / 10) % 10);

        private static int ReplaceBcdOnes(int current, byte ones, int minimum, int maximum)
        {
            var updated = (current / 10 * 10) + Math.Min(ones, (byte)9);
            return Math.Clamp(updated, minimum, maximum);
        }

        private static int ReplaceBcdTens(int current, byte tens, int minimum, int maximum)
        {
            var updated = (Math.Min(tens, (byte)9) * 10) + (current % 10);
            return Math.Clamp(updated, minimum, maximum);
        }

        private static int ExpandAmigaClockYear(int year)
            => year >= 78 ? 1900 + year : 2000 + year;
    }
}
