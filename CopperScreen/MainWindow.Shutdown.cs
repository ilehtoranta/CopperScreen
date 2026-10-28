using Avalonia.Threading;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private async Task CompleteShutdownAsync()
    {
        // The storage decision has completed on the runtime owner. Detach every
        // UI producer before transferring disposal to a worker: late native focus
        // and queued input notifications must not wake a disposed runtime.
        ReleaseInteractiveInput();
        var runtime = _runtime;
        _runtime = null;
        _isClosed = true;
        _frameNotification.Dispose();
        DisableCrtPhosphorPresentation();
        if (runtime != null)
        {
            runtime.FramePublished -= QueueFramePresentation;
            runtime.HostClipboardTextChanged -= PublishGuestClipboardText;
            runtime.HostClipboardImageChanged -= PublishGuestClipboardImage;
        }
        _gamepadHost.ControllersChanged -= OnGamepadControllersChanged;
        foreach (var controller in _gamepadControllersById.Values)
        {
            controller.ElementChanged -= OnGamepadElementChanged;
            controller.ProfileChanged -= OnGamepadProfileChanged;
        }
        _root.IsEnabled = false;
        _settingsWindow.IsEnabled = false;

        // Joining the emulator and native audio/HID callbacks can block. Keep
        // Avalonia's dispatcher and HWND alive until their owners have stopped.
        await Task.Run(() =>
        {
            try { runtime?.Dispose(); }
            catch (Exception ex) { CopperScreenCrashLog.WriteException("Runtime shutdown", ex, ex); }
            try { _gamepadHost.Dispose(); }
            catch (Exception ex) { CopperScreenCrashLog.WriteException("Gamepad shutdown", ex, ex); }
        });
        _allowDiscardOnClose = true;
        // Even an already-completed await must not recursively destroy the
        // native window from inside its original Closing notification.
        Dispatcher.UIThread.Post(Close);
    }
}
