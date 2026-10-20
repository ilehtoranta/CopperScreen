namespace CopperMod.Amiga.Lightweight;

public sealed partial class LightweightA500Machine
{
    /// <summary>Flush host-backed storage on the machine owner without advancing guest time.</summary>
    public void FlushPersistentStorage()
    {
        if (_hdf == null) return;
        foreach (var disk in _hdf.CopperHdf.Hardfiles.Values)
            if (!disk.ReadOnly) disk.Flush();
        _hdf.HostFs?.FlushPersistentStorage();
    }
}
