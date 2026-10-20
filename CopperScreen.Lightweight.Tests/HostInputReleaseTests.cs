using CopperScreen;
using CopperMod.Amiga.Lightweight;
using Avalonia.Input;

namespace CopperScreen.Tests;

public sealed class HostInputReleaseTests
{
    [Fact]
    public void FocusReleaseReleasesKeysMouseAndBothPortsIncludingPausedSession()
    {
        using var session = new ControlledSession(); session.TogglePaused();
        CopperScreenRuntime.ReleaseInteractiveInput(session, new[]{AmigaRawKey.A, AmigaRawKey.LeftShift});
        Assert.Equal(new[]{"up:A","up:LeftShift","mouse:False:False","joy:0:False:False:False:False:False:False","joy:1:False:False:False:False:False:False"},session.Calls);
        Assert.True(session.IsPaused);
        session.Calls.Clear(); CopperScreenRuntime.ReleaseInteractiveInput(session, Array.Empty<AmigaRawKey>());
        Assert.Equal(3,session.Calls.Count);
    }
    [Theory]
    [InlineData(Key.Y, PhysicalKey.Z, (int)AmigaRawKey.Z)]
    [InlineData(Key.Z, PhysicalKey.Y, (int)AmigaRawKey.Y)]
    [InlineData(Key.A, PhysicalKey.Q, (int)AmigaRawKey.Q)]
    public void PhysicalPositionsSurviveHostLayoutChanges(Key logical, PhysicalKey physical, int expected)
    {
        Assert.True(AmigaHostKeyMapper.TryMap(logical,physical,NumpadInputMode.AmigaKeys,out var key));
        Assert.Equal(expected,(int)key);
    }
    private sealed class ControlledSession : ICopperScreenSession
    {
        public List<string> Calls = new();
        public int Fields;
        public int Width => 1;
        public int Height => 1;
        public int[] Framebuffer { get; } = new int[1];
        public double VideoVBlankHz => 1d / 60;
        public CopperScreenPresentationGeometry PresentationGeometry => default;
        public CopperScreenEmulatorFrameTiming LastFrameTiming => default;
        public bool IsInterlaced => false;
        public int CompletedInterlaceField => 0;
        public string ProfileName => "Controlled audio pacing";
        public string DiskName => "None";
        public string? DiskPath => null;
        public string BaseDirectory => AppContext.BaseDirectory;
        public FloppyDriveAudioOptions FloppyDriveAudioOptions => default;
        public CopperScreenCpuState CpuState => default;
        public CopperScreenDebugSnapshot? DebugSnapshot => null;
        public string StatusText => "";
        public bool IsPaused { get; private set; }
        public bool IsWorkbenchHandoffPending => false;
        public bool IsDiskSwapPending => false;
        public bool IsPrimaryFirePressed => false;
        public bool AudioFilterEnabled => false;
        public int AudioFramesPerAppFrame(int sampleRate) => 2;
        public void RenderNextFrame(int[] destination) { Fields++; destination[0] = Fields; }
        public int RenderAudio(Span<float> destination, int sampleRate, int channels) { destination.Fill(Fields); return 2; }
        public void CaptureDriveStates(Span<CopperScreenDriveState> destination) => destination.Clear();
        public void SetStatusText(string message) { }
        public void CaptureFatalException(Exception exception) => throw exception;
        public void Reset() { }
        public bool TogglePaused() => IsPaused = !IsPaused;
        public void PulsePrimaryFire(int frames) { }
        public void KeyDown(AmigaRawKey key) { }
        public void KeyUp(AmigaRawKey key) { Calls.Add($"up:{key}"); }
        public void MoveMousePort(int x, int y) { }
        public void SetMousePortPosition(int x, int y) { }
        public void SetMousePresentationPosition(int x, int y) { }
        public void SetMouseButtons(bool primary, bool secondary) { Calls.Add($"mouse:{primary}:{secondary}"); }
        public void SetJoystickPort(bool up, bool down, bool left, bool right, bool primary, bool secondary) { }
        public void SetJoystickPort(int port, bool up, bool down, bool left, bool right, bool primary, bool secondary) { Calls.Add($"joy:{port}:{up}:{down}:{left}:{right}:{primary}:{secondary}"); }
        public void SetInputOptions(CopperScreenInputOptions options) { }
        public void SetPresentationOptions(CopperScreenPresentationOptions options) { }
        public bool InsertLoadedDisk(string path, CopperScreenAdfImage disk, bool markChanged) => false;
        public bool InsertLoadedDisk(int drive, string path, CopperScreenAdfImage disk, bool markChanged) => false;
        public bool SetDriveWriteProtected(int drive, bool writeProtected) => false;
        public bool ConsumeCopperBenchRequest() => false;
        public bool LaunchCopperBenchPath(string path, out string message) { message = ""; return false; }
        public void QueueHostClipboardText(string text) { }
        public void QueueHostClipboardImage(ClipboardImage image) { }
        public bool TryTakeHostClipboardText(out string text) { text = ""; return false; }
        public bool TryTakeHostClipboardImage(out ClipboardImage? image) { image = null; return false; }
        public void Dispose() { }
    }
}
