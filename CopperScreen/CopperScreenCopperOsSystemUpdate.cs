using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace CopperScreen;

// Same-length updates of recognized, app-supplied FFS system files. No
// directory, allocation bitmap, metadata or unrelated file bytes are changed.
internal static class CopperScreenCopperOsSystemUpdate
{
    internal const string PreviousHomeSha256 = "5368BFDE4DC393B5E1899A7344C78BCD8FE07FBB27B8862CFA583DDC40A8A258";
    internal const string PreviousCommandSha256 = "E7321120811F127E7B83F440E7C9C34DABCCB1209A0FF595284BD6F960595A2A";
    private const string BlitterHomeSha256 = "527E2229FB112F400227D89F39A53D482DC2CFC0677E3ED000B3E974FBC0236A";
    private const string BlitterCommandSha256 = "B1A3B48F376E20251290008A974C64C98034468F3DE62639C02D4BBC04681F58";
    private const string PreviousGuiSha256 = "5900735263A90AC53626E6103FD6B232329667AE03D78105BC74D88E2BC085FD";
    private const string TextHomeSha256 = "E34C30C6FDC979CBC42ECD4F726421DF7709CE97EF97855515D2B4213F1AC7D8";
    private const string TextCommandSha256 = "95B1E91A5D0DE56D3B5363A214B04776DA5BDB84CDF56051A821FBA53898E754";
    private const string TextGuiSha256 = "F74E041915C2824E5D21DC2E76ECB7764833245A429BBA08A238A3B6360EBE3C";
    private const string RedrawHomeSha256 = "BEF364F643E65D27F9C27C7232FE8A86B3A01F15D13EFA7F36A43D4DFC5B3C58";
    private const string RedrawCommandSha256 = "A522E79895FFB093CA7B7C1713CE0350A10FD9A240110D602042DD2CA8F3A02B";
    private const string RedrawGuiSha256 = "473BED6B273352740081D9B9ECD5E2290D9189AB0C85A73BFCD94D77DD8B55C6";
    private const string LayerBridgeHomeSha256 = "22AB094AC2F5F6E5A71AFEDC1C5B13CF378D04E0D5F92590048F7FF0C7AD7C5A";
    private const string LayerBridgeCommandSha256 = "E16BD06E979690AF64EFFC6B5E99CAB14D0C1ADD9B29EDC9C370344264F51F8C";
    private const string LayerBridgeGuiSha256 = "91D8EEB662FEDDC410096E3642F37B5A76E307BFDFB1B66A389A3F739A97FF54";
    private const string LayerResultHomeSha256 = "2EF15DC76976973B37F7131D419D44961899C2FF3D5E4D2E469CAFCD39ADE88E";
    private const string LayerResultCommandSha256 = "B97CEB0D6B118330425C5A35CE827378C51B2A8BEBCC6E8AFE27615C72EB6D2C";
    private const string LayerResultGuiSha256 = "1FB6B5A30565567E388681E082DD65D8E48D45E3354B5755C52316EEFD9636B7";
    private const string LayerAdmissionHomeSha256 = "FEAF6A59B184BED6E8E15E047CFF784F8A13B84292DAC94ECC30D5E3B116AEFE";
    private const string LayerAdmissionCommandSha256 = "76FA20DB1D23B0E70F193FD79A19A0FB357C890C7E8C242CE3E9A24E810B5C66";
    private const string LayerAdmissionGuiSha256 = "D814CD8669B17AA4A9F43179BFFCCC80C7C9F61A5A3C26B0C6FB7407A010E0A5";

    private const string LayerLockHomeSha256 = "A8678989641DF2FFEA9A5CB10AB8DDCEAA4DC23A3C034B73CDB27EFBBA27E157";
    private const string LayerLockCommandSha256 = "042A8AB4871452B6BBC3F10ED6A93F38CEB6A85FAF33170C9388A369DC11E33E";
    private const string LayerLockGuiSha256 = "84FE4E97B5C6CDFFA1867EEE6849194F67FDC2B837871F9BF06216DF660BB0D4";

    internal static byte[] ReadHome(byte[] image) => new Disk(image).File("L", "CopperHome").Bytes;
    internal static byte[] ReadCommand(byte[] image) => new Disk(image).File("C", "Desktop").Bytes;
    internal static byte[] ReadGui(byte[] image) => new Disk(image).File("L", "CopperGUI").Bytes;

    internal static bool TryCreateImage(byte[] image, byte[] replacement, string previousSha256, out byte[]? updated)
        => TryCreateFile(image, "L", "CopperHome", replacement, previousSha256, out updated);

    private static bool TryCreateFile(byte[] image, string directory, string name, byte[] replacement, string previousSha256, out byte[]? updated)
        => TryCreateFile(image, directory, name, replacement, [previousSha256], out updated);

    private static bool TryCreateFile(byte[] image, string directory, string name, byte[] replacement, string[] previousSha256, out byte[]? updated)
    {
        updated = null;
        var home = new Disk(image).File(directory, name);
        if (home.Bytes.Length != replacement.Length) return false;
        var hash = Convert.ToHexString(SHA256.HashData(home.Bytes));
        if (hash.Equals(Convert.ToHexString(SHA256.HashData(replacement)), StringComparison.OrdinalIgnoreCase)) return true;
        if (!previousSha256.Contains(hash, StringComparer.OrdinalIgnoreCase)) return false;
        updated = (byte[])image.Clone();
        var offset = 0;
        foreach (var block in home.Blocks)
        {
            var count = Math.Min(512, replacement.Length - offset);
            replacement.AsSpan(offset, count).CopyTo(updated.AsSpan(checked((int)block * 512), count));
            offset += count;
        }
        return true;
    }

    internal static bool TryCreateSystemImage(byte[] image, byte[] home, byte[] command, byte[] gui, out byte[]? updated)
    {
        updated = null;
        if (!TryCreateFile(image, "L", "CopperHome", home, [PreviousHomeSha256, BlitterHomeSha256, TextHomeSha256, RedrawHomeSha256, LayerBridgeHomeSha256, LayerResultHomeSha256, LayerAdmissionHomeSha256, LayerLockHomeSha256], out var first) ||
            !TryCreateFile(first ?? image, "C", "Desktop", command, [PreviousCommandSha256, BlitterCommandSha256, TextCommandSha256, RedrawCommandSha256, LayerBridgeCommandSha256, LayerResultCommandSha256, LayerAdmissionCommandSha256, LayerLockCommandSha256], out var second) ||
            !TryCreateFile(second ?? first ?? image, "L", "CopperGUI", gui, [PreviousGuiSha256, TextGuiSha256, RedrawGuiSha256, LayerBridgeGuiSha256, LayerResultGuiSha256, LayerAdmissionGuiSha256, LayerLockGuiSha256], out var third)) return false;
        updated = third ?? second ?? first;
        return true;
    }

    internal static bool TryUpgrade(string path, byte[] replacement, byte[] command, byte[] gui)
    {
        var directory = Path.GetDirectoryName(path)!;
        var temporary = Path.Combine(directory, ".system-update-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var gate = new FileStream(Path.Combine(directory, "system-update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (input.Length != 8388608) return false;
            var original = new byte[checked((int)input.Length)]; input.ReadExactly(original);
            if (!TryCreateSystemImage(original, replacement, command, gui, out var updated)) return false;
            if (updated is null) return true;
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { output.Write(updated); output.Flush(flushToDisk: true); }
            var backup = Path.Combine(directory, "CopperOS.before-drawing-" + Guid.NewGuid().ToString("N") + ".hdf");
            File.Replace(temporary, path, backup);
            return true;
        }
        // An active session, custom system file or damaged disk keeps its
        // existing image. A later cold launch can retry a locked workspace.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private sealed class Disk(byte[] bytes)
    {
        private readonly HashSet<uint> metadata = [];
        private uint Word(uint block, int index)
        {
            if (block < 2 || block >= bytes.Length / 512) throw new InvalidDataException("Invalid FFS block");
            return BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(checked((int)block * 512 + index * 4), 4));
        }
        private void Header(uint block, uint type, uint parent, uint kind)
        {
            if (!metadata.Add(block) || Word(block, 0) != type || Word(block, 125) != parent || Word(block, 127) != kind)
                throw new InvalidDataException("Invalid FFS system header");
            uint sum = 0; for (var i = 0; i < 128; i++) sum = unchecked(sum + Word(block, i));
            if (sum != 0) throw new InvalidDataException("Invalid FFS checksum");
        }
        private uint Find(uint directory, string name, uint kind)
        {
            var hash = name.Length; foreach (var c in name.ToUpperInvariant()) hash = (hash * 13 + c) & 2047;
            var block = Word(directory, 6 + hash % 72); var chain = new HashSet<uint>();
            while (block != 0)
            {
                if (!chain.Add(block)) throw new InvalidDataException("Cyclic FFS hash chain");
                if (Word(block, 0) != 2 || Word(block, 125) != directory) throw new InvalidDataException("Invalid FFS directory entry");
                var offset = checked((int)block * 512 + 432); var length = bytes[offset];
                if (length > 30) throw new InvalidDataException("Invalid FFS name");
                if (Encoding.ASCII.GetString(bytes, offset + 1, length).Equals(name, StringComparison.OrdinalIgnoreCase))
                { Header(block, 2, directory, kind); return block; }
                block = Word(block, 124);
            }
            throw new InvalidDataException("Missing CopperOS system file");
        }
        internal (byte[] Bytes, List<uint> Blocks) File(string directoryName, string fileName)
        {
            if (bytes.Length < 1024 || bytes.Length % 512 != 0 || BinaryPrimitives.ReadUInt32BigEndian(bytes) != 0x444F5301)
                throw new InvalidDataException("Expected DOS1 FFS");
            var root = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4));
            if (root == 0) root = (uint)(bytes.Length / 1024);
            Header(root, 2, 0, 1);
            var directory = Find(root, directoryName, 2); var header = Find(directory, fileName, 0xFFFFFFFD);
            var length = Word(header, 81);
            if (length > bytes.Length) throw new InvalidDataException("Invalid system file size");
            var result = new byte[checked((int)length)]; var blocks = new List<uint>(); var used = new HashSet<uint>();
            var table = header; var offset = 0;
            while (table != 0)
            {
                if (table != header) Header(table, 16, header, 0xFFFFFFFD);
                var count = Word(table, 2);
                if (count != Math.Min(72, (result.Length - offset + 511) / 512)) throw new InvalidDataException("Invalid FFS data table");
                for (var i = 0; i < count; i++)
                {
                    var block = Word(table, 77 - i); _ = Word(block, 0);
                    if (!used.Add(block) || metadata.Contains(block)) throw new InvalidDataException("Aliased FFS system data");
                    var take = Math.Min(512, result.Length - offset);
                    bytes.AsSpan(checked((int)block * 512), take).CopyTo(result.AsSpan(offset, take));
                    offset += take; blocks.Add(block);
                }
                table = Word(table, 126);
                if (table != 0 && (offset == result.Length || used.Contains(table))) throw new InvalidDataException("Invalid FFS extension");
            }
            if (offset != result.Length) throw new InvalidDataException("Truncated system file");
            return (result, blocks);
        }
    }
}
