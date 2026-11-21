using System.Globalization;
using System.Text;

namespace CopperScreen;

// Each page closes its stream before returning. The manager serializes guest
// reads and owns fault recovery; an open viewer never retains a guest handle.
internal sealed class CopperScreenFileViewSource(string name, string location, long length,
    Func<long, CancellationToken, Stream> open)
{
    internal const int PageSize = 4096;
    internal string Name => name;
    internal string Location => location;
    internal long Length => length;

    internal static CopperScreenFileViewSource Host(string path)
    {
        CopperScreenDiskImage.RequireOrdinarySource(path);
        var info = new FileInfo(path); var size = info.Length; var modified = info.LastWriteTimeUtc;
        return new(info.Name, path, size, (offset, _) =>
        {
            CopperScreenDiskImage.RequireOrdinarySource(path);
            var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                if (input.Length != size || File.GetLastWriteTimeUtc(path) != modified)
                    throw new IOException("The host file changed. Close the viewer and reopen it.");
                input.Position = offset; return input;
            }
            catch { input.Dispose(); throw; }
        });
    }

    internal static CopperScreenFileViewSource Image(DiskImageEntry entry)
    {
        if (entry.IsDirectory || entry.IsLink) throw new NotSupportedException("Select an ordinary file to view.");
        return new(entry.Name, $"{entry.Volume.Owner.FilePath} · {entry.Volume.DeviceName}:{entry.Path}", entry.Size,
            (offset, ct) => entry.Volume.OpenRead(entry, ct, offset));
    }

    internal FileViewPage ReadPage(long offset, CancellationToken ct)
    {
        if (offset < 0 || (length == 0 ? offset != 0 : offset >= length)) throw new ArgumentOutOfRangeException(nameof(offset));
        ct.ThrowIfCancellationRequested();
        var prefix = offset > 0 ? 1 : 0;
        var start = offset - prefix;
        // One preceding byte handles CRLF; four following bytes complete a UTF-8
        // character across a page boundary. Hex always shows exactly the page.
        var bytes = new byte[(int)Math.Min(PageSize + prefix + 4L, length - start)];
        using var input = open(start, ct);
        var total = 0;
        while (total < bytes.Length)
        {
            ct.ThrowIfCancellationRequested();
            var read = input.ReadAsync(bytes.AsMemory(total), ct).AsTask().GetAwaiter().GetResult();
            if (read == 0) throw new EndOfStreamException("The file ended before the requested page. Reopen the viewer.");
            total += read;
        }
        return new(offset, bytes);
    }
}

internal sealed record FileViewPage(long Offset, byte[] Data)
{
    private int Prefix => Offset > 0 ? 1 : 0;
    internal int Count => Math.Min(CopperScreenFileViewSource.PageSize, Math.Max(0, Data.Length - Prefix));
    private static bool Continuation(byte value) => (value & 0xC0) == 0x80;

    private (int Start, int End) TextRange(bool utf8)
    {
        var start = Prefix; var end = Prefix + Count;
        if (utf8)
        {
            if (Offset == 0 && Data.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) start = 3;
            if (Offset > 0) while (start < end && Continuation(Data[start])) start++;
            var limit = Math.Min(Data.Length, end + 3);
            while (end < limit && Continuation(Data[end])) end++;
        }
        if (Prefix != 0 && start < end && Data[0] == 13 && Data[start] == 10) start++;
        if (end > start && end < Data.Length && Data[end - 1] == 13 && Data[end] == 10) end++;
        return (start, end);
    }

    internal bool IsUtf8()
    {
        var (start, end) = TextRange(true);
        try { _ = new UTF8Encoding(false, true).GetCharCount(Data.AsSpan(start, end - start)); return true; }
        catch (DecoderFallbackException) { return false; }
    }

    internal string Text(bool utf8)
    {
        var (start, end) = TextRange(utf8);
        var text = (utf8 ? Encoding.UTF8 : Encoding.Latin1).GetString(Data, start, end - start).Replace("\r\n", "\n").Replace('\r', '\n');
        var result = new StringBuilder(text.Length);
        foreach (var c in text)
            result.Append(c is '\t' or '\n' || char.GetUnicodeCategory(c) is not (UnicodeCategory.Control or UnicodeCategory.Format) ? c : '·');
        return result.ToString();
    }

    internal string Hex(bool wideOffsets)
    {
        var result = new StringBuilder(Count * 5);
        for (var row = 0; row < Count; row += 16)
        {
            result.Append((Offset + row).ToString(wideOffsets ? "X16" : "X8", CultureInfo.InvariantCulture)).Append("  ");
            var count = Math.Min(16, Count - row);
            for (var column = 0; column < 16; column++)
            {
                if (column == 8) result.Append(' ');
                if (column < count) result.Append(Data[Prefix + row + column].ToString("X2", CultureInfo.InvariantCulture)).Append(' ');
                else result.Append("   ");
            }
            result.Append(" | ");
            for (var column = 0; column < count; column++)
            {
                var value = Data[Prefix + row + column]; result.Append(value is >= 32 and <= 126 ? (char)value : '.');
            }
            result.Append('\n');
        }
        return result.ToString();
    }
}
