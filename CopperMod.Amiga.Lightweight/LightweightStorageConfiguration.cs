using CopperDisk;

namespace CopperMod.Amiga.Lightweight;

/// <summary>Versioned synthetic media parameters. Times are microseconds; rates are bytes/second.</summary>
public sealed record LightweightDriveTimingProfile
{
    public int Version { get; init; } = 1;
    public string Name { get; init; } = "hdd-like-v1";
    public int CommandMicroseconds { get; init; } = 500;
    public int Heads { get; init; } = 4;
    public int SectorsPerTrack { get; init; } = 64;
    public int RevolutionsPerMinute { get; init; } = 3600;
    public int SeekBaseMicroseconds { get; init; } = 2000;
    public int SeekPerCylinderMicroseconds { get; init; } = 80;
    public int HeadSwitchMicroseconds { get; init; } = 200;
    public int TrackSkewSectors { get; init; } = 1;
    public int ReadAheadBytes { get; init; } = 65536;
    public long TransferBytesPerSecond { get; init; } = 2 * 1024 * 1024;
    public static LightweightDriveTimingProfile Hdd { get; } = new();
    public static LightweightDriveTimingProfile Cf { get; } = new()
    {
        Name = "cf-like-v1", CommandMicroseconds = 50, RevolutionsPerMinute = 0,
        SeekBaseMicroseconds = 0, SeekPerCylinderMicroseconds = 0,
        HeadSwitchMicroseconds = 0, TrackSkewSectors = 0, ReadAheadBytes = 0,
        TransferBytesPerSecond = 8 * 1024 * 1024
    };

    internal void Validate()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(Name) || CommandMicroseconds < 0 ||
            Heads is < 1 or > 16 || SectorsPerTrack is < 1 or > 255 ||
            RevolutionsPerMinute is < 0 or > 30000 || SeekBaseMicroseconds < 0 ||
            SeekPerCylinderMicroseconds < 0 || HeadSwitchMicroseconds < 0 ||
            TrackSkewSectors < 0 || ReadAheadBytes is < 0 or > 1048576 ||
            ReadAheadBytes % 512 != 0 || TransferBytesPerSecond is < 1 or > 1_000_000_000)
            throw new ArgumentException("Invalid synthetic drive timing profile.");
    }
}

/// <summary>Read-only ATA master on the A1200's native Gayle interface.</summary>
public sealed record LightweightIdeDriveConfiguration(AmigaHardfileConfiguration Media)
{
    public LightweightDriveTimingProfile Timing { get; init; } = LightweightDriveTimingProfile.Hdd;
}

/// <summary>Optional bounded storage observations, invoked only by fitted storage operations.</summary>
public interface ILightweightStorageObserver
{
    void ReadCommand(bool ide, ulong sector, uint bytes, long cycle);
    void MediaRead(ulong sector, long cycle);
    void PioRead(int bytes, long cycle);
    void Completed(bool ide, long cycle);
    void AtaAccess(uint address, int bytes, bool write, uint value, long cycle) { }
}
