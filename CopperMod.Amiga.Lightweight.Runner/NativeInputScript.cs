using System.Text.Json;
using CopperMod.Amiga.Lightweight;

// Parsed once outside execution timing. Frame zero precedes the first ExecuteFrame.
internal sealed class NativeInputScript
{
    internal sealed record Entry(int Frame, byte MouseButtons = 0, short MouseDeltaX = 0,
        short MouseDeltaY = 0, ushort KeyCode = 0, byte JoystickPort0 = 0, byte JoystickPort1 = 0,
        string? AdfPath = null, bool EjectAdf = false, int Drive = 0);
    private readonly Entry[] _entries;
    private readonly byte[]?[] _media;
    private int _next;

    internal NativeInputScript(string path, Func<string, byte[]> readAdf, int driveCount = 1)
    {
        _entries = JsonSerializer.Deserialize<Entry[]>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
            throw new ArgumentException("Input script must be an array.");
        _media = new byte[]?[_entries.Length];
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        for (var i = 0; i < _entries.Length; i++)
        {
            if (_entries[i] is null || _entries[i].Frame < 0 ||
                (i > 0 && _entries[i].Frame < _entries[i - 1].Frame))
                throw new ArgumentException("Input script frames must be nonnegative and ordered.");
            var entry = _entries[i];
            if ((uint)entry.Drive >= (uint)driveCount)
                throw new ArgumentException("Input script refers to a disconnected floppy drive.");
            if (entry.AdfPath is not null)
            {
                if (entry.EjectAdf || string.IsNullOrWhiteSpace(entry.AdfPath))
                    throw new ArgumentException("Use separate, nonempty mount and eject entries.");
                // File/ZIP reading is outside execution. Mount-time track encoding
                // is still excluded from acceptance timing by the runner's guard.
                _media[i] = readAdf(Path.GetFullPath(entry.AdfPath, directory));
            }
        }
    }

    internal bool ChangesMediaBetween(int firstFrame, int endFrame)
        => Array.Exists(_entries, e => e.Frame >= firstFrame && e.Frame < endFrame &&
            (e.EjectAdf || e.AdfPath is not null));

    internal void Apply(LightweightA500Machine m, int frame)
    {
        while (_next < _entries.Length && _entries[_next].Frame == frame)
        {
            var e = _entries[_next];
            if (e.EjectAdf) m.EjectAdf(e.Drive);
            if (_media[_next] is { } media) m.MountAdf(e.Drive, media);
            _next++;
            m.SubmitInput(new(e.JoystickPort0, e.JoystickPort1, e.MouseButtons,
                e.MouseDeltaX, e.MouseDeltaY, e.KeyCode));
        }
    }
}
