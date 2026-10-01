namespace CopperMod.Amiga.Lightweight;

/// <summary>Implemented PAL Agnus/Alice motherboard revisions; Denise/Lisa is selected separately.</summary>
public enum LightweightAgnusModel
{
    /// <summary>OCS 8371: 512 KiB Chip RAM, optional 512 KiB trapdoor slow RAM.</summary>
    Mos8371,
    /// <summary>ECS 8372A: up to 1 MiB Chip RAM, with the A500 JP2 memory layout.</summary>
    Mos8372A,
    /// <summary>ECS PAL 8375, Commodore part 318069-10: up to 2 MiB Chip RAM.</summary>
    Mos8375Pal2M,
    /// <summary>AGA 8374 Alice on the PAL A1200 board, with 2 MiB Chip RAM.</summary>
    Mos8374Alice
}

/// <summary>Construction-time memory rules for the implemented motherboard layouts.</summary>
public static class LightweightAgnus
{
    public static void ValidateMemory(LightweightAgnusModel model, int chipRamBytes, int slowRamBytes)
    {
        var valid = model switch
        {
            LightweightAgnusModel.Mos8371 => chipRamBytes == 524288 && slowRamBytes is 0 or 524288,
            LightweightAgnusModel.Mos8372A =>
                chipRamBytes == 524288 && slowRamBytes is 0 or 524288 ||
                chipRamBytes == 1048576 && slowRamBytes == 0,
            LightweightAgnusModel.Mos8375Pal2M => chipRamBytes is 1048576 or 2097152 && slowRamBytes == 0,
            LightweightAgnusModel.Mos8374Alice => chipRamBytes == 2097152 && slowRamBytes == 0,
            _ => false
        };
        if (!valid)
            throw new ArgumentException("PAL 8371: 512 KiB Chip + 0/512 KiB slow; 8372A: 512 KiB Chip + 0/512 KiB slow or 1 MiB Chip without slow; 8375 (318069-10): 1/2 MiB Chip without slow RAM; A1200 Alice: 2 MiB Chip without slow RAM.");
    }
}
