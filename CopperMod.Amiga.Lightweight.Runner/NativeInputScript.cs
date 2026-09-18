using System.Text.Json;
using CopperMod.Amiga.Lightweight;

// Parsed once outside execution timing. Frame zero precedes the first ExecuteFrame.
internal sealed class NativeInputScript
{
    internal sealed record Entry(int Frame, byte MouseButtons = 0, short MouseDeltaX = 0,
        short MouseDeltaY = 0, ushort KeyCode = 0, byte JoystickPort0 = 0, byte JoystickPort1 = 0,
        string? AdfPath = null, bool EjectAdf = false, int Drive = 0, string? DiskPath = null);
    private readonly Entry[] _entries;
    private readonly byte[]?[] _media;
    private readonly LightweightIpfImage?[] _ipf;
    private int _next;

    internal NativeInputScript(string path, Func<string, byte[]> readAdf, int driveCount = 1)
    {
        _entries = JsonSerializer.Deserialize<Entry[]>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
            throw new ArgumentException("Input script must be an array.");
        _media = new byte[]?[_entries.Length];
        _ipf = new LightweightIpfImage?[_entries.Length];
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        for (var i = 0; i < _entries.Length; i++)
        {
            if (_entries[i] is null || _entries[i].Frame < 0 ||
                (i > 0 && _entries[i].Frame < _entries[i - 1].Frame))
                throw new ArgumentException("Input script frames must be nonnegative and ordered.");
            var entry = _entries[i];
            if ((uint)entry.Drive >= (uint)driveCount)
                throw new ArgumentException("Input script refers to a disconnected floppy drive.");
            if (entry.AdfPath is not null && entry.DiskPath is not null)
                throw new ArgumentException("Specify diskPath or the legacy adfPath, not both.");
            if ((entry.DiskPath ?? entry.AdfPath) is { } mediaPath)
            {
                if (entry.EjectAdf || string.IsNullOrWhiteSpace(mediaPath))
                    throw new ArgumentException("Use separate, nonempty mount and eject entries.");
                // File/ZIP reading is outside execution. Mount-time track encoding
                // is still excluded from acceptance timing by the runner's guard.
                _media[i] = readAdf(Path.GetFullPath(mediaPath, directory));
                if (IsIpf(_media[i]!, mediaPath)) _ipf[i] = LightweightIpfImage.Prepare(_media[i]!);
            }
        }
    }

    internal bool ChangesMediaBetween(int firstFrame, int endFrame)
        => Array.Exists(_entries, e => e.Frame >= firstFrame && e.Frame < endFrame &&
            (e.EjectAdf || e.AdfPath is not null || e.DiskPath is not null));

    internal void Apply(LightweightA500Machine m, int frame)
    {
        while (_next < _entries.Length && _entries[_next].Frame == frame)
        {
            var e = _entries[_next];
            if (e.EjectAdf) m.EjectAdf(e.Drive);
            if (_ipf[_next] is { } ipf) m.MountIpf(e.Drive, ipf);
            else if (_media[_next] is { } media) m.MountAdf(e.Drive, media);
            _next++;
            m.SubmitInput(new(e.JoystickPort0, e.JoystickPort1, e.MouseButtons,
                e.MouseDeltaX, e.MouseDeltaY, e.KeyCode));
        }
    }

    private static bool IsIpf(byte[] image, string path)
        => Path.GetExtension(path).Equals(".ipf", StringComparison.OrdinalIgnoreCase) ||
            (!Path.GetExtension(path).Equals(".adf", StringComparison.OrdinalIgnoreCase) && image.AsSpan().StartsWith("CAPS"u8));

    internal static void Mount(LightweightA500Machine machine, int drive, byte[] image, string path)
    {
        if (IsIpf(image, path)) machine.MountIpf(drive, image);
        else machine.MountAdf(drive, image);
    }
}
