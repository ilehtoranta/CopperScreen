#if COPPERSCREEN_INSTALLED_TESTS
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;

namespace CopperScreen;

// Capture-time validation only; excluded from the normal release assembly.
internal static class CopperScreenInstalledCapture
{
    internal sealed record Result(string Protocol, string Path, string Sha256, long PresentedFrame,
        int PresenterTextPixels, int CaptureTextPixels, bool ProofTextRequired, bool Passed);

    internal static Result Save(Visual visual, FramebufferPresenter presenter, Size clientSize,
        double scaling, string path, bool requireProofText, long presentedFrame)
    {
        var size = new PixelSize((int)Math.Ceiling(clientSize.Width * scaling), (int)Math.Ceiling(clientSize.Height * scaling));
        using var bitmap = new RenderTargetBitmap(size, new Vector(96 * scaling, 96 * scaling));
        bitmap.Render(visual);
        bitmap.Save(path);
        // Inspect the saved capture before touching the mutable presenter bitmap.
        var capture = new int[checked(size.Width * size.Height)];
        var bytes = checked(capture.Length * sizeof(int));
        var pointer = Marshal.AllocHGlobal(bytes);
        try
        {
            bitmap.CopyPixels(new PixelRect(size), pointer, bytes, size.Width * sizeof(int));
            Marshal.Copy(pointer, capture, 0, capture.Length);
        }
        finally { Marshal.FreeHGlobal(pointer); }
        var origin = presenter.TranslatePoint(default, visual) ?? throw new InvalidOperationException("Detached presenter");
        var left = Math.Clamp((int)Math.Floor(origin.X * scaling), 0, size.Width);
        var top = Math.Clamp((int)Math.Floor(origin.Y * scaling), 0, size.Height);
        var right = Math.Clamp((int)Math.Ceiling((origin.X + presenter.Bounds.Width) * scaling), left, size.Width);
        var bottom = Math.Clamp((int)Math.Ceiling((origin.Y + presenter.Bounds.Height) * scaling), top, size.Height);
        var captureText = 0;
        for (var y = top; y < bottom; y++)
            captureText += CountNeutralTextPixels(capture.AsSpan(y * size.Width + left, right - left));
        var presenterText = CountNeutralTextPixels(presenter.DiagnosticCopyFrontPixels());
        var result = new Result("copperscreen-installed-capture-v1", path, CopperScreenInstalledSoak.Hash(path), presentedFrame,
            presenterText, captureText, requireProofText, !requireProofText || HasProofText(presenterText, captureText));
        CopperScreenInstalledSoak.Write(path + ".json", result);
        if (!result.Passed) throw new InvalidOperationException($"CopperOS proof text missing: presenter={presenterText}, capture={captureText}; retained {path}");
        return result;
    }

    internal static bool HasProofText(int presenterTextPixels, int captureTextPixels) => presenterTextPixels >= 100 && captureTextPixels >= 100;

    internal static int CountNeutralTextPixels(ReadOnlySpan<int> pixels)
    {
        var count = 0;
        foreach (var p in pixels)
        {
            var r = (p >> 16) & 255; var g = (p >> 8) & 255; var b = p & 255;
            if (((uint)p >> 24) >= 128 && Math.Min(r, Math.Min(g,b)) >= 128 && Math.Max(r, Math.Max(g,b)) - Math.Min(r, Math.Min(g,b)) <= 8) count++;
        }
        return count;
    }
}
#endif
