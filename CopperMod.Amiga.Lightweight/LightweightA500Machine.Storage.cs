namespace CopperMod.Amiga.Lightweight;

public sealed partial class LightweightA500Machine
{
    private long _nextStorageCycle = long.MaxValue;
    private readonly bool _hasClockedStorage;
    internal bool HasClockedStorage => _hasClockedStorage;
    internal long NextStorageCycle => _nextStorageCycle;
    internal IReadOnlyDictionary<int, LightweightDriveTimingProfile> StorageTimings => _configuration.HardfileTimings;
    internal ILightweightStorageObserver? StorageObserver => _configuration.StorageObserver;
    private bool TimedStorageInterrupt => _hdf?.CopperHdf.TimedInterrupt == true;
    internal void TickStorage(long cycle)
    {
        _a1200Io?.Step(cycle);
        _hdf?.CopperHdf.StepTimed(cycle);
        RefreshStorageSchedule(cycle);
    }
    internal void RefreshStorageSchedule(long cycle)
    {
        _nextStorageCycle = Math.Min(_a1200Io?.NextCycle ?? long.MaxValue, _hdf?.CopperHdf.NextTimedCycle ?? long.MaxValue);
        if (_a1200Io?.InterruptAsserted == true || TimedStorageInterrupt)
            LatchHardwareInterrupt(0x0008, cycle, cpuVisibilityDelay: 0);
        RefreshNextDeviceCycle();
    }
    /// <summary>Flush host-backed storage on the machine owner without advancing guest time.</summary>
    public void FlushPersistentStorage()
    {
        if (_hdf == null) return;
        foreach (var disk in _hdf.CopperHdf.Hardfiles.Values)
            if (!disk.ReadOnly) disk.Flush();
        _hdf.HostFs?.FlushPersistentStorage();
    }
}
