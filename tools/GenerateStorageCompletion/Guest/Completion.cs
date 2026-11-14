using Amiga;
using CopperSharp.Compiler;
using CopperSharp.Sdk.Amiga;

namespace CopperScreen.Storage;

// Real guest Exec calls. The host only publishes completed requests and IRQ2.
public static class Completion
{
    // Link both explicit register entries. The controller enters them directly.
    public static uint Root()
    {
        var context = APTR.FromPointer(0x100);
        var exec = APTR.FromPointer(APTR.ReadUInt32(APTR.FromPointer(4), 0));
        return APTR.ReadUInt32(context, 12) == 0 ? Initialize(context, exec) : Interrupt(context);
    }
    [AmigaIndirectCall(M68kRegister.A5)]
    private static extern void Add([M68kRegister(M68kRegister.A5)] APTR code,
        [M68kRegister(M68kRegister.D0)] uint number, [M68kRegister(M68kRegister.A1)] APTR node,
        [M68kRegister(M68kRegister.A6)] APTR exec);
    [AmigaIndirectCall(M68kRegister.A5)]
    private static extern void Reply([M68kRegister(M68kRegister.A5)] APTR code,
        [M68kRegister(M68kRegister.A1)] APTR request, [M68kRegister(M68kRegister.A6)] APTR exec);

    [M68kInternalRegisterAbi]
    public static uint Initialize([M68kRegister(M68kRegister.A1)] APTR context,
        [M68kRegister(M68kRegister.A6)] APTR exec)
    {
        Add(APTR.FromPointer(exec.Raw - 168), 3, APTR.FromPointer(APTR.ReadUInt32(context, 4)), exec);
        return APTR.ReadUInt32(context, 8);
    }

    [M68kInternalRegisterAbi]
    public static uint Interrupt([M68kRegister(M68kRegister.A1)] APTR context)
    {
        // Servers on an Exec chain receive A6 as scratch, unlike exclusive
        // handlers. Another server may have changed it before our entry.
        var exec=APTR.FromPointer(APTR.ReadUInt32(APTR.FromPointer(4),0));
        var register = APTR.FromPointer(APTR.ReadUInt32(context, 0) + 0x6000);
        while (true)
        {
            var request = APTR.ReadUInt32(register, 0);
            if (request == 0) break;
            Reply(APTR.FromPointer(exec.Raw - 378), APTR.FromPointer(request), exec);
        }
        return 0;
    }
}
