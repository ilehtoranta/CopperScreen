using System.Text;

namespace CopperDisk.FileSystem;

public enum HostNameInterpretation { RestoreEncoded, Literal }

/// <summary>Versioned, sidecar-free Amiga/Windows filename mapping.</summary>
public static class HostNameCodec
{
    public const string Prefix = ".copperimage-v1-";
    public static IReadOnlyDictionary<string, string> MapSiblings(IEnumerable<string> names)
    {
        var values = names.ToArray(); var collisions = values.GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).SelectMany(g => g).ToHashSet(StringComparer.Ordinal);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in values)
        {
            var escape = collisions.Contains(name) || name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
            try { DiskTransfer.ValidateName(name); } catch (IOException) { escape = true; }
            if (!result.TryAdd(name, escape ? Encode(name) : name)) throw new InvalidDataException("Duplicate source name.");
        }
        if (result.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != result.Count) throw new InvalidDataException("Host aliases collide.");
        return result;
    }

    public static string Encode(string name)
    {
        ValidateAmiga(name);
        var result = Prefix + Convert.ToHexString(Encoding.Latin1.GetBytes(name));
        var dot = name.LastIndexOf('.'); var extension = dot >= 0 ? name[dot..] : "";
        if (extension.Length is > 1 and <= 17 && extension.AsSpan(1).IndexOfAnyExcept("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789") < 0)
            result += extension;
        DiskTransfer.ValidateName(result); return result;
    }

    public static string Decode(string name, HostNameInterpretation interpretation = HostNameInterpretation.RestoreEncoded)
    {
        if (interpretation == HostNameInterpretation.Literal || !name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return name;
        try
        {
            var payload = name[Prefix.Length..]; var dot = payload.IndexOf('.'); if (dot >= 0) payload = payload[..dot];
            var original = Encoding.Latin1.GetString(Convert.FromHexString(payload)); ValidateAmiga(original);
            if (Encode(original) != name) throw new FormatException("Non-canonical encoded name.");
            return original;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or IOException)
        { throw new InvalidDataException($"Malformed encoded filename: {name}. Choose literal-name import if this is an ordinary host name.", ex); }
    }

    public static IReadOnlyList<(string HostName, string ImageName)> Preview(IEnumerable<string> paths)
    {
        var result = new List<(string, string)>(); var pending = new Stack<string>(paths);
        while (pending.TryPop(out var path))
        {
            DiskImageSession.RequireOrdinarySource(path); var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
            if (name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                string decoded;
                try { decoded = Decode(name); } catch (InvalidDataException) { decoded = "(invalid encoding)"; }
                result.Add((name, decoded));
            }
            if (Directory.Exists(path)) foreach (var child in Directory.EnumerateFileSystemEntries(path)) pending.Push(child);
        }
        return result;
    }

    private static void ValidateAmiga(string name)
    {
        if (name.Length is 0 or > 107 || name is "." or ".." || name.Any(c => c < 32 || c > 255 || c is '/' or ':'))
            throw new InvalidDataException("Invalid Amiga name.");
    }
}
