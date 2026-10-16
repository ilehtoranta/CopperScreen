using System.Text;
using System.Buffers.Binary;
using Copper68k;
using CopperDisk;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightHostDirectoryTests
{
    [Fact]
    public void LiveNamesMetadataAndReadOnlyProtectionRoundTrip()
    {
        var root = Temporary();
        try
        {
            var backing = new LightweightHostDirectory(new("HOST0", "Exchange", root, false));
            System.IO.File.WriteAllText(Path.Combine(root, "ordinary.txt"), "first");
            Assert.Equal("ordinary.txt", Path.GetFileName(backing.Resolve(root, "host0:ORDINARY.TXT")));
            var mapped = backing.Resolve(root, "CON", true); System.IO.File.WriteAllText(mapped, "device-name"); backing.Record(mapped, "CON", 0x40, "Amiga comment");
            var reopened = new LightweightHostDirectory(new("HOST0", "Exchange", root));
            Assert.Equal(mapped, reopened.Resolve(root, "con"));
            Assert.Equal(new LightweightHostDirectory.Entry("CON", Path.GetFileName(mapped), 0x40, "Amiga comment"), reopened.Describe(mapped));
            Assert.DoesNotContain(reopened.Enumerate(root), e => e.Name == LightweightHostDirectory.MetadataName);
            System.IO.File.WriteAllText(Path.Combine(root, "external.txt"), "host change");
            Assert.Contains(reopened.Enumerate(root), e => e.Name == "external.txt");
            Assert.Equal(214, Assert.Throws<HostDosException>(() => reopened.Record(mapped, "CON", 0)).Error);
            Assert.Throws<HostDosException>(() => backing.Resolve(root, "../outside"));
            Assert.Throws<HostDosException>(() => backing.Resolve(root, "/outside"));
            Assert.Throws<HostDosException>(() => backing.Resolve(root, "C:\\outside"));
            foreach (var name in new[] { "LPT¹.txt", "CONIN$", "trailing.", "star*name", ".copperfs.json" })
            {
                var encoded = backing.Resolve(root, name, true); System.IO.File.WriteAllText(encoded, name); backing.Record(encoded, name);
                Assert.StartsWith(".copperfs-", Path.GetFileName(encoded)); Assert.Equal(encoded, reopened.Resolve(root, name.ToLowerInvariant()));
            }
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void MalformedMetadataAndLinkedObjectsAreNeverFollowed()
    {
        var root = Temporary(); var outside = Temporary();
        try
        {
            System.IO.File.WriteAllText(Path.Combine(root, LightweightHostDirectory.MetadataName), "{broken");
            Assert.Throws<HostDosException>(() => new LightweightHostDirectory(new("HOST0", "Exchange", root)));
            Assert.Equal("{broken", System.IO.File.ReadAllText(Path.Combine(root, LightweightHostDirectory.MetadataName)));
            System.IO.File.Delete(Path.Combine(root, LightweightHostDirectory.MetadataName));
            var backing = new LightweightHostDirectory(new("HOST0", "Exchange", root, false));
            // Junctions require no Windows developer-mode symlink privilege.
            if (OperatingSystem.IsWindows())
            {
                var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{root}\\linked\" \"{outside}\"") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true });
                Assert.NotNull(process); process.WaitForExit(); Assert.Equal(0, process.ExitCode);
                Assert.Throws<HostDosException>(() => backing.Resolve(root, "linked/file.txt", true));
                Assert.Empty(backing.Enumerate(root)); Directory.Delete(Path.Combine(root, "linked"));
            }
        }
        finally { Directory.Delete(root, true); Directory.Delete(outside, true); }
    }

    internal static string Temporary() { var path = Path.Combine(Path.GetTempPath(), "copperfs-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
}

public sealed class LightweightNativeHostDirectoryTests
{
    [NativeHostDirectoryTheory]
    [InlineData("13")] [InlineData("31")]
    public void NativeWorkbenchCopiesDrawerBetweenSharedVolumes(string version)
    {
        var root = LightweightHostDirectoryTests.Temporary(); var exchange = LightweightHostDirectoryTests.Temporary();
        try
        {
            CopyWorkbench(version, root);
            // User icon preferences in disposable supplied OS files establish
            // reproducible coordinates; no ROM or engine behavior is patched.
            var diskIcon = System.IO.File.ReadAllBytes(Path.Combine(Environment.GetEnvironmentVariable("COPPERSCREEN_HOSTFS_WB13")!, "Disk.info"));
            Icon(diskIcon, 500, 140); Drawer(diskIcon, 10, 20, 400, 180); System.IO.File.WriteAllBytes(Path.Combine(root, "Disk.info"), diskIcon);
            Icon(diskIcon, 500, 30); Drawer(diskIcon, 420, 20, 210, 180); System.IO.File.WriteAllBytes(Path.Combine(exchange, "Disk.info"), diskIcon);
            var emptyIcon = System.IO.File.ReadAllBytes(Path.Combine(Environment.GetEnvironmentVariable("COPPERSCREEN_HOSTFS_WB13")!, "Empty.info")); Icon(emptyIcon, 180, 90); System.IO.File.WriteAllBytes(Path.Combine(root, "Empty.info"), emptyIcon);
            Directory.CreateDirectory(Path.Combine(root, "Empty"));
            var contents = Enumerable.Range(0, 70001).Select(i => (byte)(i * 37)).ToArray();
            System.IO.File.WriteAllBytes(Path.Combine(root, "Empty", "workbench-input.bin"), contents);
            using var machine = new LightweightA500Machine(new() { FramebufferWidth = 908, HostDirectories = [new("DH0", "Host", root, false, 0), new("HOST0", "Exchange", exchange, false)] });
            machine.LoadKickstart(System.IO.File.ReadAllBytes(Environment.GetEnvironmentVariable("COPPERSCREEN_KICKSTART" + version + "_ROM")!));
            Frames(1800);
            var bus = machine.HardfileBus!; var library = bus.ReadLong(bus.ReadLong(4) + 0x17A); uint intuition = 0;
            for (var i = 0; i < 128 && bus.IsMappedMemoryRange(library, 40); i++, library = bus.ReadLong(library))
            {
                var address = bus.ReadLong(library + 10); var name = new StringBuilder();
                for (var c = 0; c < 32 && bus.ReadByte(address + (uint)c) != 0; c++) name.Append((char)bus.ReadByte(address + (uint)c));
                if (name.ToString() == "intuition.library") { intuition = library; break; }
            }
            Assert.NotEqual(0u, intuition);
            Move(524, version == "13" ? 160 : 174); Click(); Frames(3); Click(); Frames(120); Capture(machine, $"workbench-open-{version}");
            Move(524, version == "13" ? 52 : 64); Click(); Frames(3); Click(); Frames(120); Capture(machine, $"workbench-destination-{version}");
            // Drawer icon positions are relative to its window's content area.
            Move(240, 130); Button(1); Frames(4); Move(510, 150, 1); Button(0); Frames(600);
            Capture(machine, $"workbench-copy-{version}");
            var result = Path.Combine(exchange, "Empty", "workbench-input.bin");
            Assert.True(System.IO.File.Exists(result), $"Workbench copy failed: PC={machine.Cpu.ProgramCounter:X8}; error={bus.HostFs!.LastError}\n{string.Join('\n', bus.HostFs.DiagnosticPackets)}");
            Assert.Equal(contents, System.IO.File.ReadAllBytes(result)); Assert.Null(machine.UnsupportedActiveFeature);
            void Frames(int count) { for (var i = 0; i < count; i++) machine.ExecuteFrame(); }
            void Button(byte value) => machine.SubmitInput(new(0, 0, value, 0, 0, 0));
            void Click() { Button(1); Frames(3); Button(0); Frames(3); }
            void Move(int x, int y, byte buttons = 0)
            {
                for (var i = 0; i < 250; i++)
                {
                    var screen = bus.ReadLong(intuition + 0x38);
                    var dx = x - (short)bus.ReadWord(screen + 18); var dy = y - (short)bus.ReadWord(screen + 16);
                    if (Math.Abs(dx) <= 2 && Math.Abs(dy) <= 2) return;
                    machine.SubmitInput(new(0, 0, buttons, (short)Math.Clamp(dx, -8, 8), (short)Math.Clamp(dy, -8, 8), 0)); Frames(2);
                }
                Assert.Fail("Native Intuition pointer did not reach the target.");
            }
        }
        finally { Directory.Delete(root, true); Directory.Delete(exchange, true); }
        static void Icon(byte[] icon, int x, int y) { BinaryPrimitives.WriteInt32BigEndian(icon.AsSpan(58), x); BinaryPrimitives.WriteInt32BigEndian(icon.AsSpan(62), y); }
        static void Drawer(byte[] icon, short x, short y, short width, short height)
        { BinaryPrimitives.WriteInt16BigEndian(icon.AsSpan(78), x); BinaryPrimitives.WriteInt16BigEndian(icon.AsSpan(80), y); BinaryPrimitives.WriteInt16BigEndian(icon.AsSpan(82), width); BinaryPrimitives.WriteInt16BigEndian(icon.AsSpan(84), height); }
    }

    private static void CopyWorkbench(string version, string root)
    {
        var source = Environment.GetEnvironmentVariable("COPPERSCREEN_HOSTFS_WB" + version)!;
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(root, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)) System.IO.File.Copy(file, Path.Combine(root, Path.GetRelativePath(source, file)));
    }
    [NativeHostDirectoryMediaTheory]
    [InlineData("13", false)] [InlineData("31", false)] [InlineData("31", true)]
    public void ExchangeFolderCoexistsWithNativeFloppyAndCopperHdfBoot(string version, bool hdf)
    {
        var root = LightweightHostDirectoryTests.Temporary();
        try
        {
            var bytes = Enumerable.Range(0, 70001).Select(i => (byte)(i * 37)).ToArray();
            System.IO.File.WriteAllBytes(Path.Combine(root, "input.bin"), bytes);
            System.IO.File.WriteAllText(Path.Combine(root, "exchange-script"),
                "FailAt 21\nCopy HOST0:input.bin HOST0:output.bin\nMakeDir HOST0:drawer\nRename HOST0:output.bin HOST0:drawer/result.bin\n" +
                "Protect HOST0:drawer/result.bin +s\nFileNote HOST0:drawer/result.bin \"media exchange\"\nSetDate HOST0:drawer/result.bin 01-Jan-90 12:34:56\nList >HOST0:list.txt HOST0:\nLoadWB\n", Encoding.Latin1);
            var source = Environment.GetEnvironmentVariable("COPPERSCREEN_HOSTFS_ADF" + version)!;
            var hardfile = Path.Combine(root, "boot.hdf"); if (hdf) System.IO.File.Copy(source, hardfile);
            using var machine = new LightweightA500Machine(new() { FramebufferWidth = 908,
                Hardfiles = hdf ? [new(0, hardfile, true, mountMode: AmigaHardfileMountMode.Partition,
                    partition: new AmigaHardfilePartitionMetadata { DeviceName = "DH7", DosType = 0x444F5301, BootPriority = 0 })] : [],
                HostDirectories = [new("HOST0", "Exchange", root, false)] });
            machine.LoadKickstart(System.IO.File.ReadAllBytes(Environment.GetEnvironmentVariable("COPPERSCREEN_KICKSTART" + version + "_ROM")!));
            if (!hdf) { machine.MountAdf(System.IO.File.ReadAllBytes(source)); machine.Reset(); }
            for (var frame = 0; frame < 2500; frame++) machine.ExecuteFrame();
            Capture(machine, $"coexist-{version}-{hdf}");
            var result = Path.Combine(root, "drawer", "result.bin");
            Assert.True(System.IO.File.Exists(result), $"PC={machine.Cpu.ProgramCounter:X8}; unsupported={machine.UnsupportedActiveFeature}; error={machine.HardfileBus!.HostFs!.LastError}\n{string.Join('\n', machine.HardfileBus.HostFs.DiagnosticPackets)}");
            Assert.Equal(bytes, System.IO.File.ReadAllBytes(result));
            Assert.Equal("media exchange", new LightweightHostDirectory(new("HOST0", "Exchange", root)).Describe(result).Comment);
            Assert.Equal(new DateTime(1990, 1, 1, 12, 34, 56), System.IO.File.GetLastWriteTime(result));
            Assert.Contains("input.bin", System.IO.File.ReadAllText(Path.Combine(root, "list.txt")), StringComparison.OrdinalIgnoreCase);
            Assert.Null(machine.UnsupportedActiveFeature);
        }
        finally { Directory.Delete(root, true); }
    }
    [NativeHostDirectoryTheory]
    [InlineData("13", 0)] [InlineData("13", 2097152)]
    [InlineData("31", 0)] [InlineData("31", 2097152)]
    [InlineData("31", 2097152, M68kCpuModel.M68040, false)]
    [InlineData("31", 2097152, M68kCpuModel.M68040, true)]
    public void NativeFolderBootWritesAndFreshMachineReopens(string version, int fastRam, M68kCpuModel cpu = M68kCpuModel.M68000, bool jit = false)
    {
        var source = Environment.GetEnvironmentVariable("COPPERSCREEN_HOSTFS_WB" + version)!;
        var root = LightweightHostDirectoryTests.Temporary(); var exchange = LightweightHostDirectoryTests.Temporary();
        try
        {
            CopyWorkbench(version, root);
            var bytes = Enumerable.Range(0, 70001).Select(i => (byte)(i * 37)).ToArray();
            System.IO.File.WriteAllBytes(Path.Combine(exchange, "from-host.bin"), bytes);
            System.IO.File.WriteAllText(Path.Combine(exchange, "live-change.bin"), "before");
            var startup = Path.Combine(root, "S", "Startup-Sequence");
            var commands = "Copy HOST0:live-change.bin SYS:observed-change.bin\nCopy HOST0:from-host.bin SYS:from-host.bin\nCopy SYS:from-host.bin HOST0:from-guest.bin\n" +
                "MakeDir HOST0:drawer\nCopy SYS:from-host.bin HOST0:drawer/renamed.bin\n" +
                "IF EXISTS HOST0:drawer/final.bin\nDelete HOST0:drawer/final.bin\nENDIF\n" +
                "Rename HOST0:drawer/renamed.bin HOST0:drawer/final.bin\nProtect HOST0:drawer/final.bin +s\n" +
                "FileNote HOST0:drawer/final.bin \"preserved comment\"\nSetDate HOST0:drawer/final.bin 01-Jan-90 12:34:56\nList >SYS:host-list.txt HOST0:\n";
            System.IO.File.WriteAllText(startup, System.IO.File.ReadAllText(startup).Replace("LoadWB\n", commands + "LoadWB\n"), Encoding.Latin1);
            var proof = Path.Combine(root, "host-proof.txt"); var reopen = Path.Combine(root, "host-reopen.txt");
            if (System.IO.File.Exists(proof)) System.IO.File.Delete(proof); if (System.IO.File.Exists(reopen)) System.IO.File.Delete(reopen);
            Run(); Assert.Equal("CopperFS native folder boot\n", System.IO.File.ReadAllText(proof)); Assert.False(System.IO.File.Exists(reopen));
            Run(); Assert.Equal("CopperFS reopen verified\n", System.IO.File.ReadAllText(reopen));
            Assert.Equal(bytes, System.IO.File.ReadAllBytes(Path.Combine(exchange, "from-guest.bin")));
            Assert.Equal(bytes, System.IO.File.ReadAllBytes(Path.Combine(root, "from-host.bin")));
            Assert.Equal("host change during native session", System.IO.File.ReadAllText(Path.Combine(root, "observed-change.bin")));
            var metadata = new LightweightHostDirectory(new("HOST0", "Exchange", exchange));
            var final = metadata.Describe(Path.Combine(exchange, "drawer", "final.bin"));
            Assert.Equal(0x40u, final.Protection & 0x40u); Assert.Equal("preserved comment", final.Comment);
            Assert.Equal(new DateTime(1990, 1, 1, 12, 34, 56), System.IO.File.GetLastWriteTime(Path.Combine(exchange, "drawer", "final.bin")));
            Assert.Contains("from-host.bin", System.IO.File.ReadAllText(Path.Combine(root, "host-list.txt")), StringComparison.OrdinalIgnoreCase);
            void Run()
            {
                using var machine = new LightweightA500Machine(new() { CpuModel = cpu, CpuExecutionMode = jit ? M68kExecutionMode.Jit : M68kExecutionMode.Interpreter,
                    FramebufferWidth = 908, FastRamBytes = fastRam, HostDirectories = [new("DH0", "Host", root, false, 0), new("HOST0", "Exchange", exchange, false)] });
                machine.LoadKickstart(System.IO.File.ReadAllBytes(Environment.GetEnvironmentVariable("COPPERSCREEN_KICKSTART" + version + "_ROM")!));
                var firstUnsupported = "";
                var changed = false;
                for (var i = 0; i < 1800; i++)
                {
                    machine.ExecuteFrame();
                    if (!changed && System.IO.File.Exists(proof)) { System.IO.File.WriteAllText(Path.Combine(exchange, "live-change.bin"), "host change during native session"); changed = true; }
                    if (firstUnsupported.Length == 0 && machine.UnsupportedActiveFeature is not null)
                        firstUnsupported = $"frame={i} PC={machine.Cpu.ProgramCounter:X8} BLTCON0={machine.GetCustomRegister(LightweightRegisters.Bltcon0):X4} BLTCON1={machine.GetCustomRegister(LightweightRegisters.Bltcon1):X4} BLTSIZE={machine.GetCustomRegister(LightweightRegisters.Bltsize):X4}";
                }
                var fs = machine.HardfileBus!.HostFs!;
                Capture(machine, $"folder-{version}-{fastRam}-{cpu}-{jit}");
                string Field(string name) => Convert.ToString(typeof(LightweightHostFsController).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(fs))!;
                Assert.True(System.IO.File.Exists(proof), $"Native folder boot failed: PC={machine.Cpu.ProgramCounter:X8}; board={fs.IsConfigured}; copy={Field("_copy")}; registered={Field("_registered")}; error={fs.LastError}\n{string.Join('\n', fs.DiagnosticPackets)}");
                Assert.True(machine.UnsupportedActiveFeature is null, $"{machine.UnsupportedActiveFeature}; {firstUnsupported}; list={System.IO.File.Exists(Path.Combine(root, "host-list.txt"))}; error={fs.LastError}\n{string.Join('\n', fs.DiagnosticPackets)}");
                Assert.True(System.IO.File.GetLastWriteTime(Path.Combine(exchange, "drawer", "final.bin")) == new DateTime(1990, 1, 1, 12, 34, 56), $"Date error={fs.LastError}; {fs.DiagnosticDatePacket}\n{string.Join('\n', fs.DiagnosticPackets)}");
                if (jit) Assert.True(machine.JitStatistics!.Value.CompiledInstructions > 0, machine.JitStatistics.ToString());
            }
        }
        finally { Directory.Delete(root, true); Directory.Delete(exchange, true); }
    }

    private static void Capture(LightweightA500Machine machine, string name)
    {
        var path = Environment.GetEnvironmentVariable("COPPERSCREEN_HOSTFS_CAPTURE"); if (path is null) return;
        Directory.CreateDirectory(path);
        using var writer = new BinaryWriter(System.IO.File.Create(Path.Combine(path, name + ".bmp")));
        var pixels = machine.Framebuffer.Span;
        writer.Write((ushort)0x4D42); writer.Write(54 + pixels.Length * 4); writer.Write(0); writer.Write(54); writer.Write(40);
        writer.Write(machine.FramebufferWidth); writer.Write(-machine.FramebufferHeight); writer.Write((ushort)1); writer.Write((ushort)32);
        writer.Write(0); writer.Write(pixels.Length * 4); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
        foreach (var pixel in pixels) writer.Write(pixel);
    }
}
public sealed class NativeHostDirectoryMediaTheoryAttribute : TheoryAttribute
{
    public NativeHostDirectoryMediaTheoryAttribute()
    {
        if (new[] { "COPPERSCREEN_KICKSTART13_ROM", "COPPERSCREEN_KICKSTART31_ROM", "COPPERSCREEN_HOSTFS_ADF13", "COPPERSCREEN_HOSTFS_ADF31" }.Any(e => !System.IO.File.Exists(Environment.GetEnvironmentVariable(e))))
            Skip = "Native media coexistence unavailable: supply KS1.3/3.1 ROMs and disposable exchange-boot ADFs from prepare-host-directory-native.py.";
    }
}
public sealed class NativeHostDirectoryTheoryAttribute : TheoryAttribute
{
    public NativeHostDirectoryTheoryAttribute()
    {
        if (new[] { "COPPERSCREEN_KICKSTART13_ROM", "COPPERSCREEN_KICKSTART31_ROM" }.Any(e => !System.IO.File.Exists(Environment.GetEnvironmentVariable(e))) ||
            new[] { "COPPERSCREEN_HOSTFS_WB13", "COPPERSCREEN_HOSTFS_WB31" }.Any(e => !Directory.Exists(Environment.GetEnvironmentVariable(e))))
            Skip = "Native folder coverage unavailable: supply KS1.3/3.1 ROMs and fresh Workbench folders from prepare-host-directory-native.py.";
    }
}
