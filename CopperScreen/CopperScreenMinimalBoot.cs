using System.Buffers.Binary;
using System.Text;

namespace CopperScreen;

internal sealed class CopperScreenMinimalBoot
{
    internal const uint RomBase = 0xFC0000, StateAddress = 0x75800, BootAddress = 0x6800;
    internal byte[] Rom { get; }
    internal uint FaultAddress { get; }
    internal CopperScreenMinimalBoot(CopperScreenAdfImage disk, bool slowRam, int chipRamBytes = 524288, bool ec020 = false)
    {
        ValidateDisk(disk);
        var assembler = new CopperScreenBootAssembler(RomBase);
        var code = assembler.Assemble(BuildSource(chipRamBytes, ec020));
        Rom = new byte[262144];
        code.CopyTo(Rom, 0);
        BinaryPrimitives.WriteUInt32BigEndian(Rom.AsSpan((int)(assembler.Symbol("slow_config") - RomBase)), slowRam ? 1u : 0u);
        FaultAddress = assembler.Symbol("fault");
    }
    internal static string BuildSource(int chipRamBytes = 524288, bool ec020 = false)
    {
        if (chipRamBytes is not (524288 or 1048576 or 2097152)) throw new ArgumentOutOfRangeException(nameof(chipRamBytes));
        using var stream = typeof(CopperScreenMinimalBoot).Assembly.GetManifestResourceStream("CopperScreen.Firmware.MinimalDiskBoot.s")!;
        using var reader = new StreamReader(stream);
        // Specialize immutable fitted memory and CPU traits once, before execution.
        // The A500 instruction stream stays byte-identical. Resident boot services
        // keep their low-memory addresses; extended Chip RAM is a second free chunk.
        var firmware = reader.ReadToEnd()
            .Replace("chiplimit equ $80000", $"chiplimit equ ${chipRamBytes:x}")
            .Replace("chipfree equ $69000", $"chipfree equ ${0x69000 + chipRamBytes - 0x80000:x}")
            .Replace("; FITTED_CHIP_INIT", chipRamBytes > 524288 ? $"""
                move.l #$80000,chipfirst
                clr.l $80000
                move.l #${chipRamBytes - 0x80000:x},$80004
                """ : "")
            .Replace("; FITTED_CHIP_FREE", chipRamBytes > 524288 ? """
                cmpi.l #$80000,d0
                bcs freetryslow
                cmpi.l #chiplimit,d3
                bls freepool
                freetryslow:
                """ : "")
            .Replace("; CPU_BOOT_FRAME", ec020 ? "clr.w -(sp)" : "")
            .Replace("; CPU_FLAGS", ec020 ? "move.w #3,exec+296" : "")
            .Replace("groupzero_pc equ 10", ec020 ? "groupzero_pc equ 2" : "groupzero_pc equ 10");
        var source = new StringBuilder(firmware);
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
        if (disk.Format != CopperMod.Amiga.Lightweight.LightweightFloppyFormat.Adf || disk.RequiresHighDensityDrive)
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
