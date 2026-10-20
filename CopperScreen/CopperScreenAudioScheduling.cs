using System.Runtime.InteropServices;

namespace CopperScreen;

// The PCM producer must meet the same host deadlines as its native consumer.
// Registration belongs to the runtime thread and is reverted on that thread.
internal sealed class CopperScreenAudioScheduling : IDisposable
{
    private nint _handle;
    private CopperScreenAudioScheduling(nint handle) => _handle = handle;
    internal bool IsRegistered => _handle != 0;
#if COPPERSCREEN_INSTALLED_TESTS
    internal bool DiagnosticSetEnabled(bool enabled)
    {
        if (!enabled) Dispose();
        else if (_handle == 0 && OperatingSystem.IsWindows())
        {
            uint index = 0;
            _handle = AvSetMmThreadCharacteristics("Games", ref index);
        }
        return IsRegistered == enabled;
    }
#endif

    internal static CopperScreenAudioScheduling? TryEnter()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            uint index = 0;
            var handle = AvSetMmThreadCharacteristics("Games", ref index);
            return handle == 0 ? null : new(handle);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null; // Scheduling is best effort; normal priority remains usable.
        }
    }

    public void Dispose()
    {
        if (_handle == 0) return;
        AvRevertMmThreadCharacteristics(_handle);
        _handle = 0;
    }

    [DllImport("avrt.dll", EntryPoint = "AvSetMmThreadCharacteristicsW", CharSet = CharSet.Unicode)]
    private static extern nint AvSetMmThreadCharacteristics(string taskName, ref uint taskIndex);
    [DllImport("avrt.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AvRevertMmThreadCharacteristics(nint handle);
}
