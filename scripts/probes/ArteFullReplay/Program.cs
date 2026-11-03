using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using CopperScreen;
using CopperScreen.Tests;

// External, untimed original-media investigation. A completed field budget is
// not proof of a full demo: inspect the captured ending and subsequent restart.
if (args.Length is < 2 or > 4)
{
    Console.Error.WriteLine("Usage: ArteFullReplay <adf-or-zip-entry> <output-directory> [model:0|1|2] [fields:40000]");
    return 2;
}
var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
var model = args.Length > 2 ? int.Parse(args[2]) : 2;
var limit = args.Length > 3 ? int.Parse(args[3]) : 40000;
if (model is < 0 or > 2 || limit < 1) throw new ArgumentOutOfRangeException(nameof(args));
if (File.Exists(Path.Combine(output, "frames.jsonl"))) throw new IOException("Choose a new output directory to preserve the preceding run.");
var draft = CopperScreenLaunchWorkflow.SelectStartChoice(new(), 6 + model);
draft.DriveDiskPaths[0] = args[0];
var disk = CopperScreenDiskImageArchive.LoadDiskImage(args[0]);
if (Convert.ToHexString(SHA256.HashData(disk.Data)) != "EB7521FC1DE886C69706AB0E21898A89199FB12256DB40743F3ED190957E896A")
    throw new InvalidDataException("This probe's recorded scene evidence requires the qualified Arte ADF. See docs/engine/MINIMAL_DISK_BOOT.md.");
var rom = new CopperScreenMinimalBoot(disk, model == 0, draft.ChipRamKb * 1024, model == 2);
using var session = new CopperScreenLightweightSession(draft.ToStartupOptions(AppContext.BaseDirectory));
using var log = new StreamWriter(Path.Combine(output, "frames.jsonl")) { AutoFlush = true };
using var pcm = File.Create(Path.Combine(output, "audio.wav")); pcm.Write(new byte[44]);
var frames = 0; var audible = 0; long sampleCount = 0; var exceptions = new List<object>();
var owner = Environment.CurrentManagedThreadId;
string? failure = null;
void Capture()
{
    var path = Path.Combine(output, $"frame-{frames:D5}.png");
    NativeCapture.WritePng(path, session.Framebuffer, session.Width, session.Height);
    var audio = session.Machine.AudioSamples.Span;
    short peak = 0; foreach (var s in audio) peak = (short)Math.Max(peak, Math.Min(32767, Math.Abs((int)s)));
    log.WriteLine(JsonSerializer.Serialize(new
    {
        frames,
        cycle = session.Machine.Cycle,
        pc = session.Machine.Cpu.ProgramCounter.ToString("X8"),
        beam = session.Machine.BeamSyncRunning,
        drive = session.Machine.DriveState,
        colors = session.Framebuffer.Distinct().Count(),
        hash = Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(session.Framebuffer.AsSpan()))),
        audible,
        peak,
        sampleCount,
        exceptionCount = exceptions.Count,
        fault = session.FaultMessage,
        path
    }));
    if (frames % 1000 == 0) Console.WriteLine($"frame={frames} PC={session.Machine.Cpu.ProgramCounter:X8} audible={audible} exceptions={exceptions.Count}");
}
void OnException(object? _, FirstChanceExceptionEventArgs e)
{
    if (Environment.CurrentManagedThreadId != owner || e.Exception.GetType().Namespace != "Copper68k") return;
    var m = session.Machine;
    exceptions.Add(new { frames, cycle = m.Cycle, pc = m.Cpu.ProgramCounter.ToString("X8"), type = e.Exception.GetType().FullName, message = e.Exception.Message, d = m.Cpu.D.ToArray(), a = m.Cpu.A.ToArray() });
    if (exceptions.Count <= 4) File.WriteAllBytes(Path.Combine(output, $"exception-{frames:D5}-{exceptions.Count}.ram"), m.ChipRam.ToArray());
    File.WriteAllText(Path.Combine(output, "exceptions.json"), JsonSerializer.Serialize(exceptions, new JsonSerializerOptions { WriteIndented = true }));
}
AppDomain.CurrentDomain.FirstChanceException += OnException;
try
{
    for (; frames < limit;)
    {
        session.RenderNextFrame(session.Framebuffer); frames++;
        var audio = session.Machine.AudioSamples.Span; pcm.Write(MemoryMarshal.AsBytes(audio)); sampleCount += audio.Length;
        foreach (var s in audio) if (s != 0) { audible++; break; }
        if (frames % 50 == 0 || frames == limit) Capture();
        if (session.Machine.UnsupportedActiveFeature != null || session.Machine.Cpu.Halted) throw new Exception(session.Machine.UnsupportedActiveFeature ?? "CPU halted");
    }
}
catch (Exception e) { failure = e.ToString(); Capture(); }
finally { AppDomain.CurrentDomain.FirstChanceException -= OnException; }
File.WriteAllBytes(Path.Combine(output, "final.ram"), session.Machine.ChipRam.ToArray());
var bytes = (int)(pcm.Length - 44); pcm.Position = 0;
using (var bw = new BinaryWriter(pcm, System.Text.Encoding.UTF8, leaveOpen: true))
{
    bw.Write("RIFF"u8); bw.Write(bytes + 36); bw.Write("WAVEfmt "u8); bw.Write(16); bw.Write((short)1); bw.Write((short)2); bw.Write(48000); bw.Write(192000); bw.Write((short)4); bw.Write((short)16); bw.Write("data"u8); bw.Write(bytes);
}
File.WriteAllText(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(new
{
    model,
    cpu = session.Machine.CpuModel.ToString(),
    agnus = session.Machine.AgnusModel.ToString(),
    denise = session.Machine.DeniseModel.ToString(),
    chipRamBytes = session.Machine.ChipRam.Length,
    frames,
    limit,
    audible,
    sampleCount,
    failure,
    exceptions,
    mediaSha256 = Convert.ToHexString(SHA256.HashData(disk.Data)),
    firmwareSha256 = Convert.ToHexString(SHA256.HashData(rom.Rom)),
    engineSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(CopperMod.Amiga.Lightweight.LightweightA500Machine).Assembly.Location))),
    scope = "No-input full-sequence investigation; frame PNG every 50 fields, continuous guest PCM; no host performance measurement"
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"done frames={frames} failure={failure} exceptions={exceptions.Count}");
return failure == null && exceptions.Count == 0 ? 0 : 1;
