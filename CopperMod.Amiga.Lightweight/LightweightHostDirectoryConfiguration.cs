namespace CopperMod.Amiga.Lightweight;

/// <summary>A live native AmigaDOS volume backed by an existing host folder.</summary>
public sealed record LightweightHostDirectoryConfiguration(
    string DeviceName, string VolumeName, string Path, bool ReadOnly = true, int? BootPriority = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DeviceName) || DeviceName.Length > 30 ||
            DeviceName.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
            throw new ArgumentException("Shared-folder device names require 1–30 ASCII letters, digits or underscores.");
        if (string.IsNullOrWhiteSpace(VolumeName) || VolumeName.Length > 30 ||
            VolumeName.Any(c => c < 32 || c > 255 || c is ':' or '/' or '\\'))
            throw new ArgumentException("Shared-folder volume names require 1–30 Amiga characters, without path separators.");
        if (DeviceName.ToUpperInvariant() is "RAM" or "CON" or "RAW" or "NIL" or "PIPE" or "SER" or "PAR" or "PRT" ||
            DeviceName.Length == 3 && DeviceName.StartsWith("DF", StringComparison.OrdinalIgnoreCase) && char.IsAsciiDigit(DeviceName[2]))
            throw new ArgumentException($"Shared-folder device name {DeviceName} is reserved.");
        if (BootPriority is < -127 or > 127) throw new ArgumentOutOfRangeException(nameof(BootPriority));
        if (string.IsNullOrWhiteSpace(Path)) throw new ArgumentException("Choose an existing shared folder.");
    }
}
