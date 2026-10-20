using System.IO.Compression;
using System.Text.Json;

namespace CopperScreen;

internal sealed partial class CopperOsWorkspace
{
    private sealed record Backup(int Format, string SeedPairId, string SeedSha256, string ImageSha256, int Bytes);
    private static void Admit(Identity identity)
    {
        var contract = Contract;
        if (!new[] { new Seed(contract.PairId, contract.Sha256) }.Concat(contract.CompatibleWorkspaceSeeds ?? [])
            .Any(seed => seed.PairId == identity.SeedPairId && seed.Sha256 == identity.SeedSha256))
            throw new NotSupportedException("This workspace backup belongs to an unsupported system pair. Preserve it and use a compatible app version.");
    }
    private static FileStream Lease(string image) => new(Path.Combine(Path.GetDirectoryName(image)!, "workspace.lock"),
        FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

    public static bool IsManagedDestination(string path)
    {
        for (var directory = Path.GetDirectoryName(Path.GetFullPath(path)); directory != null; directory = Path.GetDirectoryName(directory))
            if (File.Exists(Path.Combine(directory, "workspace.json"))) return true;
        return false;
    }

    private static void WriteBackup(string destination, byte[] bytes, string pair, string seedHash)
    {
        if (!Path.GetExtension(destination).Equals(".copperos", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a new .copperos file for the workspace backup.");
        if (IsManagedDestination(destination)) throw new IOException("Export outside managed workspace folders.");
        ValidateImage(bytes);
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var image = zip.CreateEntry("System.adf").Open()) image.Write(bytes);
            using var manifest = zip.CreateEntry("export.json").Open();
            JsonSerializer.Serialize(manifest, new Backup(1, pair, seedHash, Hash(bytes), bytes.Length));
        }
        CopperScreenAtomicFile.Write(destination, buffer.ToArray());
    }

    internal void ExportBackup(string destination, byte[] bytes) => WriteBackup(destination, bytes, PairId, SeedSha256);

    public static void ExportSaved(string image, string destination, bool previous = false)
    {
        image = Path.GetFullPath(image);
        // Preserve an older pair for import by a compatible app; exporting is not boot admission.
        var identity = ReadIdentity(image, requireImage: !previous);
        using var lease = Lease(image);
        var source = previous ? Path.Combine(Path.GetDirectoryName(image)!, "Previous.adf") : image;
        RejectLinks(source);
        var bytes = ReadImage(source); ValidateImage(bytes);
        WriteBackup(destination, bytes, identity.SeedPairId, identity.SeedSha256);
    }

    public static string ImportBackup(string userData, string id, string backup)
    {
        ValidateId(id);
        using var source = File.OpenRead(backup);
        if (source.Length > 4 * 1024 * 1024) throw new InvalidDataException("Workspace backup exceeds the supported size.");
        using var zip = new ZipArchive(source, ZipArchiveMode.Read);
        if (zip.Entries.Count != 2 || zip.GetEntry("export.json") is not { Length: > 0 and <= 4096 } metadata ||
            zip.GetEntry("System.adf") is not { Length: 901120 } image)
            throw new InvalidDataException("Choose a complete CopperOS backup containing its system image and version record.");
        var metadataBytes = new byte[checked((int)metadata.Length)];
        using (var manifest = metadata.Open())
        {
            manifest.ReadExactly(metadataBytes);
            if (manifest.ReadByte() != -1) throw new InvalidDataException("Workspace backup version record exceeds its declared size.");
        }
        Backup record;
        try { record = JsonSerializer.Deserialize<Backup>(metadataBytes) ?? throw new InvalidDataException("Workspace backup version record is missing."); }
        catch (JsonException ex) { throw new InvalidDataException("Workspace backup version record is damaged. Keep the backup and choose a complete compatible copy.", ex); }
        if (record.Format != 1 || record.Bytes != 901120) throw new NotSupportedException("Unsupported workspace backup version.");
        Admit(new(1, id, CopperScreenUserData.GetDirectory(userData), record.SeedPairId, record.SeedSha256));
        var bytes = new byte[901120]; using (var disk = image.Open()) { disk.ReadExactly(bytes); if (disk.ReadByte() != -1) throw new InvalidDataException("Workspace backup image is too large."); }
        if (Hash(bytes) != record.ImageSha256) throw new InvalidDataException("Workspace backup is damaged. Its image and integrity record do not match.");
        ValidateImage(bytes);
        return CreateCopy(userData, id, bytes, new(record.SeedPairId, record.SeedSha256), requireNew: true);
    }

    public static string RecoverPrevious(string image)
    {
        image = Path.GetFullPath(image);
        var identity = ReadIdentity(image, requireImage: false); Admit(identity);
        using var lease = Lease(image);
        var previous = Path.Combine(Path.GetDirectoryName(image)!, "Previous.adf");
        RejectLinks(previous); var bytes = ReadImage(previous); ValidateImage(bytes);
        var preserved = Path.Combine(Path.GetDirectoryName(image)!, "Before-recovery-" + Guid.NewGuid().ToString("N") + ".adf");
        CopperScreenAtomicFile.Write(image, bytes, overwrite: true, backup: preserved);
        return File.Exists(preserved) ? preserved : previous;
    }

    private static byte[] ReadImage(string path)
    {
        using var source = File.OpenRead(path);
        if (source.Length != 901120) throw new InvalidDataException("The saved workspace image has the wrong size. Preserve it before repair.");
        var bytes = new byte[901120]; source.ReadExactly(bytes); return bytes;
    }
}
