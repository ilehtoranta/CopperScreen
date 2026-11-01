using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Copper68k;
using CopperDisk;
using CopperMod.Amiga.Lightweight;

// Diagnostic reader for the pinned development muimaster.library 4.1 ABI.
// Guest memory is observed only. All interaction uses ordinary machine input.
// This is a correctness workload, never a throughput acceptance measurement.
if (args.Length != 5) throw new ArgumentException("ROM HDF new-output hdf-ro|hdf-ffs max-fields");
var output = Path.GetFullPath(args[2]);
if (Directory.Exists(output)) throw new ArgumentException("Use a new output directory");
Directory.CreateDirectory(output);
var rom = File.ReadAllBytes(args[0]);
var mediaHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[1])));
var media = args[3] switch
{
    "hdf-ro" => new AmigaHardfileConfiguration(0, args[1], true),
    "hdf-ffs" => new AmigaHardfileConfiguration(0, args[1], true,
        mountMode: AmigaHardfileMountMode.Partition,
        partition: AmigaFfsPartition.ParseGeometry("DH0:1:32:0:1023")),
    _ => throw new ArgumentException("Unknown media mode")
};
using var machine = new LightweightA500Machine(configuration: new LightweightA500Configuration
{
    ChipRamBytes = 524288, SlowRamBytes = 524288, FastRamBytes = 4194304,
    FramebufferWidth = 908,
    Hardfiles = [media], CpuModel = M68kCpuModel.M68000,
    AgnusModel = LightweightAgnusModel.Mos8371, DeniseModel = LightweightDeniseModel.Mos8362,
}); // Public constructor uses the production conservative CPU loop batch.
machine.LoadKickstart(rom);
var slow = (byte[])typeof(LightweightA500Machine).GetField("_slowRam", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(machine)!;
var memory = new Reader(machine, slow);
var actions = new[] { "5", "Enter", "6", "Enter", "+" };
var events = new List<object>();
var phase = "startup"; var action = 0; var entered = 0; var lastInput = -100;
uint library = 0, windowObject = 0, target = 0;
uint originalNativeWindow = 0, originalScreen = 0;
bool? nativeWindowUnlinked = null;
int targetX = 0, targetY = 0;
var reason = "MAX_FIELDS"; var success = false;
long? cleanupObservedFrame = null;
State? state = null;
for (var frame = 0; frame < int.Parse(args[4]); frame++)
{
    machine.ExecuteFrame();
    if (machine.UnsupportedActiveFeature is { } unsupported)
    { reason = "UNSUPPORTED: " + unsupported; Capture("unsupported"); break; }
    if (frame % 50 != 0) continue;
    if (library == 0) library = memory.FindLibrary();
    state = library == 0 ? null : memory.ReadState(library);
    if (frame % 10000 == 0)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { frame, phase, action, objects = state?.Objects.Count, depth = state?.Depth }));
        Capture("progress");
    }
    if (phase == "quit-settle")
    {
        if (state is not null && (state.Depth != 0 || state.Objects.Count != 0 || state.Classes != 0))
        { reason = "REGISTRY_ACTIVITY_AFTER_QUIT"; Capture("quit-activity"); break; }
        // A completed Dispose may precede the client's CloseLibrary/CLI return.
        // Keep executing ordinary guest frames; an expunged library is allowed
        // only after the captured empty-registry observation above.
        if (frame - entered >= 5000)
        {
            nativeWindowUnlinked = !memory.ScreenContainsWindow(originalScreen, originalNativeWindow);
            Capture("quit-settled");
            success = nativeWindowUnlinked == true;
            reason = success ? "EMPTY_REGISTRIES_AND_NATIVE_WINDOW_UNLINKED_AFTER_QUIT_AND_SETTLE"
                : "NATIVE_WINDOW_STILL_LINKED_AFTER_QUIT";
            break;
        }
        continue;
    }
    if (state is null) continue;
    var window = state.Objects.FirstOrDefault(o => o.Class == "Window.mui" && o.NativeWindow != 0);
    if (phase == "startup")
    {
        if (window is null || state.Depth != 0) continue;
        windowObject = window.Object;
        originalNativeWindow = window.NativeWindow;
        originalScreen = memory.U32(originalNativeWindow + 46);
        Capture("opened");
        if (!SelectButton()) break;
    }
    else if (phase == "aim" || phase == "close-aim")
    {
        window = state.Objects.FirstOrDefault(o => o.Object == windowObject);
        if (window is null || window.NativeWindow == 0) { reason = "WINDOW_LOST_DURING_AIM"; break; }
        if (frame - entered > 10000) { reason = "POINTER_AIM_TIMEOUT"; Capture("aim-timeout"); break; }
        var x = memory.I16(window.NativeWindow + 14); var y = memory.I16(window.NativeWindow + 12);
        var dx = targetX - x; var dy = targetY - y;
        if (Math.Abs(dx) <= 2 && Math.Abs(dy) <= 2)
        {
            Input(1, 0, 0); Transition(phase == "aim" ? "press" : "close-press");
        }
        else Input(0, Delta(dx), Delta(dy));
    }
    else if (phase == "press")
    {
        var button = state.Objects.FirstOrDefault(o => o.Object == target);
        if (button?.Pressed == 1) { Input(0, 0, 0); Transition("release"); }
        else if (frame - entered > 60000) { reason = "BUTTON_PRESS_TIMEOUT"; Capture("press-timeout"); break; }
    }
    else if (phase == "release")
    {
        var button = state.Objects.FirstOrDefault(o => o.Object == target);
        if (button?.Pressed == 0 && state.Depth == 0 && frame - entered >= 500)
        {
            Capture("after-" + action);
            events.Add(new { frame, operation = actions[action], result = "accepted-mouse-release" });
            action++;
            if (action < actions.Length) { if (!SelectButton()) break; }
            else
            {
                Capture("arithmetic-result");
                targetX = 5; targetY = 5; Transition("close-aim");
            }
        }
        else if (frame - entered > 60000) { reason = "BUTTON_RELEASE_TIMEOUT"; Capture("release-timeout"); break; }
    }
    else if (phase == "close-press" && frame - entered >= 500)
    { Input(0, 0, 0); Transition("cleanup"); }
    else if (phase == "cleanup" && state.Depth == 0 && state.Objects.Count == 0 && state.Classes == 0)
    { cleanupObservedFrame = machine.CompletedFrames; Capture("cleaned"); Transition("quit-settle"); }
}
machine.SubmitInput(new(0, 0, 0, 0, 0, 0));
Capture("final");
var result = new
{
    schemaVersion = 4, protocol = "mui-calc-native-mouse-v4", profile = "a500-ocs-5m-diagnostic-v1",
    buttonPressTimeoutFields = 60000, buttonReleaseTimeoutFields = 60000,
    quitSettleFields = 5000, cleanupObservedFrame,
    romSha256 = Convert.ToHexString(SHA256.HashData(rom)), imageSha256 = mediaHash,
    engineSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(LightweightA500Machine).Assembly.Location))),
    cpuSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(M68kCpuModel).Assembly.Location))),
    machine.CompletedFrames, reason, inputAndCleanup = success ? "PASS" : "INCOMPLETE_OR_FAILED",
    arithmeticVerification = "REQUIRES_VISUAL_REVIEW_OF_ARITHMETIC_RESULT", events,
    phase, action, finalObjects = state?.Objects.Count, finalClasses = state?.Classes, finalDepth = state?.Depth,
    originalNativeWindow, originalScreen, nativeWindowUnlinked,
};
File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(result));
return success ? 0 : 2;

short Delta(int difference) => (short)Math.Clamp(difference == 0 ? 0 : difference / 2 == 0 ? Math.Sign(difference) : difference / 2, -16, 16);
void Input(byte buttons, short dx, short dy)
{
    if (machine.CompletedFrames - lastInput < 50) return;
    machine.SubmitInput(new(0, 0, buttons, dx, dy, 0)); lastInput = (int)machine.CompletedFrames;
}
void Transition(string next)
{
    phase = next; entered = (int)machine.CompletedFrames;
    events.Add(new { frame = machine.CompletedFrames, phase, action, target, targetX, targetY });
    Console.WriteLine(JsonSerializer.Serialize(events[^1]));
}
bool SelectButton()
{
    var matches = state!.Objects.Where(o => o.Class == "Text.mui" && o.InputMode is >= 1 and <= 3 &&
        o.Visible && o.Width >= 6 && o.Height >= 6 && o.ContainingWindow == windowObject &&
        string.Equals(Normalize(o.Text), actions[action], StringComparison.OrdinalIgnoreCase)).ToArray();
    if (matches.Length != 1 || matches[0].Width < 6 || matches[0].Height < 6)
    { reason = "BUTTON_NOT_UNIQUELY_VISIBLE: " + actions[action]; Capture("button-unavailable"); return false; }
    var button = matches[0]; target = button.Object;
    targetX = button.Left + button.Width / 2; targetY = button.Top + button.Height / 2;
    Transition("aim"); return true;
}
string Normalize(string text)
{
    var result = new StringBuilder();
    for (var i = 0; i < text.Length; i++) { if (text[i] == '\x1b') { i++; continue; } result.Append(text[i]); }
    return result.ToString().Trim();
}
void Capture(string label)
{
    var stem = Path.Combine(output, $"frame-{machine.CompletedFrames:D6}-{label}");
    File.WriteAllText(stem + ".json", JsonSerializer.Serialize(new
    {
        machine.CompletedFrames, phase, action, state,
        pc = machine.Cpu.ProgramCounter.ToString("X8"),
        status = machine.Cpu.StatusRegister.ToString("X4"),
        data = machine.Cpu.D.ToArray().Select(v => v.ToString("X8")).ToArray(),
        address = machine.Cpu.A.ToArray().Select(v => v.ToString("X8")).ToArray(),
    }, new JsonSerializerOptions { WriteIndented = true }));
    using (var writer = new BinaryWriter(File.Create(stem + ".bmp")))
    {
        var pixels = machine.Framebuffer.Span;
        writer.Write((ushort)0x4D42); writer.Write(54 + pixels.Length * 4); writer.Write(0); writer.Write(54); writer.Write(40);
        writer.Write(machine.FramebufferWidth); writer.Write(-machine.FramebufferHeight); writer.Write((ushort)1); writer.Write((ushort)32);
        writer.Write(0); writer.Write(pixels.Length * 4); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
        foreach (var pixel in pixels) writer.Write(pixel);
    }
    File.WriteAllBytes(stem + ".fastram", machine.FastRam.ToArray());
    File.WriteAllBytes(stem + ".chipram", machine.ChipRam.ToArray());
    File.WriteAllBytes(stem + ".slowram", slow);
    File.WriteAllText(stem + ".memory.json", JsonSerializer.Serialize(new { fastRamBytes = machine.FastRam.Length, fastRamBase = machine.FastRamBase }));
}

sealed record GuestObject(uint Object, string Class, string Text, uint InputMode, uint Pressed,
    int Left, int Top, int Width, int Height, bool Visible, uint NativeWindow, uint ContainingWindow);
sealed record State(uint Library, uint Root, uint Depth, int Classes, List<GuestObject> Objects);
sealed class Reader(LightweightA500Machine machine, byte[] slow)
{
    bool Mapped(uint a, uint length) => a != 0 && ((ulong)a + length <= (ulong)machine.ChipRam.Length ||
        a >= 0xC00000 && (ulong)a - 0xC00000 + length <= (ulong)slow.Length ||
        machine.FastRamBase is uint b && a >= b && (ulong)a - b + length <= (ulong)machine.FastRam.Length);
    public byte U8(uint a) => a < machine.ChipRam.Length ? machine.ChipRam.Span[(int)a] :
        a >= 0xC00000 && a - 0xC00000 < slow.Length ? slow[a - 0xC00000] :
        machine.FastRamBase is uint b && a >= b && a - b < machine.FastRam.Length ? machine.FastRam.Span[(int)(a - b)] : (byte)0;
    public uint U32(uint a) => (uint)(U8(a) << 24 | U8(a + 1) << 16 | U8(a + 2) << 8 | U8(a + 3));
    public short I16(uint a) => unchecked((short)(U8(a) << 8 | U8(a + 1)));
    public bool ScreenContainsWindow(uint screen, uint window)
    {
        // Unknown or malformed capture state cannot establish successful quit.
        if (window == 0 || !Mapped(screen, 8)) return true;
        var current = U32(screen + 4);
        for (var i = 0; current != 0 && i < 256; i++)
        {
            if (current == window || !Mapped(current, 4)) return true;
            current = U32(current);
        }
        return current != 0;
    }
    string Text(uint a)
    {
        var b = new List<byte>(); for (uint i = 0; a != 0 && i < 512 && U8(a + i) != 0; i++) b.Add(U8(a + i));
        return Encoding.Latin1.GetString(b.ToArray());
    }
    public uint FindLibrary()
    {
        if (machine.FastRamBase is not uint b) return 0;
        var d = machine.FastRam.Span;
        for (var i = 16; i + 32 < d.Length; i += 2)
            if (BinaryPrimitives.ReadUInt32BigEndian(d.Slice(i, 4)) == 0x02F40030 &&
                BinaryPrimitives.ReadUInt32BigEndian(d.Slice(i + 4, 4)) == 0x00040001 && Text(U32(b + (uint)i - 6)) == "muimaster.library")
                return b + (uint)i - 16;
        return 0;
    }
    uint Attribute(uint record, uint key, uint fallback = 0)
    {
        var a = U32(record + 12);
        for (var i = 0; a != 0 && i < 4096; i++, a = U32(a)) if (U32(a + 4) == key) return U32(a + 8);
        return fallback;
    }
    uint Geometry(uint record)
    {
        var a = U32(record + 32);
        for (var i = 0; a != 0 && i < 1024; i++, a = U32(a))
            if (U32(a + 4) == 0x7F070035 && U32(a + 12) == 28 && U32(U32(a + 8)) == 0x4D414745) return U32(a + 8);
        return 0;
    }
    public State? ReadState(uint library)
    {
        if (Text(U32(library + 10)) != "muimaster.library" ||
            U32(library + 16) != 0x02F40030 || U32(library + 20) != 0x00040001) return null;
        var root = U32(library + 42); var registry = U32(root);
        if (!Mapped(root, 48) || !Mapped(registry, 32)) return null;
        var objects = new List<GuestObject>(); var rec = U32(registry + 12);
        for (var i = 0; rec != 0 && i < 1024; i++, rec = U32(rec))
        {
            if (!Mapped(rec, 64)) return null;
            var geometry = Geometry(rec); var visible = Attribute(rec, 0x80429BA8, 1) != 0;
            uint containingWindow = 0;
            for (uint parent = U32(rec + 28), n = 0; parent != 0 && n < 64; parent = U32(parent + 28), n++)
            {
                visible &= Attribute(parent, 0x80429BA8, 1) != 0;
                if (Text(U32(U32(parent + 8) + 4)) == "Window.mui") containingWindow = U32(parent + 4);
            }
            objects.Add(new(U32(rec + 4), Text(U32(U32(rec + 8) + 4)), Text(Attribute(rec, 0x8042F8DC)),
                Attribute(rec, 0x8042FB04), Attribute(rec, 0x80423535),
                geometry == 0 ? 0 : unchecked((int)U32(geometry + 4)), geometry == 0 ? 0 : unchecked((int)U32(geometry + 8)),
                geometry == 0 ? 0 : unchecked((int)U32(geometry + 12)), geometry == 0 ? 0 : unchecked((int)U32(geometry + 16)),
                visible, Attribute(rec, 0x7FFE0011), containingWindow));
        }
        var classes = 0;
        for (var c = U32(registry + 8); c != 0 && classes < 64; c = U32(c)) classes++;
        return new(library, root, U32(root + 32) + U32(root + 36), classes, objects);
    }
}
