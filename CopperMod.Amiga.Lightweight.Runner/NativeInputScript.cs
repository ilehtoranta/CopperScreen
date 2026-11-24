using System.Text.Json;
using CopperMod.Amiga.Lightweight;

// Parsed once outside execution timing. Frame zero precedes the first ExecuteFrame.
internal sealed class NativeInputScript
{
    internal sealed record Entry(int Frame, byte MouseButtons = 0, short MouseDeltaX = 0,
        short MouseDeltaY = 0, ushort KeyCode = 0, byte JoystickPort0 = 0, byte JoystickPort1 = 0,
        string? AdfPath = null, bool EjectAdf = false, int Drive = 0, string? DiskPath = null,
        byte? Cd32ButtonsPort0 = null, byte? Cd32ButtonsPort1 = null, string? CdPath = null, bool EjectCd = false);
    private readonly Entry[] _entries;
    private readonly LightweightFloppyImage?[] _media;
    private readonly string?[] _cdPaths;
    private int _next;

    internal NativeInputScript(string path, Func<string, byte[]> readAdf, int driveCount = 1, bool cd32 = false)
    {
        _entries = JsonSerializer.Deserialize<Entry[]>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
            throw new ArgumentException("Input script must be an array.");
        _media = new LightweightFloppyImage?[_entries.Length];
        _cdPaths = cd32 ? new string?[_entries.Length] : [];
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        for (var i = 0; i < _entries.Length; i++)
        {
            if (_entries[i] is null || _entries[i].Frame < 0 ||
                (i > 0 && _entries[i].Frame < _entries[i - 1].Frame))
                throw new ArgumentException("Input script frames must be nonnegative and ordered.");
            var entry = _entries[i];
            if ((entry.Drive != 0 || entry.EjectAdf || entry.DiskPath is not null || entry.AdfPath is not null) &&
                (uint)entry.Drive >= (uint)driveCount)
                throw new ArgumentException("Input script refers to a disconnected floppy drive.");
            if (entry.Cd32ButtonsPort0.HasValue || entry.Cd32ButtonsPort1.HasValue)
            {
                if (!cd32) throw new ArgumentException("CD32 button input requires --cd32.");
                if (((entry.Cd32ButtonsPort0 ?? 0) | (entry.Cd32ButtonsPort1 ?? 0)) > 127)
                    throw new ArgumentException("CD32 button masks must be between 0 and 127.");
            }
            if (entry.CdPath is not null || entry.EjectCd)
            {
                if (!cd32) throw new ArgumentException("CD media changes require --cd32.");
                if (entry.CdPath is not null)
                {
                    if (entry.EjectCd || string.IsNullOrWhiteSpace(entry.CdPath))
                        throw new ArgumentException("Use separate, nonempty CD mount and eject entries.");
                    _cdPaths[i] = ResolveMediaPath(entry.CdPath, directory);
                }
            }
            if (entry.AdfPath is not null && entry.DiskPath is not null)
                throw new ArgumentException("Specify diskPath or the legacy adfPath, not both.");
            if ((entry.DiskPath ?? entry.AdfPath) is { } mediaPath)
            {
                if (entry.EjectAdf || string.IsNullOrWhiteSpace(mediaPath))
                    throw new ArgumentException("Use separate, nonempty mount and eject entries.");
                // File/ZIP reading is outside execution. Mount-time track encoding
                // is still excluded from acceptance timing by the runner's guard.
                _media[i] = LightweightFloppyImage.Prepare(readAdf(ResolveMediaPath(mediaPath, directory)), mediaPath);
            }
        }
    }

    private static string ResolveMediaPath(string path, string directory)
    {
        // Normalize the host path without rewriting the archive's #/ delimiter.
        var selector = path.IndexOf("#/", StringComparison.Ordinal);
        return selector < 0 ? Path.GetFullPath(path, directory) :
            Path.GetFullPath(path[..selector], directory) + path[selector..];
    }

    internal bool ChangesMediaBetween(int firstFrame, int endFrame)
        => Array.Exists(_entries, e => e.Frame >= firstFrame && e.Frame < endFrame &&
            (e.EjectAdf || e.AdfPath is not null || e.DiskPath is not null || e.EjectCd || e.CdPath is not null));

    internal void Apply(LightweightA500Machine m, int frame)
    {
        while (_next < _entries.Length && _entries[_next].Frame == frame)
        {
            var e = _entries[_next];
            if (e.EjectAdf) m.EjectAdf(e.Drive);
            if (_media[_next] is { } media) m.MountDisk(e.Drive, media);
#if LIGHTWEIGHT_CD32
            if (e.EjectCd) m.EjectCd32Disc();
            if (e.CdPath is not null) m.MountCd32Image(_cdPaths[_next]!);
#endif
            _next++;
            m.SubmitInput(new(e.JoystickPort0, e.JoystickPort1, e.MouseButtons,
                e.MouseDeltaX, e.MouseDeltaY, e.KeyCode));
#if LIGHTWEIGHT_CD32
            if (e.Cd32ButtonsPort0 is { } buttons0) m.SetCd32PadButtons(0, (LightweightCd32Buttons)buttons0);
            if (e.Cd32ButtonsPort1 is { } buttons1) m.SetCd32PadButtons(1, (LightweightCd32Buttons)buttons1);
#endif
        }
    }

    internal static void Mount(LightweightA500Machine machine, int drive, byte[] image, string path)
    {
        machine.MountDisk(drive, image, path);
    }
}
