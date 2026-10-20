using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using CopperMod.Amiga.Lightweight;

namespace CopperScreen;

// Host ownership of complete media only. Guest DOS/filesystem operations remain
// in the native ROM, through CIA/Paula. Never mount a package seed writable.
internal sealed partial class CopperOsWorkspace : IDisposable
{
    internal sealed record Seed(string PairId, string Sha256);
    private sealed record SeedContract(string PairId, string Sha256, Seed[]? CompatibleWorkspaceSeeds);
    private sealed record Identity(int Format, string Id, string UserDataRoot, string SeedPairId, string SeedSha256);
    private readonly FileStream _lease;
    private int _owners = 1;
    internal CopperOsWorkspace Retain() { _owners++; return this; }
    public string ImagePath { get; }
    public string DirectoryPath => Path.GetDirectoryName(ImagePath)!;
    public string PairId { get; }
    public string SeedSha256 { get; }
    private CopperOsWorkspace(string image, Identity identity, FileStream lease)
        => (ImagePath, PairId, SeedSha256, _lease) = (image, identity.SeedPairId, identity.SeedSha256, lease);
    private static SeedContract Contract
    {
        get
        {
            using var stream = typeof(CopperOsWorkspace).Assembly.GetManifestResourceStream("CopperScreen.Firmware.CopperOS.json")!;
            return JsonSerializer.Deserialize<SeedContract>(stream)!;
        }
    }
    internal static Seed BundledSeed
    {
        get { var contract = Contract; return new(contract.PairId, contract.Sha256); }
    }
    public static string Prepare(string userData, string id, string? seedPath, Seed? seed = null)
    {
        ValidateId(id); userData = CopperScreenUserData.GetDirectory(userData);
        var root = Path.Combine(userData, "CopperOS", "Workspaces");
        var directory = Path.Combine(root, id); var image = Path.Combine(directory, "System.adf");
        if (Directory.Exists(directory)) { ReadIdentity(image); return image; }
        if (seedPath == null) throw new FileNotFoundException("CopperOS workspace is missing. Create it from the qualified system image.", image);
        seed ??= BundledSeed;
        if (new FileInfo(seedPath).Length != 901120) throw new InvalidDataException("CopperOS requires a complete 880 KiB system seed.");
        var bytes = File.ReadAllBytes(seedPath);
        if (Hash(bytes) != seed.Sha256) throw new InvalidDataException("CopperOS seed does not match the qualified pair. Existing workspaces have not been changed.");
        ValidateImage(bytes);
        return CreateCopy(userData, id, bytes, seed, requireNew: false);
    }
    private static string CreateCopy(string userData, string id, byte[] bytes, Seed seed, bool requireNew)
    {
        ValidateId(id); userData = CopperScreenUserData.GetDirectory(userData);
        var root = Path.Combine(userData, "CopperOS", "Workspaces");
        var directory = Path.Combine(root, id); var image = Path.Combine(directory, "System.adf");
        if (Directory.Exists(directory)) throw new IOException("That workspace already exists. Use a new name; existing files are unchanged.");
        Directory.CreateDirectory(root);
        RejectLinks(root);
        var staging = Path.Combine(root, ".creating-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            WriteFile(Path.Combine(staging, "System.adf"), bytes);
            CopperScreenUserData.WriteAtomically(Path.Combine(staging, "workspace.json"),
                JsonSerializer.Serialize(new Identity(1, id, userData, seed.PairId, seed.Sha256)));
            try { Directory.Move(staging, directory); }
            catch (IOException) when (!requireNew && Directory.Exists(directory)) { ReadIdentity(image); }
        }
        finally
        {
            // Only our two known staging files; no recursive deletion of user data.
            if (Directory.Exists(staging))
            {
                foreach (var name in new[] { "System.adf", "workspace.json" }) File.Delete(Path.Combine(staging, name));
                Directory.Delete(staging);
            }
        }
        return image;
    }
    public static bool IsWorkspace(string? image)
        => image != null && Path.GetFileName(image).Equals("System.adf", StringComparison.OrdinalIgnoreCase) &&
           File.Exists(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(image))!, "workspace.json"));
    public static void ValidateOwnership(string image)
    {
        var contract = Contract;
        ValidateCompatibility(image, [new(contract.PairId, contract.Sha256), .. contract.CompatibleWorkspaceSeeds ?? []]);
    }
    internal static void ValidateCompatibility(string image, Seed[] admittedSeeds)
    {
        var identity = ReadIdentity(image);
        if (!admittedSeeds.Any(seed => seed.PairId == identity.SeedPairId && seed.Sha256 == identity.SeedSha256))
            throw new NotSupportedException("This CopperOS workspace belongs to an unsupported system pair. Keep or export its disk before choosing a compatible app version; it has not been reset or migrated.");
    }
    public static CopperOsWorkspace Open(string image)
    {
        image = Path.GetFullPath(image); var identity = ReadIdentity(image);
        var lease = new FileStream(Path.Combine(Path.GetDirectoryName(image)!, "workspace.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            using var writable = new FileStream(image, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            if (writable.Length != 901120) throw new InvalidDataException("CopperOS workspace has the wrong size. Preserve it before repair.");
            var bytes = new byte[901120]; writable.ReadExactly(bytes); ValidateImage(bytes);
            return new(image, identity, lease);
        }
        catch { lease.Dispose(); throw; }
    }
    private static Identity ReadIdentity(string image, bool requireImage = true)
    {
        image = Path.GetFullPath(image); var directory = Path.GetDirectoryName(image)!;
        RejectLinks(directory);
        if (requireImage || File.Exists(image)) RejectLinks(image);
        var identity = JsonSerializer.Deserialize<Identity>(File.ReadAllText(Path.Combine(directory, "workspace.json")))
            ?? throw new InvalidDataException("CopperOS workspace identity is missing. Export the workspace before repair.");
        ValidateId(identity.Id);
        if (identity.Format != 1 || string.IsNullOrWhiteSpace(identity.SeedPairId) || identity.SeedSha256?.Length != 64 ||
            !Path.IsPathFullyQualified(identity.UserDataRoot) ||
            !Path.GetFullPath(Path.Combine(identity.UserDataRoot, "CopperOS", "Workspaces", identity.Id, "System.adf")).Equals(image, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("CopperOS workspace location or format is incompatible. Preserve/export it before migration.");
        RejectLinks(Path.Combine(directory, "workspace.json"));
        foreach (var part in new[] { identity.UserDataRoot, Path.Combine(identity.UserDataRoot,"CopperOS"), Path.Combine(identity.UserDataRoot,"CopperOS","Workspaces") }) RejectLinks(part);
        var install = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (image.StartsWith(install, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("A CopperOS workspace must live in user data, outside the application installation.");
        return identity;
    }
    internal static void ValidateId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 64 || !id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ||
            id.Equals("CON", StringComparison.OrdinalIgnoreCase) || id.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            id.Equals("AUX", StringComparison.OrdinalIgnoreCase) || id.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            (id.Length == 4 && (id.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || id.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && char.IsAsciiDigit(id[3])))
            throw new ArgumentException("Choose a workspace name containing letters, numbers, '-' or '_'.");
    }
    private static void RejectLinks(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("A CopperOS workspace cannot use linked files or directories.");
    }
    internal static void ValidateImage(ReadOnlySpan<byte> image)
    {
        if (image.Length != 901120 || BinaryPrimitives.ReadUInt32BigEndian(image) != 0x444F5301) throw new InvalidDataException("CopperOS requires a complete DOS1 DD workspace image.");
        var root = image.Slice(880 * 512, 512);
        if (U(root, 0) != 2 || U(root, 12) != 72 || U(root, 508) != 1 || U(root, 312) != uint.MaxValue || !Checksum(root))
            throw new InvalidDataException("CopperOS metadata is unfinished or damaged. The last complete workspace is retained; export it before repair.");
        var bitmap = U(root, 316);
        if (bitmap < 2 || bitmap >= 1760 || bitmap == 880 || !Checksum(image.Slice((int)bitmap * 512, 512)))
            throw new InvalidDataException("CopperOS allocation metadata is damaged. Export the workspace before repair.");
    }
    private static uint U(ReadOnlySpan<byte> b, int at) => BinaryPrimitives.ReadUInt32BigEndian(b[at..]);
    private static bool Checksum(ReadOnlySpan<byte> b)
    { uint sum = 0; for (var at = 0; at < 512; at += 4) sum = unchecked(sum + U(b, at)); return sum == 0; }
    public bool Checkpoint(LightweightA500Machine machine)
    {
        if (machine.GetDriveState(0) is { ActiveDma: true } or { MotorOn: true }) return false;
        if (!machine.IsDriveDirty(0)) return true;
        byte[] bytes;
        try { bytes = machine.ExportAdf(0); }
        catch (InvalidOperationException ex) { throw new IOException("CopperOS has an incomplete disk track. The last complete workspace is retained; preserve/export it before repair.", ex); }
        try { ValidateImage(bytes); } catch (InvalidDataException) { return false; }
        Save(bytes); machine.MarkAdfSaved(0); return true;
    }
    internal void Save(byte[] bytes)
    {
        ValidateImage(bytes);
        CopperScreenAtomicFile.Write(ImagePath, bytes, overwrite: true,
            backup: Path.Combine(DirectoryPath, "Previous.adf"));
    }
    private static void WriteFile(string path, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes); stream.Flush(flushToDisk: true);
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    public void Dispose() { if (--_owners == 0) _lease.Dispose(); }
}
