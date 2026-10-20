namespace CopperDisk;

/// <summary>Inspect existing RDB media through the same parser used by CopperHDF.</summary>
public static class AmigaRdbImage
{
    /// <summary>Require a readable, checksum-valid RDB image without changing it.</summary>
    public static void Validate(string path)
    {
        using var image = AmigaHardfile.Open(new(0, path, readOnly: true,
            mountMode: AmigaHardfileMountMode.RigidDiskBlock));
        _ = image.GetMountablePartitions();
    }
}
