using System.Buffers.Binary;
using System.Text;

namespace CopperScreen;

internal sealed class CopperScreenMinimalBoot
{
    internal const uint RomBase = 0xFC0000, StateAddress = 0x75800, BootAddress = 0x6800;
    internal byte[] Rom { get; }
    internal uint FaultAddress { get; }
    internal CopperScreenMinimalBoot(CopperScreenAdfImage disk, bool slowRam, bool a1200 = false)
    {
        ValidateDisk(disk);
        var assembler = new CopperScreenBootAssembler(RomBase);
        var code = assembler.Assemble(BuildSource(a1200));
        Rom = new byte[262144];
        code.CopyTo(Rom, 0);
        BinaryPrimitives.WriteUInt32BigEndian(Rom.AsSpan((int)(assembler.Symbol("slow_config") - RomBase)), slowRam ? 1u : 0u);
        FaultAddress = assembler.Symbol("fault");
    }
    internal static string BuildSource(bool a1200 = false)
    {
        using var stream = typeof(CopperScreenMinimalBoot).Assembly.GetManifestResourceStream("CopperScreen.Firmware.MinimalDiskBoot.s")!;
        using var reader = new StreamReader(stream);
        // Specialize our shared firmware before assembly. These are guest instructions,
        // not host callbacks. Keep the 68000 path free of 020-only control registers.
        // MOVEC D0,VBR ($4e7b,$0801) and D0,CACR ($4e7b,$0002) reset
        // vector relocation and disable the instruction cache before RAM takeover.
        var source = new StringBuilder($"chipphysical equ ${(a1200 ? 0x200000 : 0x80000):x}\n" +
            $"chipupperend equ ${(a1200 ? 0x200000 : 0x7c000):x}\n" +
            $"groupzero_pc_offset equ {(a1200 ? 2 : 10)}\n" +
            $"cpuattention equ {(a1200 ? 3 : 0)}\n" +
            reader.ReadToEnd()
                .Replace("; @cpu_setup", a1200 ? "moveq #0,d0\n dc.w $4e7b,$0801\n dc.w $4e7b,$0002\n move.w #0,$dff1fc\n move.w #$0001,$dff100\n move.w #0,$dff106\n move.w #0,$dff10c" : "")
                .Replace("; @chip_extension", a1200 ? "move.l #chipupper,chipfirst\n clr.l chipupper\n move.l #chipphysical-chipupper,chipupper+4\n addi.l #chipphysical-chipupper,chipheader+28" : "")
                .Replace("; @boot_frame", a1200 ? "clr.w -(sp)" : ""));
        var exec = new Dictionary<int, string>
        {
            [30] = "supervisor", [120] = "disable", [126] = "enable", [132] = "forbid", [138] = "permit",
            [198] = "allocmem", [210] = "freemem", [216] = "availmem",
            [294] = "findtask",
            [246] = "addtail_public", [252] = "remport", [354] = "addport", [360] = "remport",
            [408] = "oldopenlibrary", [414] = "closelibrary", [444] = "opendevice", [450] = "close_device",
            [456] = "doio", [474] = "waitio", [552] = "openlibrary"
        };
        // Asynchronous requests/message ports require an OS scheduler. Do not fake completion.
        var graphics = new Dictionary<int, string> { [222] = "loadview", [270] = "waittof" };
        source.AppendLine("oldopenlibrary:\n moveq #0,d0\n bra openlibrary");
        AddVectors(source, "exec", exec, 0);
        AddVectors(source, "gfx", graphics, 0x8000);
        return source.ToString();
    }
    private static void AddVectors(StringBuilder source, string name, Dictionary<int, string> services, int libraryId)
    {
        source.AppendLine(name + "_vectors:");
        for (var offset = 768; offset >= 6; offset -= 6)
            source.AppendLine($" dc.w $4ef9\n dc.l {(services.TryGetValue(offset, out var handler) ? handler : name + "_missing_" + offset)}");
        for (var offset = 6; offset <= 768; offset += 6)
            if (!services.ContainsKey(offset))
                source.AppendLine($"{name}_missing_{offset}:\n move.w #${libraryId | offset:x4},state\n move.l (sp),state+4\n bra fault");
    }
    internal static void ValidateDisk(CopperScreenAdfImage disk)
    {
        if (disk.Format != CopperMod.Amiga.Lightweight.LightweightFloppyFormat.Adf)
            throw new NotSupportedException("Minimal disk boot requires a standard 880 KiB ADF in DF0. Use a native Kickstart ROM for IPF disks.");
        if (!disk.Data.AsSpan(0, 3).SequenceEqual("DOS"u8))
            throw new NotSupportedException("The DF0 disk has no DOS boot-block identifier. Use a native Kickstart ROM for this disk.");
        uint sum = 0;
        for (var i = 0; i < 1024; i += 4)
        {
            var value = BinaryPrimitives.ReadUInt32BigEndian(disk.Data.AsSpan(i, 4));
            var next = unchecked(sum + value);
            if (next < sum) next = unchecked(next + 1);
            sum = next;
        }
        if (sum != uint.MaxValue)
            throw new NotSupportedException("The DF0 boot-block checksum is invalid. Minimal disk boot will not execute it.");
    }
    internal static string DescribeFault(ushort service, uint caller, uint detail = 0) => service switch
    {
        0xffff => $"Minimal disk boot: guest exception at ${caller:X6}.",
        0xfffe => "Minimal disk boot: the boot block returned to the OS. DOS and Workbench require a native Kickstart ROM.",
        0xf001 => $"Minimal disk boot: invalid FreeMem at ${caller:X6}.",
        0xf002 => $"Minimal disk boot: unsupported trackdisk command {detail} at ${caller:X6}.",
        0xf003 => "Minimal disk boot: DF0 could not be read or has no valid boot block.",
        0xf004 => $"Minimal disk boot: trackdisk read into ${detail:X6} overlaps resident boot services at ${caller:X6}. Use a native Kickstart ROM.",
        0xf005 => $"Minimal disk boot: invalid CloseLibrary at ${caller:X6}.",
        0x8111 => $"Minimal disk boot: LoadView requires a non-null view at ${caller:X6}. Use a native Kickstart ROM.",
        _ => $"Minimal disk boot: unsupported {(service >= 0x8000 ? "graphics.library" : "exec.library")} LVO -{service & 0x7fff} at ${caller:X6}. Use a native Kickstart ROM."
    };
}
