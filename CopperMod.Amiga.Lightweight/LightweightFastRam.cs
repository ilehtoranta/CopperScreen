namespace CopperMod.Amiga.Lightweight;

// CPU-only, 16-bit Zorro II memory. Agnus DMA never sees this storage.
internal sealed class LightweightFastRam : AutoconfigBoard
{
    internal byte[] Memory { get; }

    internal static bool IsSupportedSize(int bytes)
        => bytes is 0 or 524288 or 1048576 or 2097152 or 4194304 or 8388608;

    internal LightweightFastRam(int bytes) : base(CreateIdentity(bytes))
        => Memory = new byte[bytes];

    private static AutoconfigIdentity CreateIdentity(int bytes)
    {
        if (bytes == 0 || !IsSupportedSize(bytes)) throw new ArgumentOutOfRangeException(nameof(bytes));
        // Virtual Copper expansion identity; RAM-list and 8 MiB-space flags.
        var identity = AutoconfigIdentity.CreateIoBoard(bytes, 0x07DB, 0x49, 0);
        return identity with { Type = (byte)(identity.Type | 0x20), Flags = 0x80 };
    }

    public override bool IsDirectRam => true;

    protected override void ValidateConfigurationBase(uint address)
    {
        var size = (uint)Memory.Length;
        if (address < 0x200000 || (ulong)address + size > 0xA00000 || (address & 0xFFFF) != 0)
            throw new ArgumentOutOfRangeException(nameof(address), address, "Fast RAM must fit in Zorro II memory space at a 64 KiB Autoconfig address.");
    }

    internal bool ContainsRange(uint address, int count)
        => IsConfigured && count >= 0 && address >= ConfiguredBase &&
            (ulong)(address - ConfiguredBase) + (uint)count <= (uint)Memory.Length;

    public override bool ContainsBoardAddress(uint address) => ContainsRange(address, 1);
    public override byte ReadBoardByte(uint address)
        => ContainsBoardAddress(address) ? Memory[address - ConfiguredBase] : (byte)255;
    public override bool TryWriteBoardByte(uint address, byte value)
    {
        if (!ContainsBoardAddress(address)) return false;
        Memory[address - ConfiguredBase] = value;
        return true;
    }
}

public sealed partial class LightweightA500Machine
{
    private readonly LightweightFastRam? _fastRam;
    private readonly AutoconfigChain? _autoconfig;

    /// <summary>Installed CPU-only Zorro II RAM. Read on the execution owner thread.</summary>
    public ReadOnlyMemory<byte> FastRam => _fastRam?.Memory ?? ReadOnlyMemory<byte>.Empty;
    /// <summary>Guest-assigned address, or null until Autoconfig completes.</summary>
    public uint? FastRamBase => _fastRam is { IsConfigured: true } ram ? ram.ConfiguredBase : null;

    internal byte ReadExpansionByte(uint address)
        => _autoconfig is { } chain &&
            (chain.TryReadByte(address, out var value) || chain.TryReadConfiguredByte(address, out value))
                ? value : (byte)255;

    internal bool TryWriteExpansionByte(uint address, byte value)
        => _autoconfig is { } chain &&
            (chain.TryWriteByte(address, value) || chain.TryWriteConfiguredByte(address, value));
}
