namespace CopperMod.Amiga.Lightweight;

[Flags]
public enum LightweightCd32Buttons : byte
{
    None = 0, Play = 1, Reverse = 2, Forward = 4, Green = 8,
    Yellow = 16, Blue = 32, Red = 64
}

internal sealed class LightweightCd32Pads
{
    private readonly Port[] _ports = new Port[2];

    internal bool IsConnected(int port) => _ports[port].Buttons.HasValue;

    internal void SetButtons(int port, LightweightCd32Buttons? buttons, ushort potgo)
    {
        ref var state = ref _ports[port];
        if (state.Buttons.HasValue != buttons.HasValue)
        {
            state = new Port { Serial = IsSerial(port, potgo), Bit = 8 };
        }
        state.Buttons = buttons;
    }

    internal void Reset(bool cold)
    {
        for (var port = 0; port < _ports.Length; port++)
        {
            var buttons = _ports[port].Buttons;
            _ports[port] = new Port { Buttons = cold && buttons.HasValue ? LightweightCd32Buttons.None : buttons, Bit = 8 };
        }
    }

    internal void WritePotgo(ushort value)
    {
        for (var port = 0; port < _ports.Length; port++)
        {
            ref var state = ref _ports[port];
            var serial = IsSerial(port, value);
            if (!serial || !state.Serial) state.Bit = 8;
            state.Serial = serial;
        }
    }

    internal void WriteCiaPort(byte latch, byte output)
    {
        for (var port = 0; port < _ports.Length; port++)
        {
            ref var state = ref _ports[port];
            var pin = 0x40 << port;
            var clockHigh = (latch & output & pin) != 0;
            if (state.Serial && state.ClockHigh && (output & pin) != 0 && !clockHigh && state.Bit > 0)
                state.Bit--;
            state.ClockHigh = clockHigh;
        }
    }

    internal byte FirePins(byte pins)
    {
        for (var port = 0; port < _ports.Length; port++)
        {
            var state = _ports[port];
            if (state.Buttons is { } buttons && !state.Serial && (buttons & LightweightCd32Buttons.Red) != 0)
                pins &= (byte)~(0x40 << port);
        }
        return pins;
    }

    internal ushort PotPins(ushort pins)
    {
        for (var port = 0; port < _ports.Length; port++)
        {
            var state = _ports[port];
            if (state.Buttons is not { } buttons) continue;
            var pressed = state.Serial
                ? state.Bit == 0 || (state.Bit >= 2 && ((byte)buttons & (1 << (state.Bit - 2))) != 0)
                : (buttons & LightweightCd32Buttons.Blue) != 0;
            if (pressed) pins &= (ushort)~(0x0400 << (port * 4));
        }
        return pins;
    }

    private static bool IsSerial(int port, ushort potgo)
        => ((potgo >> (port * 4)) & 0x0300) == 0x0200;

    private struct Port
    {
        internal LightweightCd32Buttons? Buttons;
        internal bool Serial;
        internal bool ClockHigh;
        internal int Bit;
    }
}
