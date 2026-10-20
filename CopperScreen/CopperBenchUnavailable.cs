namespace CopperScreen;

// Keep the host overlay explanatory while the CopperStart browser/launcher is absent.
// The retired browser implementation is preserved in Git history and the legacy archive.
internal sealed class CopperBenchViewModel
{
    private const string Unavailable = "The Legacy CopperBench browser is not included. Boot floppy applications with a native Kickstart ROM or the qualified standalone CopperStart ROM on Lightweight.";
    public bool IsOverlayVisible { get; private set; }
    public bool IsToolbarVisible { get; private set; } = true;
    public string DisplayPath => "CopperBench unavailable";
    public string StatusMessage { get; private set; } = Unavailable;
    public string SelectedDetails => StatusMessage;
    public IReadOnlyList<CopperBenchEntry> Entries => Array.Empty<CopperBenchEntry>();
    public int SelectedIndex => -1;
    public void HideOverlay() => IsOverlayVisible = false;
    public void ToggleToolbar() => IsToolbarVisible = !IsToolbarVisible;
    public Task RefreshAsync(string? diskPath) { StatusMessage = Unavailable; return Task.CompletedTask; }
    public Task ToggleOverlayAsync(string? diskPath) { IsOverlayVisible = !IsOverlayVisible; return RefreshAsync(diskPath); }
    public Task ShowOverlayAsync(string? diskPath) { IsOverlayVisible = true; return RefreshAsync(diskPath); }
    public Task GoUpAsync(string? diskPath) => RefreshAsync(diskPath);
    public void SelectIndex(int index) { }
    public void ResetPath() { }
    public void SetStatusMessage(string message) => StatusMessage = message;
    public Task<bool> ActivateSelectedAsync(string? diskPath, Func<string, Task<CopperScreenCommandResult>> launchAsync)
    { StatusMessage = Unavailable; return Task.FromResult(false); }
}

internal sealed record CopperBenchEntry(string Name, string Path, string Details);
