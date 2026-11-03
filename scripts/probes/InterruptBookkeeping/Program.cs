using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

// Artificial CPU-interface input isolation, not an emulated machine workload.
// Reflection/expressions are prepared before timing and never enter production.
if (args.Length is < 2 or > 3)
    throw new ArgumentException("Usage: InterruptBookkeeping <frozen-engine-directory> <output.json> [iterations]");
var directory = Path.GetFullPath(args[0]);
var iterations = args.Length == 3 ? long.Parse(args[2]) : 20_000_000;
if (iterations < 10_000) throw new ArgumentException("At least 10000 iterations required.");
AssemblyLoadContext.Default.Resolving += (_, name) =>
    AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(directory, name.Name + ".dll"));
var enginePath = Path.Combine(directory, "CopperMod.Amiga.Lightweight.dll");
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(enginePath);
var type = assembly.GetType("CopperMod.Amiga.Lightweight.LightweightA500Machine", true)!;
using var machine = (IDisposable)Activator.CreateInstance(type, new object?[] { null })!;
var cpu = FindField(type, "_cpu").GetValue(machine)!;
var state = cpu.GetType().GetProperty("State")!.GetValue(cpu)!;
var status = state.GetType().GetProperty("StatusRegister")!;
var dispatch = type.GetMethod("DispatchPendingCpuInterrupt", BindingFlags.Instance | BindingFlags.NonPublic)!
    .CreateDelegate<Func<bool>>(machine);
var sampleField = FindField(cpu.GetType(), "_lastInterruptSample");
var sampleValue = Expression.Parameter(typeof(long));
var setSample = Expression.Lambda<Action<long>>(
    Expression.Assign(Expression.Field(Expression.Constant(cpu), sampleField),
        Expression.New(sampleField.FieldType.GetConstructor(new[] { typeof(long), typeof(ulong) })!,
            sampleValue, Expression.Constant(0UL))), sampleValue).Compile();
var reset = type.GetMethod("Reset", Type.EmptyTypes)!;
var observations = new List<object>();
foreach (var scenario in new[] { "inactive", "masked" })
{
    reset.Invoke(machine, null);
    status.SetValue(state, scenario == "masked" ? (ushort)0x2700 : (ushort)0x2000);
    FindField(type, "_interruptPinLevel").SetValue(machine, scenario == "masked" ? 3 : 0);
    FindField(type, "_interruptPinChangeCycle").SetValue(machine, 0L);
    // Each poll gets a distinct late instruction sample, unlike repeatedly
    // polling one cached sample. Both scenarios must reject exception entry.
    if (Poll(dispatch, setSample, 500_000, 0) != 0) throw new Exception("Warmup accepted an interrupt.");
    _ = ReadThread();
    _ = Stopwatch.GetElapsedTime(Stopwatch.GetTimestamp());
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    var allocationBefore = GC.GetAllocatedBytesForCurrentThread();
    var threadBefore = ReadThread();
    var start = Stopwatch.GetTimestamp();
    var accepted = Poll(dispatch, setSample, iterations, 4_000_000);
    var elapsed = Stopwatch.GetElapsedTime(start).TotalSeconds;
    var threadAfter = ReadThread();
    var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
    if (accepted != 0 || allocated != 0) throw new Exception($"Unexpected accepted={accepted}, allocated={allocated}.");
    var pipeline = FindField(cpu.GetType(), "_iplPipelineState").GetValue(cpu)!;
    observations.Add(new
    {
        scenario, iterations, accepted, allocated,
        elapsedSeconds = elapsed,
        threadCpuNanosecondsPerPoll = (threadAfter.Cpu100ns - threadBefore.Cpu100ns) * 100.0 / iterations,
        threadCyclesPerPoll = (threadAfter.Cycles - threadBefore.Cycles) / (double)iterations,
        wallNanosecondsPerPoll = elapsed * 1e9 / iterations,
        finalSample = JsonSerializer.SerializeToElement(sampleField.GetValue(cpu), sampleField.FieldType),
        pipeline = JsonSerializer.SerializeToElement(pipeline, pipeline.GetType())
    });
}
var report = new
{
    protocol = "interrupt-interface-cost-v1", diagnosticOnly = true,
    scope = "Artificial inactive/masked interface polls with distinct sample cycles; no CPU/device/DMA workload or native-FPS claim",
    commonOverhead = "One compiled sample-field setter delegate, one dispatch delegate and loop arithmetic per poll in every variant",
    engineSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(enginePath))),
    cpuSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, "Copper68k.dll")))),
    observations
};
File.WriteAllText(args[1], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

static FieldInfo FindField(Type type, string name)
{
    for (Type? current = type; current != null; current = current.BaseType)
        if (current.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) is { } field) return field;
    throw new MissingFieldException(type.FullName, name);
}

[MethodImpl(MethodImplOptions.NoInlining)]
static long Poll(Func<bool> dispatch, Action<long> setSample, long iterations, long start)
{
    long accepted = 0;
    for (long i = 1; i <= iterations; i++)
    {
        setSample(start + i * 8);
        if (dispatch()) accepted++;
    }
    return accepted;
}

static (ulong Cpu100ns, ulong Cycles) ReadThread()
{
    var thread = Native.GetCurrentThread();
    if (!Native.GetThreadTimes(thread, out _, out _, out var kernel, out var user) ||
        !Native.QueryThreadCycleTime(thread, out var cycles)) throw new Exception("Native thread accounting unavailable.");
    return (kernel + user, cycles);
}

internal static class Native
{
    [DllImport("kernel32.dll")] internal static extern IntPtr GetCurrentThread();
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetThreadTimes(IntPtr thread, out ulong created, out ulong exited, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool QueryThreadCycleTime(IntPtr thread, out ulong cycles);
}
