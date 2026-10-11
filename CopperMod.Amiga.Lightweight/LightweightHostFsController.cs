using System.Text;
using Copper68k;

namespace CopperMod.Amiga.Lightweight;

// Virtual filesystem expansion. No sector emulation and no independent clock.
internal sealed class LightweightHostFsController : AutoconfigBoard, IDisposable
{
    internal const int DiagOffset = 0x4000, DiagSize = 0x1000;
    private const uint PacketGatewayOffset = 0x380;
    // V34 BCPL commands allocate only the fields through fib_Comment. Never
    // touch fib_Reserved: that memory can be the caller's live stack frame.
    private const int FileInfoBytes = 224;
    private readonly byte[] _rom = new byte[65536];
    private readonly List<Volume> _volumes;
    private readonly Dictionary<uint, GuestLock> _locks = new();
    private readonly Dictionary<uint, OpenFile> _files = new();
    private readonly Dictionary<uint, Enumeration> _enumerations = new();
    private uint _nextHandle, _nextEnumeration, _copy, _exec, _expansion, _configDev;
    private LightweightHdfBus _bus = null!;
    private bool _registered;
    internal string? LastError { get; private set; }
#if LIGHTWEIGHT_DIAGNOSTICS
    internal readonly Queue<string> DiagnosticPackets = new();
    internal string? DiagnosticDatePacket;
#endif
    private sealed class Volume(LightweightHostDirectory backing)
    {
        internal LightweightHostDirectory Backing { get; } = backing;
        internal uint Device, Node, Port;
    }
    private sealed record GuestLock(Volume Volume, string Path, int Access);
    private sealed record OpenFile(Volume Volume, string Path, FileStream Stream, int Access);
    private sealed record Enumeration(uint Lock, List<string> Paths) { internal int Index; }

    internal LightweightHostFsController(IReadOnlyList<LightweightHostDirectoryConfiguration> mounts)
        : base(AutoconfigIdentity.CreateIoBoard(65536, 0x07DB, 0x46, DiagOffset))
    {
        if (mounts.Select(m => m.VolumeName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != mounts.Count)
            throw new ArgumentException("Shared folders need unique volume names.");
        _volumes = mounts.Select(m => new Volume(new LightweightHostDirectory(m))).ToList();
        if (_volumes.Select(v => v.Backing.Root).Distinct(StringComparer.OrdinalIgnoreCase).Count() != mounts.Count)
            throw new ArgumentException("Mount each host folder only once in a machine.");
        BuildRom();
    }
    public override bool IsPresent => _volumes.Count != 0;
    public override bool ContainsBoardAddress(uint address) => IsConfigured && address >= ConfiguredBase && address - ConfiguredBase < 65536;
    public override byte ReadBoardByte(uint address) => _rom[address - ConfiguredBase];
    public override bool TryWriteBoardByte(uint address, byte value) => ContainsBoardAddress(address);

    internal void Install(LightweightHdfBus bus)
    {
        CloseFiles(); _locks.Clear(); _enumerations.Clear(); _registered = false; LastError = null;
#if LIGHTWEIGHT_DIAGNOSTICS
        DiagnosticPackets.Clear();
        DiagnosticDatePacket = null;
#endif
        _copy = _exec = _expansion = _configDev = 0;
        foreach (var volume in _volumes) volume.Device = volume.Node = volume.Port = 0;
        _bus = bus;
        Trap(0x20, bus.RegisterBootstrapGateway(Diag, 0x20, () => ConfiguredBase + DiagOffset, DiagSize));
        Trap(0x30, bus.RegisterBootstrapGateway(Boot, 0x30, () => ConfiguredBase + DiagOffset, DiagSize));
        Trap(0x140, bus.RegisterBootstrapGateway(Init, 0x140, () => ConfiguredBase + DiagOffset, DiagSize));
    }

    private void Diag(M68kCpuState cpu)
    {
        _copy = _bus.CpuAddress(cpu.A[2]); _exec = _bus.CpuAddress(cpu.A[6]);
        _expansion = _bus.CpuAddress(cpu.A[5]); _configDev = _bus.CpuAddress(cpu.A[3]);
        Require(_copy, DiagSize);
        var resident = _copy + 0x40;
        _bus.WriteLong(resident + 2, resident); _bus.WriteLong(resident + 6, _copy + 0x146);
        _bus.WriteLong(resident + 14, _copy + 0x100); _bus.WriteLong(resident + 18, _copy + 0x120);
        _bus.WriteLong(resident + 22, _copy + 0x140);
        cpu.D[0] = 1;
    }
    private void Init(M68kCpuState cpu) { Register(); cpu.D[0] = 1; }
    private void Boot(M68kCpuState cpu)
    {
        Register();
        Require(cpu.A[7] - 4, 4);
        cpu.A[7] -= 4; _bus.WriteLong(cpu.A[7], _copy + 0x180); cpu.D[0] = 1;
    }
    private void Register()
    {
        if (_registered || _copy == 0) return;
        var allocator = new CopperHdfController.BootMetadataAllocator(_bus, _copy, _exec);
        foreach (var volume in _volumes)
        {
            uint Alloc(int bytes) { var address = allocator.Allocate(bytes); if (address == 0) throw new HostDosException(103, "No memory for host filesystem."); return address; }
            var configuration = volume.Backing.Configuration;
            volume.Device = Alloc(44); volume.Node = Alloc(44);
            var startup = Alloc(16); var environment = Alloc(17 * 4); var boot = Alloc(32);
            var deviceName = Alloc(configuration.DeviceName.Length + 2); Bstr(deviceName, configuration.DeviceName);
            var volumeName = Alloc(configuration.VolumeName.Length + 2); Bstr(volumeName, configuration.VolumeName);
            _bus.WriteLong(volume.Device + 16, 0);
            _bus.WriteLong(volume.Device + 20, 8192); _bus.WriteLong(volume.Device + 24, 5);
            _bus.WriteLong(volume.Device + 28, startup >> 2); _bus.WriteLong(volume.Device + 32, (_copy + 0x200) >> 2);
            _bus.WriteLong(volume.Device + 36, uint.MaxValue); _bus.WriteLong(volume.Device + 40, deviceName >> 2);
            _bus.WriteLong(startup, (uint)_volumes.IndexOf(volume)); _bus.WriteLong(startup + 8, environment >> 2);
            _bus.WriteLong(environment, 16); _bus.WriteLong(environment + 4, 128);
            _bus.WriteLong(environment + 60, unchecked((uint)(configuration.BootPriority ?? -128)));
            _bus.WriteLong(environment + 64, 0x444F5300);
            _bus.WriteLong(volume.Node + 4, 2); _bus.WriteLong(volume.Node + 32, 0x444F5300);
            _bus.WriteLong(volume.Node + 40, volumeName >> 2);
            _bus.WriteByte(boot + 8, 16); _bus.WriteByte(boot + 9, unchecked((byte)(configuration.BootPriority ?? -128)));
            _bus.WriteLong(boot + 10, configuration.BootPriority.HasValue ? _configDev : 0);
            _bus.WriteLong(boot + 16, volume.Device);
            LinkBoot(boot);
        }
        _bus.RegisterHostGateway(_copy + PacketGatewayOffset, Packet);
        if (_configDev != 0)
        {
            _bus.WriteByte(_configDev + 14, (byte)(_bus.ReadByte(_configDev + 14) & ~2));
            _bus.WriteLong(_configDev + 40, _copy + 0x40);
        }
        _registered = true;
    }
    private void LinkBoot(uint node)
    {
        var list = _expansion + 0x4A; Require(list, 14);
        var next = _bus.ReadLong(list); var previous = list;
        var priority = unchecked((sbyte)_bus.ReadByte(node + 9));
        for (var count = 0; next != list + 4; count++)
        {
            if (count >= 1024) throw new HostDosException(115, "Invalid expansion mount list.");
            Require(next, 10);
            if (unchecked((sbyte)_bus.ReadByte(next + 9)) < priority) break;
            previous = next; next = _bus.ReadLong(next);
        }
        Require(next, 8);
        _bus.WriteLong(node, next); _bus.WriteLong(node + 4, previous);
        _bus.WriteLong(previous, node); _bus.WriteLong(next + 4, node);
    }
    private void Start(Volume volume, uint port)
    {
        if (volume.Port != 0) throw new HostDosException(202, "Handler already started.");
        var library = _bus.ReadLong(_exec + 0x17A);
        uint dos = 0;
        for (var count = 0; count < 128 && library != 0 && _bus.IsMappedMemoryRange(library, 40); count++)
        {
            if (CString(_bus.ReadLong(library + 10), 64).Equals("dos.library", StringComparison.OrdinalIgnoreCase)) { dos = library; break; }
            library = _bus.ReadLong(library);
        }
        if (dos == 0) throw new HostDosException(209, "Native DOS library is unavailable.");
        var root = _bus.ReadLong(dos + 34); Require(root, 28);
        var info = Bptr(_bus.ReadLong(root + 24), 8);
        _bus.WriteLong(volume.Node, _bus.ReadLong(info + 4));
        _bus.WriteLong(volume.Node + 8, port);
        _bus.WriteLong(info + 4, volume.Node >> 2);
        _bus.WriteLong(volume.Device + 8, port); volume.Port = port;
        WriteDate(volume.Node + 16, Directory.GetLastWriteTime(volume.Backing.Root));
    }

    // D2=0 asks for any necessary guest allocation; D2=1 supplies AllocMem's
    // result in D1. Negative D0 requests native FreeMem after a completed packet.
    // Zero D0 means the guest loop can return the packet through native PutMsg.
    internal void Packet(M68kCpuState cpu)
    {
        var packet = cpu.A[4]; cpu.D[0] = 0;
        try
        {
            Require(packet, 48); _currentPort = cpu.A[3];
            var type = unchecked((int)_bus.ReadLong(packet + 8));
            var args = new uint[7]; for (var i = 0; i < 7; i++) args[i] = _bus.ReadLong(packet + 20u + (uint)i * 4);
#if LIGHTWEIGHT_DIAGNOSTICS
            while (DiagnosticPackets.Count >= 30) DiagnosticPackets.Dequeue();
            DiagnosticPackets.Enqueue($"{type}: {string.Join(' ', args.Select(a => a.ToString("X8")))} port={cpu.A[3]:X8} packet={packet:X8} msg={cpu.A[5]:X8} link={_bus.ReadLong(packet):X8} header={string.Join(' ', Enumerable.Range(0, 5).Select(i => _bus.ReadLong(packet + (uint)i * 4).ToString("X8")))}");
            if (type is 8 or 1005 or 1006) DiagnosticPackets.Enqueue($"name={ReadBstr(type == 8 ? args[1] : args[2])}");
            if (type == 34) DiagnosticDatePacket = $"date={args[3]:X8}; direct={_bus.ReadLong(args[3]):X8}/{_bus.ReadLong(args[3] + 4):X8}/{_bus.ReadLong(args[3] + 8):X8}; bptr={_bus.ReadLong(args[3] << 2):X8}/{_bus.ReadLong((args[3] << 2) + 4):X8}/{_bus.ReadLong((args[3] << 2) + 8):X8}";
#endif
            // V34 sends CreateProc's launch message before the filesystem
            // startup packet. Its third longword is the supplied SegList,
            // followed by stack size, rather than dp_Type/dp_Res1. Acknowledge
            // it without interpreting or overwriting those launch parameters.
            if (type == (int)((_copy + 0x200) >> 2) && _bus.ReadLong(packet + 12) == 8192)
            {
                var launchVolume = _volumes.FirstOrDefault(v => _bus.ReadLong(v.Device + 28) == args[1]);
                if (launchVolume is null) throw new HostDosException(211, "Invalid V34 filesystem startup.");
                Start(launchVolume, cpu.A[3]); return;
            }
            var volume = type == 0 ? _volumes.FirstOrDefault(v => v.Device == Bptr(args[2], 44)) : _volumes.FirstOrDefault(v => v.Port == cpu.A[3]);
            if (volume is null) throw new HostDosException(211, $"Unknown filesystem handler: packet={type}, arg3={args[2]:X8}, port={cpu.A[3]:X8}, device={_volumes[0].Device:X8}.");
            var needsLock = type is 8 or 19 or 22 or 29 or 1030 or 1031;
            if (needsLock && cpu.D[2] == 0) { cpu.D[0] = 24; return; }
            var allocated = needsLock ? cpu.D[1] : 0;
            if (needsLock) RequireAllocation(allocated);
            try
            {
                var result = Execute(volume, type, args, allocated);
                _bus.WriteLong(packet + 12, unchecked((uint)result)); _bus.WriteLong(packet + 16, 0);
                if (needsLock && !_locks.ContainsKey(allocated >> 2)) { cpu.D[0] = unchecked((uint)-24); cpu.D[1] = allocated; }
                if (type == 15 && args[0] != 0 || type == 1026 && args[1] != 0)
                { cpu.D[0] = unchecked((uint)-24); cpu.D[1] = (type == 15 ? args[0] : args[1]) << 2; }
            }
            catch
            {
                if (needsLock && !_locks.ContainsKey(allocated >> 2)) { cpu.D[0] = unchecked((uint)-24); cpu.D[1] = allocated; }
                throw;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or OverflowException)
        {
            LastError = ex.Message;
            if (_bus.IsMappedMemoryRange(packet, 20))
            {
                var type = _bus.ReadLong(packet + 8);
                _bus.WriteLong(packet + 12, type is 82 or 87 or 1008 or 1022 ? uint.MaxValue : 0);
                _bus.WriteLong(packet + 16, (uint)Error(ex));
            }
        }
    }
    private static int Error(Exception ex) => ex switch
    { HostDosException dos => dos.Error, FileNotFoundException or DirectoryNotFoundException => 205,
      UnauthorizedAccessException => 223, ArgumentException or OverflowException => 115,
      IOException io when (io.HResult & 0xFFFF) is 2 or 3 => 205,
      IOException io when (io.HResult & 0xFFFF) is 5 => 223,
      IOException io when (io.HResult & 0xFFFF) is 80 or 183 => 203,
      IOException io when (io.HResult & 0xFFFF) is 112 => 221,
      IOException io when (io.HResult & 0xFFFF) is 145 => 216,
      IOException io when (io.HResult & 0xFFFF) is 32 or 33 => 202, _ => 209 };

    private int Execute(Volume volume, int type, uint[] a, uint allocated)
    {
        var backing = volume.Backing;
        string Resolve(uint parent, uint name, bool missing = false)
        {
            // DOS already resolved device/volume/assign prefixes into this
            // handler and base lock. An assign such as LIBS: remains in BSTR.
            var text = ReadBstr(name); var colon = text.IndexOf(':');
            return backing.Resolve(Lock(volume, parent).Path, colon >= 0 ? text[(colon + 1)..] : text, missing);
        }
        switch (type)
        {
            case 0: Start(volume, volume.Port == 0 ? _currentPort : volume.Port); return -1;
            case 8:
            {
                // DOS RKRM requires unknown access modes to behave as shared
                // locks. Workbench 1.3's drawer copier actually sends one.
                var path = Resolve(a[0], a[1]); var access = a[2] == uint.MaxValue ? -1 : -2;
                Conflict(volume, path, access); return MakeLock(volume, path, access, allocated);
            }
            case 15: if (a[0] != 0) ReleaseLock(volume, a[0]); return -1;
            case 19: { var l = Lock(volume, a[0]); if (l.Access == -1) throw new HostDosException(202, "Cannot duplicate exclusive lock."); return MakeLock(volume, l.Path, -2, allocated); }
            case 29: { var path = Lock(volume, a[0]).Path; return path.Equals(backing.Root, StringComparison.OrdinalIgnoreCase) ? 0 : MakeLock(volume, Path.GetDirectoryName(path)!, -2, allocated); }
            case 40: return Lock(volume, a[0]).Path.Equals(Lock(volume, a[1]).Path, StringComparison.OrdinalIgnoreCase) ? -1 : 0;
            case 23: Examine(volume, Lock(volume, a[0]).Path, Bptr(a[1], FileInfoBytes), a[0]); return -1;
            case 24:
            {
                var l = Lock(volume, a[0]); var fib = Bptr(a[1], FileInfoBytes); var key = _bus.ReadLong(fib);
                if (!_enumerations.TryGetValue(key, out var enumeration) || enumeration.Lock != a[0]) throw new HostDosException(232, "No more entries.");
                if (enumeration.Index == enumeration.Paths.Count) { _enumerations.Remove(key); throw new HostDosException(232, "No more entries."); }
                Examine(volume, enumeration.Paths[enumeration.Index++], fib, null); _bus.WriteLong(fib, key); return -1;
            }
            case 1004: case 1005: case 1006:
            {
                var fh = Bptr(a[0], 44); var path = Resolve(a[1], a[2], type != 1005);
                if (Directory.Exists(path)) throw new HostDosException(212, "Cannot open a directory as a file.");
                var exists = System.IO.File.Exists(path); if (type != 1005 || !exists) backing.Writable();
                if (exists) { if (type != 1006) backing.Protected(path, 8, 224); if (type != 1005) backing.Protected(path, 4, 223); if (type == 1006) backing.Protected(path, 1, 222); }
                var access = type == 1006 ? -1 : -2; Conflict(volume, path, access);
                using var lease = backing.Lease(Path.GetDirectoryName(path)!);
                if (exists) LightweightHostDirectory.RejectLink(path);
                var fileAccess = backing.Configuration.ReadOnly || exists && (backing.Describe(path).Protection & 4) != 0 ? FileAccess.Read : FileAccess.ReadWrite;
                FileStream stream;
                try { stream = backing.Open(path, exists ? FileMode.Open : FileMode.CreateNew, fileAccess); }
                catch (Exception ex) when (type == 1005 && (ex is UnauthorizedAccessException || ex is IOException io && (io.HResult & 0xFFFF) == 5))
                { stream = backing.Open(path, FileMode.Open, FileAccess.Read); }
                try
                {
                    backing.ValidateHandle(stream.SafeFileHandle);
                    if (stream.Length > int.MaxValue) throw new HostDosException(219, "Classic DOS file exceeds 2 GiB.");
                    if (type == 1006) stream.SetLength(0);
                    if (!exists) backing.Record(path, LastComponent(ReadBstr(a[2])));
                    var handle = Next(ref _nextHandle);
                    _files.Add(handle, new(volume, path, stream, access));
                    _bus.WriteLong(fh + 8, volume.Port); _bus.WriteLong(fh + 36, handle);
                }
                catch { stream.Dispose(); throw; }
                return -1;
            }
            case 82: case 87:
            {
                var file = File(volume, a[0]);
                var length = checked((int)a[2]); if (length == 0) return 0; Require(a[1], length);
                if (type == 87) { backing.Writable(); backing.Protected(file.Path, 4, 223); if (!file.Stream.CanWrite) throw new HostDosException(223, "File is write protected.");
                    if (file.Stream.Position + length > int.MaxValue) throw new HostDosException(219, "Classic DOS file exceeds 2 GiB."); }
                else backing.Protected(file.Path, 8, 224);
                var buffer = new byte[Math.Min(65536, length)]; var actual = 0;
                while (actual < length)
                {
                    var count = Math.Min(buffer.Length, length - actual);
                    if (type == 87) { _bus.CopyFromMemory(a[1] + (uint)actual, buffer.AsSpan(0, count)); file.Stream.Write(buffer, 0, count); }
                    else { count = file.Stream.Read(buffer, 0, count); if (count == 0) break; _bus.CopyToMemory(a[1] + (uint)actual, buffer.AsSpan(0, count)); }
                    actual += count;
                }
                return actual;
            }
            case 1008:
            {
                var stream = File(volume, a[0]).Stream; var old = stream.Position;
                var position = Position(stream, unchecked((int)a[1]), unchecked((int)a[2]));
                if (position > stream.Length) throw new HostDosException(219, "Seek past end of file.");
                stream.Position = position; return checked((int)old);
            }
            case 1022:
            {
                var file = File(volume, a[0]); backing.Writable(); backing.Protected(file.Path, 4, 223);
                if (!file.Stream.CanWrite) throw new HostDosException(223, "File is write protected.");
                var size = Position(file.Stream, unchecked((int)a[1]), unchecked((int)a[2]));
                // DOS keeps the positions of other open handles valid when
                // truncating. Flush their pending bytes before resizing.
                foreach (var other in _files.Values.Where(f => f != file && f.Volume == volume && f.Path.Equals(file.Path, StringComparison.OrdinalIgnoreCase)))
                { other.Stream.Flush(); size = Math.Max(size, other.Stream.Position); }
                file.Stream.SetLength(size); if (file.Stream.Position > size) file.Stream.Position = size; return checked((int)size);
            }
            case 1007: { var file = File(volume, a[0]); try { file.Stream.Flush(true); } finally { _files.Remove(a[0]); file.Stream.Dispose(); } return -1; }
            case 22:
            {
                backing.Writable(); var path = Resolve(a[0], a[1], true);
                if (Directory.Exists(path) || System.IO.File.Exists(path)) throw new HostDosException(203, "Object already exists.");
                using var lease = backing.Lease(Path.GetDirectoryName(path)!); Directory.CreateDirectory(path);
                backing.Record(path, LastComponent(ReadBstr(a[1]))); return MakeLock(volume, path, -1, allocated);
            }
            case 16:
            {
                backing.Writable(); var path = Resolve(a[0], a[1]); MutableObject(volume, path); backing.Protected(path, 1, 222);
                using var lease = backing.Lease(Path.GetDirectoryName(path)!);
                if (Directory.Exists(path))
                {
                    using var drawer = backing.Lease(path);
                    _ = backing.ReadMetadata(path);
                    if (Directory.EnumerateFileSystemEntries(path).Any(p => !Path.GetFileName(p).Equals(LightweightHostDirectory.MetadataName, StringComparison.OrdinalIgnoreCase)))
                        throw new HostDosException(216, "Directory is not empty.");
                    // Only the owned metadata file may be removed with an empty drawer.
                    var metadata = Path.Combine(path, LightweightHostDirectory.MetadataName);
                    if (System.IO.File.Exists(metadata)) backing.DeleteObject(metadata);
                }
                backing.DeleteObject(path);
                backing.Forget(path); return -1;
            }
            case 17:
            {
                backing.Writable(); var source = Resolve(a[0], a[1]); MutableObject(volume, source); backing.Protected(source, 1, 222);
                var destination = Resolve(a[2], a[3], true);
                if (source.Equals(destination, StringComparison.OrdinalIgnoreCase))
                { backing.Record(source, LastComponent(ReadBstr(a[3]))); return -1; }
                if (Directory.Exists(destination) || System.IO.File.Exists(destination)) throw new HostDosException(203, "Destination already exists.");
                var old = backing.Describe(source);
                using var from = backing.Lease(Path.GetDirectoryName(source)!); using var to = backing.Lease(Path.GetDirectoryName(destination)!);
                backing.RenameObject(source, destination);
                backing.Record(destination, LastComponent(ReadBstr(a[3])), old.Protection, old.Comment); backing.Forget(source); return -1;
            }
            case 21: case 28: case 34:
            {
                backing.Writable(); var path = Resolve(a[1], a[2]); var entry = backing.Describe(path);
                using var lease = backing.Lease(Directory.Exists(path) ? path : Path.GetDirectoryName(path)!);
                if (type == 21) backing.Record(path, entry.Name, a[3]);
                else if (type == 28) { var comment = ReadBstr(a[3]); if (comment.Length > 79 || comment.Any(c => c < 32)) throw new HostDosException(220, "Comment too long or invalid."); backing.Record(path, entry.Name, comment: comment); }
                // Native V34/V40 send an APTR (which can be word-aligned),
                // despite the BPTR label in some packet reference tables.
                else { Require(a[3], 12); backing.SetDate(path, ReadDate(a[3])); }
                return -1;
            }
            case 25: Info(volume, Bptr(a[0], 36)); return -1;
            case 26: _ = Lock(volume, a[0]); Info(volume, Bptr(a[1], 36)); return -1;
            case 27: foreach (var file in _files.Values.Where(f => f.Volume == volume)) file.Stream.Flush(true); return -1;
            case 1027: return -1;
            case 1026:
            {
                var fh = Bptr(a[0], 44); var l = Lock(volume, a[1]);
                if (Directory.Exists(l.Path)) throw new HostDosException(212, "Cannot convert a directory lock to a file handle.");
                backing.Protected(l.Path, 8, 224);
                using var lease = backing.Lease(Path.GetDirectoryName(l.Path)!);
                FileStream stream;
                try { stream = backing.Open(l.Path, FileMode.Open, backing.Configuration.ReadOnly || (backing.Describe(l.Path).Protection & 4) != 0 ? FileAccess.Read : FileAccess.ReadWrite); }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException io && (io.HResult & 0xFFFF) == 5)
                { stream = backing.Open(l.Path, FileMode.Open, FileAccess.Read); }
                try
                {
                    backing.ValidateHandle(stream.SafeFileHandle);
                    if (stream.Length > int.MaxValue) throw new HostDosException(219, "Classic DOS file exceeds 2 GiB.");
                    var handle = Next(ref _nextHandle);
                    ReleaseLock(volume, a[1]); _files.Add(handle, new(volume, l.Path, stream, l.Access));
                    _bus.WriteLong(fh + 8, volume.Port); _bus.WriteLong(fh + 36, handle);
                }
                catch { stream.Dispose(); throw; } return -1;
            }
            case 1034: Examine(volume, File(volume, a[0]).Path, Bptr(a[1], FileInfoBytes), null); return -1;
            case 1030: { var file = File(volume, a[0]); if (file.Access == -1) throw new HostDosException(202, "Cannot duplicate exclusive file access."); return MakeLock(volume, file.Path, -2, allocated, a[0]); }
            case 1031: return MakeLock(volume, Path.GetDirectoryName(File(volume, a[0]).Path)!, -2, allocated);
            case 1028:
            {
                var mode = unchecked((int)a[2]) switch { -1 or 1006 => -1, -2 or 1004 or 1005 => -2, _ => throw new HostDosException(115, "Invalid lock mode.") };
                if (a[0] == 0) { if (a[1] == 0) throw new HostDosException(211, "Cannot change a null lock."); var l = Lock(volume, a[1]); Conflict(volume, l.Path, mode, a[1]); _locks[a[1]] = l with { Access = mode }; _bus.WriteLong((a[1] << 2) + 8, unchecked((uint)mode)); }
                else if (a[0] == 1) { var key = _bus.ReadLong(Bptr(a[1], 44) + 36); var f = File(volume, key); Conflict(volume, f.Path, mode, file: key); _files[key] = f with { Access = mode }; }
                else throw new HostDosException(115, "Invalid change-mode object."); return -1;
            }
            default: throw new HostDosException(209, $"Unsupported host-filesystem packet {type}.");
        }
    }
    private uint _currentPort;
    private GuestLock Lock(Volume volume, uint key)
    {
        if (key == 0) return new(volume, volume.Backing.Root, -2);
        if (!_locks.TryGetValue(key, out var l) || l.Volume != volume) throw new HostDosException(211, "Invalid or stale lock.");
        return l;
    }
    private OpenFile File(Volume volume, uint key) => _files.TryGetValue(key, out var f) && f.Volume == volume ? f : throw new HostDosException(211, "Invalid or stale file handle.");
    private void Conflict(Volume volume, string path, int mode, uint except = 0, uint file = 0)
    {
        if (_locks.Any(p => p.Key != except && p.Value.Volume == volume && p.Value.Path.Equals(path, StringComparison.OrdinalIgnoreCase) && (mode == -1 || p.Value.Access == -1)) ||
            _files.Any(p => p.Key != file && p.Value.Volume == volume && p.Value.Path.Equals(path, StringComparison.OrdinalIgnoreCase) && (mode == -1 || p.Value.Access == -1)))
            throw new HostDosException(202, "Object is in use.");
    }
    private int MakeLock(Volume volume, string path, int access, uint address, uint exceptFile = 0)
    {
        Conflict(volume, path, access, file: exceptFile);
        RequireAllocation(address); using var lease = volume.Backing.Lease(Directory.Exists(path) ? path : Path.GetDirectoryName(path)!);
        var key = address >> 2; if (_locks.ContainsKey(key)) throw new HostDosException(211, "Reused lock allocation.");
        _bus.ClearMemory(address, 24); _bus.WriteLong(address, _bus.ReadLong(volume.Node + 28));
        _bus.WriteLong(address + 4, Next(ref _nextHandle)); _bus.WriteLong(address + 8, unchecked((uint)access));
        _bus.WriteLong(address + 12, volume.Port); _bus.WriteLong(address + 16, volume.Node >> 2);
        _bus.WriteLong(volume.Node + 28, key); _locks.Add(key, new(volume, path, access)); return unchecked((int)key);
    }
    private void ReleaseLock(Volume volume, uint key)
    {
        _ = Lock(volume, key);
        var link = volume.Node + 28;
        for (var guard = 0; guard <= _locks.Count; guard++)
        {
            var next = _bus.ReadLong(link); if (next == 0) break;
            var pointer = Bptr(next, 20);
            if (next == key) { _bus.WriteLong(link, _bus.ReadLong(pointer)); break; }
            link = pointer;
        }
        _locks.Remove(key);
        foreach (var id in _enumerations.Where(e => e.Value.Lock == key).Select(e => e.Key).ToArray()) _enumerations.Remove(id);
    }
    private void MutableObject(Volume volume, string path)
    {
        if (path.Equals(volume.Backing.Root, StringComparison.OrdinalIgnoreCase)) throw new HostDosException(202, "Cannot remove the volume root.");
        if (_locks.Values.Any(l => l.Volume == volume && (l.Path.Equals(path, StringComparison.OrdinalIgnoreCase) || l.Path.StartsWith(path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) ||
            _files.Values.Any(f => f.Volume == volume && (f.Path.Equals(path, StringComparison.OrdinalIgnoreCase) || f.Path.StartsWith(path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))))
            throw new HostDosException(202, "Object is in use.");
    }
    private void Examine(Volume volume, string path, uint fib, uint? lockKey)
    {
        Require(fib, FileInfoBytes); var backing = volume.Backing; var directory = Directory.Exists(path);
        if (!directory && !System.IO.File.Exists(path)) throw new HostDosException(205, "Object not found.");
        using var lease = backing.Lease(directory ? path : Path.GetDirectoryName(path)!);
        var entry = backing.Describe(path); _bus.ClearMemory(fib, FileInfoBytes);
        var kind = directory ? path.Equals(backing.Root, StringComparison.OrdinalIgnoreCase) ? 1 : 2 : -3;
        _bus.WriteLong(fib + 4, unchecked((uint)kind)); Bstr(fib + 8, entry.Name);
        _bus.WriteLong(fib + 116, entry.Protection); _bus.WriteLong(fib + 120, unchecked((uint)kind));
        var size = directory ? 0 : new FileInfo(path).Length; if (size > int.MaxValue) throw new HostDosException(219, "File exceeds classic DOS size.");
        _bus.WriteLong(fib + 124, (uint)size); _bus.WriteLong(fib + 128, (uint)((size + 511) / 512));
        WriteDate(fib + 132, directory ? Directory.GetLastWriteTime(path) : System.IO.File.GetLastWriteTime(path)); Bstr(fib + 144, entry.Comment);
        if (directory && lockKey.HasValue)
        {
            foreach (var key in _enumerations.Where(e => e.Value.Lock == lockKey).Select(e => e.Key).ToArray()) _enumerations.Remove(key);
            var id = Next(ref _nextEnumeration); _enumerations.Add(id, new(lockKey.Value, backing.Enumerate(path).Select(e => Path.Combine(path, e.HostName)).ToList())); _bus.WriteLong(fib, id);
        }
    }
    private void Info(Volume volume, uint info)
    {
        Require(info, 36); _bus.ClearMemory(info, 36);
        var drive = new DriveInfo(Path.GetPathRoot(volume.Backing.Root)!);
        _bus.WriteLong(info + 8, volume.Backing.Configuration.ReadOnly ? 80u : 82u);
        _bus.WriteLong(info + 12, (uint)Math.Min(int.MaxValue, drive.TotalSize / 512));
        _bus.WriteLong(info + 16, (uint)Math.Min(int.MaxValue, (drive.TotalSize - drive.AvailableFreeSpace) / 512));
        _bus.WriteLong(info + 20, 512); _bus.WriteLong(info + 24, 0x444F5300); _bus.WriteLong(info + 28, volume.Node >> 2);
        _bus.WriteLong(info + 32, _locks.Values.Any(l => l.Volume == volume) || _files.Values.Any(f => f.Volume == volume) ? uint.MaxValue : 0);
    }
    private static long Position(FileStream stream, int offset, int mode)
    {
        var position = checked((mode switch { -1 => 0L, 0 => stream.Position, 1 => stream.Length, _ => throw new HostDosException(115, "Invalid seek origin.") }) + offset);
        if (position is < 0 or > int.MaxValue) throw new HostDosException(219, "Invalid file position."); return position;
    }
    private void Require(uint address, int bytes)
    { if (address == 0 || bytes < 0 || !_bus.IsMappedMemoryRange(address, bytes)) throw new HostDosException(115, "Guest address is outside RAM."); }
    private void RequireAllocation(uint address) { if (address == 0) throw new HostDosException(103, "No guest memory."); Require(address, 24); if ((address & 3) != 0) throw new HostDosException(115, "Unaligned guest allocation."); }
    private uint Bptr(uint pointer, int bytes, bool readOnly = false)
    { if (pointer > 0x3FFFFFFF) throw new HostDosException(115, "BPTR overflow."); var address = pointer << 2;
      if (readOnly) { if (address == 0 || !_bus.IsReadableMemoryRange(address, bytes)) throw new HostDosException(115, "Guest string is outside readable memory."); }
      else Require(address, bytes); return address; }
    private string ReadBstr(uint bptr)
    { var address = Bptr(bptr, 1, true); var size = _bus.ReadByte(address); _ = Bptr(bptr, size + 1, true); var data = new byte[size]; for (var i = 0; i < size; i++) data[i] = _bus.ReadByte(address + 1 + (uint)i); return Encoding.Latin1.GetString(data); }
    private string CString(uint address, int maximum)
    { var text = new StringBuilder(); for (var i = 0; i < maximum; i++) { if (!_bus.IsReadableMemoryRange(address + (uint)i, 1)) return ""; var c = _bus.ReadByte(address + (uint)i); if (c == 0) return text.ToString(); text.Append((char)c); } return ""; }
    private void Bstr(uint address, string text)
    { Require(address, text.Length + 1); _bus.WriteByte(address, checked((byte)text.Length)); _bus.CopyToMemory(address + 1, Encoding.Latin1.GetBytes(text)); }
    private static string LastComponent(string path) => path[(Math.Max(path.LastIndexOf('/'), path.LastIndexOf(':')) + 1)..];
    private static uint Next(ref uint id) { if (id == uint.MaxValue) throw new HostDosException(103, "Handle space exhausted."); return ++id; }
    private void WriteDate(uint address, DateTime date)
    { var ticks = Math.Max(0, (date - new DateTime(1978, 1, 1)).Ticks / (TimeSpan.TicksPerSecond / 50)); _bus.WriteLong(address, (uint)(ticks / (86400 * 50L))); _bus.WriteLong(address + 4, (uint)(ticks / 3000 % 1440)); _bus.WriteLong(address + 8, (uint)(ticks % 3000)); }
    private DateTime ReadDate(uint address)
    { var days = _bus.ReadLong(address); var minutes = _bus.ReadLong(address + 4); var ticks = _bus.ReadLong(address + 8); if (days > 100000 || minutes >= 1440 || ticks >= 3000) throw new HostDosException(115, "Invalid DateStamp."); return new DateTime(1978, 1, 1).AddDays(days).AddMinutes(minutes).AddTicks(ticks * (TimeSpan.TicksPerSecond / 50)); }
    private void CloseFiles()
    {
        List<Exception>? errors = null;
        foreach (var file in _files.Values)
        {
            try { file.Stream.Flush(true); } catch (Exception ex) { (errors ??= []).Add(ex); }
            try { file.Stream.Dispose(); } catch (Exception ex) { (errors ??= []).Add(ex); }
        }
        _files.Clear(); if (errors is not null) throw new AggregateException("Shared-folder shutdown failed to flush one or more files.", errors);
    }
    public void Dispose() => CloseFiles();

    private void Trap(int offset, uint token) { Word(offset, 0xFF00); Long(offset + 2, token); }
    private void Word(int offset, ushort value) { _rom[DiagOffset + offset] = (byte)(value >> 8); _rom[DiagOffset + offset + 1] = (byte)value; }
    private void Long(int offset, uint value) { Word(offset, (ushort)(value >> 16)); Word(offset + 2, (ushort)value); }
    private void Text(int offset, string value) { Encoding.ASCII.GetBytes(value).CopyTo(_rom, DiagOffset + offset); }
    private void BuildRom()
    {
        Word(0, 0x9000); Word(2, DiagSize); Word(4, 0x20); Word(6, 0x30); Word(8, 0x100);
        Word(0x40, 0x4AFC); Long(0x42, 0x40); Long(0x46, 0x146);
        Word(0x4A, 0x0101); Word(0x4C, 0x0305); Long(0x4E, 0x100); Long(0x52, 0x120); Long(0x56, 0x140);
        Text(0x100, "copperfs.handler"); Text(0x120, "CopperFS 1.0");
        ushort[] boot = [0x2C78, 4, 0x43FA, 0x002A, 0x4EAE, 0xFFA0, 0x4A80, 0x670A, 0x2040, 0x2068, 0x0016, 0x7000, 0x4ED0, 0x7000, 0x4E75];
        for (var i = 0; i < boot.Length; i++) Word(0x180 + i * 2, boot[i]); Text(0x1B0, "dos.library");
        Long(0x1FC, 0x190); Long(0x200, 0); // native segment header; entry at +4
        var words = new List<ushort>(); var labels = new Dictionary<string, int>(); var patches = new List<(int, string)>();
        void Emit(params ushort[] values) => words.AddRange(values);
        void Label(string name) => labels.Add(name, 0x204 + words.Count * 2);
        void Branch(ushort opcode, string label) { Emit(opcode, 0); patches.Add((words.Count - 1, label)); }
        // A3 is this Process's native message port; A4 current DosPacket.
        Emit(0x2C78, 4, 0x93C9, 0x4EAE, 0xFEDA, 0x2640, 0x47EB, 0x005C);
        Label("wait"); Emit(0x204B, 0x4EAE, 0xFE80, 0x204B, 0x4EAE, 0xFE8C, 0x4A80); Branch(0x6700, "wait");
        Emit(0x2A40, 0x286D, 10, 0x7200, 0x7400);
        Label("call"); Branch(0x4EBA, "gateway"); Emit(0x4A80); Branch(0x6700, "reply"); Branch(0x6B00, "free");
        Emit(0x223C, 1, 1, 0x4EAE, 0xFF3A, 0x2200, 0x7401); Branch(0x6000, "call");
        Label("free"); Emit(0x4480, 0x2241, 0x4EAE, 0xFF2E);
        Label("reply"); Emit(0x206C, 4, 0x294B, 4, 0x2254, 0x4EAE, 0xFE92); Branch(0x6000, "wait");
        labels.Add("gateway", (int)PacketGatewayOffset);
        foreach (var (index, label) in patches) words[index] = unchecked((ushort)(labels[label] - (0x204 + index * 2)));
        for (var i = 0; i < words.Count; i++) Word(0x204 + i * 2, words[i]);
    }
}
