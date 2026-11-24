namespace CopperMod.Amiga.Lightweight;

// Host deltas are accepted counter transitions at SubmitInput's current cycle.
// Analog pots and sub-CCK quadrature transitions are not modeled here.
internal struct LightweightControllers
{
    private byte _mouseX, _mouseY;
    private ushort _joy1Upper;
    private byte _joy0, _joy1, _buttons;
    private bool _dongleFireHigh, _dongleVerticalBit;

    internal void Submit(LightweightInputState input)
    {
        _mouseX = unchecked((byte)(_mouseX + input.MouseDeltaX));
        _mouseY = unchecked((byte)(_mouseY + input.MouseDeltaY));
        _joy0 = input.JoystickPort0;
        _joy1 = input.JoystickPort1;
        _buttons = input.MouseButtons;
    }

    internal ushort ReadJoy0(bool padConnected) => (_joy0 & 0x80) != 0 || padConnected
        ? (ushort)((((_mouseY << 8) | _mouseX) & 0xFCFC) | Directions(_joy0))
        : (ushort)((_mouseY << 8) | _mouseX);

    internal ushort ReadJoy1(bool roboCop3Dongle)
    {
        var directions = Directions(_joy1);
        return roboCop3Dongle
            ? (ushort)((directions ^ (_dongleVerticalBit ? 0x0100 : 0)) & 0x0101)
            : (ushort)(_joy1Upper | directions);
    }

    internal void WriteDongleFire(byte latch, byte direction)
    {
        var high = (latch & 0x80) != 0;
        if ((direction & 0x80) != 0 && high && !_dongleFireHigh)
            _dongleVerticalBit = !_dongleVerticalBit;
        _dongleFireHigh = high;
    }

    private static ushort Directions(byte state)
    {
        var up = state & 1;
        var down = (state >> 1) & 1;
        var left = (state >> 2) & 1;
        var right = (state >> 3) & 1;
        return (ushort)((left << 9) | ((left ^ up) << 8) | (right << 1) | (right ^ down));
    }

    internal void WriteJoytest(ushort value)
    {
        _mouseX = (byte)((_mouseX & 3) | (value & 0xFC));
        _mouseY = (byte)((_mouseY & 3) | ((value >> 8) & 0xFC));
        _joy1Upper = (ushort)(value & 0xFCFC);
    }

    internal byte ReadFirePins(bool padConnected = false)
    {
        var fire0 = (_joy0 & 0x80) != 0 || padConnected ? (_joy0 & 0x10) != 0 : (_buttons & 1) != 0;
        return (byte)(0xFF & ~(fire0 ? 0x40 : 0) & ~((_joy1 & 0x10) != 0 ? 0x80 : 0));
    }

    internal ushort ReadPotgor(ushort potgo, bool padConnected = false)
    {
        var pins = 0x5500;
        var port0Joystick = (_joy0 & 0x80) != 0 || padConnected;
        if (port0Joystick ? (_joy0 & 0x20) != 0 : (_buttons & 2) != 0) pins &= ~0x0400;
        if ((_buttons & 4) != 0 && !port0Joystick) pins &= ~0x0100;
        if ((_joy1 & 0x20) != 0) pins &= ~0x4000;
        var lowOutputs = (potgo >> 1) & ~potgo & 0x5500;
        return (ushort)(pins & ~lowOutputs);
    }
}
