using System.Buffers.Binary;
using Copper68k;

namespace CopperMod.Amiga.Lightweight;

public sealed partial class LightweightA500Machine
{
    // CopperStart bootstrap ABI 1: a guest-emitted setup stub, before Exec/DOS.
    // Recognition runs only through the existing CPU gateway consumer boundary.
    private const uint CopperStartBootstrapToken = 0x43534731;

    private bool IsCopperStartBootstrapGateway(uint address) => address == 0x780 && _romLoaded &&
        BinaryPrimitives.ReadUInt16BigEndian(_chipRam.AsSpan(0x780, 2)) == 0xFF00 &&
        BinaryPrimitives.ReadUInt32BigEndian(_chipRam.AsSpan(0x782, 4)) == CopperStartBootstrapToken;

    private void HandleCopperStartBootstrap(M68kCpuState state)
    {
        const uint receipt = 0x760, requestMagic = 0x43534231, replyMagic = 0x43504D31, version = 1;
        var request = state.D[0];
        state.D[0] = 0;
        var stack = state.A[7];
        if (state.LastInstructionProgramCounter != 0x780 || state.ProgramCounter != 0x786 ||
            (state.StatusRegister & M68kCpuState.Supervisor) == 0 || request != requestMagic ||
            state.D[1] != version || state.A[0] != receipt || (stack & 1) != 0 ||
            stack < 0x2000 || stack > (uint)_chipRam.Length - 4)
            return;
        var caller = BinaryPrimitives.ReadUInt32BigEndian(_chipRam.AsSpan((int)stack, 4));
        var data = _chipRam.AsSpan((int)receipt, 32);
        if ((caller & 1) != 0 || caller < RomBase || caller - RomBase >= (uint)_rom.Length ||
            BinaryPrimitives.ReadUInt32BigEndian(data) != requestMagic ||
            BinaryPrimitives.ReadUInt32BigEndian(data[4..]) != version ||
            BinaryPrimitives.ReadUInt32BigEndian(data[12..]) != 1)
            return;
        var challenge = BinaryPrimitives.ReadUInt32BigEndian(data[8..]);
        BinaryPrimitives.WriteUInt32BigEndian(data[16..], replyMagic);
        BinaryPrimitives.WriteUInt32BigEndian(data[20..], version);
        BinaryPrimitives.WriteUInt32BigEndian(data[24..], challenge ^ replyMagic ^ version);
        BinaryPrimitives.WriteUInt32BigEndian(data[28..], 1);
        NotifyJitRamWrite(receipt + 16, 16);
        state.D[0] = replyMagic;
    }
}
