namespace CopperScreen;

// Complete host snapshots only. Guest writes still run through the existing owner.
internal static class CopperScreenAtomicFile
{
    internal enum Stage { Partial, Flushed, Published }
#if COPPERSCREEN_STORAGE_TESTS
    internal static Action<Stage>? TestObserver;
#endif
    [System.Diagnostics.Conditional("COPPERSCREEN_STORAGE_TESTS")]
    private static void Observe(Stage stage)
    {
#if COPPERSCREEN_STORAGE_TESTS
        TestObserver?.Invoke(stage);
#endif
    }

    public static void Write(string destination, ReadOnlySpan<byte> bytes, bool overwrite = false,
        string? backup = null)
    {
        destination = Path.GetFullPath(destination);
        ValidateDestination(destination);
        if (backup != null) ValidateDestination(backup);
        if (!overwrite && File.Exists(destination)) throw new IOException("The destination already exists. Choose a new name, or explicitly confirm replacement.");
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".snapshot-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
#if COPPERSCREEN_STORAGE_TESTS
                stream.Write(bytes[..(bytes.Length / 2)]);
                Observe(Stage.Partial);
                stream.Write(bytes[(bytes.Length / 2)..]);
#else
                stream.Write(bytes);
#endif
                stream.Flush(flushToDisk: true);
            }
            Observe(Stage.Flushed);
            // Recheck after staging; never follow a newly introduced link.
            ValidateDestination(destination);
            if (overwrite && File.Exists(destination)) File.Replace(temporary, destination, backup);
            else File.Move(temporary, destination); // No overwrite, including a racing newly created destination.
            Observe(Stage.Published);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static void ValidateDestination(string path)
    {
        path = Path.GetFullPath(path);
        var install = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (path.StartsWith(install, StringComparison.OrdinalIgnoreCase)) throw new IOException("Save outside the application installation.");
        for (var item = path; item != null; item = Path.GetDirectoryName(item))
            if (Path.Exists(item) && (File.GetAttributes(item) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Save to a location without linked files or folders.");
    }
}
