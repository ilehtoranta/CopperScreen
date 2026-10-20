#if COPPERSCREEN_INSTALLED_TESTS
using System.Diagnostics;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    // Bounded diagnostic experiment only. No production per-field observers.
    private async Task RunInstalledCaptureProbeAsync(CopperScreenRuntime runtime, string output, string stem)
    {
        var directory = Path.Combine(output, stem + "-capture-probe");
        Directory.CreateDirectory(directory);
        for (var i = 0; i < 32; i++)
        {
            var saved = await runtime.SavePersistentStorageAsync();
            CopperScreenInstalledSoak.Require(saved.Success, saved.Message);
            int[] native = [], session = []; int width = 0, height = 0;
            string console = ""; long ownerQpc = 0;
            var observed = await runtime.DiagnosticObserveAsync(e =>
            {
                var guest = (CopperScreenLightweightSession)e;
                native = guest.Machine.Framebuffer.ToArray();
                width = guest.Machine.FramebufferWidth; height = guest.Machine.FramebufferHeight;
                session = guest.Framebuffer.ToArray();
                console = CopperScreenInstalledSoak.Console(guest); ownerQpc = Stopwatch.GetTimestamp();
            });
            CopperScreenInstalledSoak.Require(observed.Success, observed.Message);
            var prefix = Path.Combine(directory, i.ToString("D3"));
            var offset = _presenter.TranslatePoint(default, this) ?? throw new InvalidOperationException("Presenter is detached");
            var presentedFrame = _presentedFrames; var captureQpc = Stopwatch.GetTimestamp();
            // Capture BEFORE reading the WriteableBitmap. Locking it for diagnostic
            // readback may mark it dirty; that must not repair the observed capture.
            using (var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(ClientSize.Width * RenderScaling), (int)Math.Ceiling(ClientSize.Height * RenderScaling)), new Vector(96 * RenderScaling, 96 * RenderScaling)))
            { bitmap.Render(this); bitmap.Save(prefix + "-window.png"); }
            var front = _presenter.DiagnosticCopyFrontPixels();
            SaveProbePixels(native, width, height, prefix + "-native.png");
            SaveProbePixels(session, width, height * 2, prefix + "-session.png");
            SaveProbePixels(front, _presenter.PixelWidth, _presenter.PixelHeight, prefix + "-presenter.png");
            static object Pixels(int[] pixels) => new { white = pixels.Count(p => (p & 0xffffff) == 0xffffff), sha256 = Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(pixels.AsSpan()))).ToLowerInvariant() };
            CopperScreenInstalledSoak.Write(prefix + ".json", new { protocol = "copperscreen-capture-layers-v1", index = i,
                ownerQpc, captureQpc, presentedFrame, state = observed.State, console,
                native = Pixels(native), session = Pixels(session), presenter = Pixels(front),
                presenterBounds = new { x = offset.X * RenderScaling, y = offset.Y * RenderScaling, width = _presenter.Bounds.Width * RenderScaling, height = _presenter.Bounds.Height * RenderScaling },
                captureBeforePresenterReadback = true });
            await Task.Delay(31 + i % 7);
        }
    }

    private static void SaveProbePixels(int[] pixels, int width, int height, string path)
    {
        using var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96,96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using (var fb = bitmap.Lock()) FramebufferPresenter.CopyBgraRows(pixels, width, height, fb.Address, fb.RowBytes);
        bitmap.Save(path);
    }
}
#endif
