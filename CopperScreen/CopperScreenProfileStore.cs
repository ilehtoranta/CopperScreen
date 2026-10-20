using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CopperScreen;

internal static partial class CopperScreenProfileStore
{
    public static string GetUserProfilesDirectory(string? userDataDirectory = null)
        => Path.Combine(CopperScreenUserData.GetDirectory(userDataDirectory), "Profiles");

    public static string FindBundledPresetsDirectory(string baseDirectory)
        => FindDirectory(baseDirectory, "Presets") ?? Path.Combine(Path.GetFullPath(baseDirectory), "Presets");

    private static string? FindDirectory(string baseDirectory, string name)
    {
        for (var directory = new DirectoryInfo(baseDirectory); directory != null; directory = directory.Parent)
        {
            foreach (var path in new[] { Path.Combine(directory.FullName, name), Path.Combine(directory.FullName, "CopperScreen", name) })
                if (Directory.Exists(path)) return path;
        }
        return null;
    }

    public static IReadOnlyList<CopperScreenProfileSummary> ListProfiles(string baseDirectory, string? userDataDirectory = null)
        => ListProfiles(baseDirectory, out _, userDataDirectory);

    public static IReadOnlyList<CopperScreenProfileSummary> ListProfiles(string baseDirectory, out string? error, string? userDataDirectory = null)
    {
        var profiles = new List<CopperScreenProfileSummary>();
        var errors = new List<string>();
        AddDirectory(GetUserProfilesDirectory(userDataDirectory), bundled: false);
        AddDirectory(FindBundledPresetsDirectory(baseDirectory), bundled: true);
        if (profiles.Count == 0)
        {
            var fallback = CopperScreenProfile.CreateFallbackDefault();
            profiles.Add(new(fallback.Id, fallback.DisplayName, fallback.Id, IsBundled: true));
        }
        error = errors.Count == 0 ? null : string.Join(Environment.NewLine, errors);
        return profiles;

        void AddDirectory(string directory, bool bundled)
        {
            try
            {
                foreach (var path in Directory.GetFiles(directory, "*.json").Order(StringComparer.OrdinalIgnoreCase))
                {
                    if (CopperScreenProfile.TryLoad(path, baseDirectory, out var profile, out var loadError, userDataDirectory))
                    {
                        var unavailable = CopperScreenAvailability.GetUnavailableReason(CopperScreenSettingsDraft.FromProfile(profile), baseDirectory);
                        profiles.Add(new(profile.Id, profile.DisplayName, path, unavailable, bundled));
                    }
                    else errors.Add(loadError ?? $"Could not read profile '{path}'.");
                }
            }
            catch (DirectoryNotFoundException) when (!File.Exists(directory) && !File.Exists(Path.GetDirectoryName(directory))) { }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { errors.Add($"Could not read profiles in '{directory}': {ex.Message}"); }
        }
    }

    public static string Save(CopperScreenSettingsDraft draft, string baseDirectory, string? userDataDirectory = null)
    {
        var id = ValidateFileId(draft.Id);
        var copy = draft.Clone();
        copy.Id = id;
        var path = Path.Combine(GetUserProfilesDirectory(userDataDirectory), id + ".json");
        SaveToPath(copy, path, baseDirectory: baseDirectory);
        draft.Id = id;
        draft.SourcePath = path;
        return path;
    }

    internal static void SaveToPath(CopperScreenSettingsDraft draft, string path, bool overwrite = true, string? baseDirectory = null)
    {
        CopperScreenUserData.WriteAtomically(path, Serialize(draft, baseDirectory), overwrite);
    }

    internal static string Serialize(CopperScreenSettingsDraft draft, string? baseDirectory = null)
    {
        _ = draft.ToProfile();
        var json = JsonNode.Parse(JsonSerializer.Serialize(ProfileFile.FromDraft(draft), WriteJsonOptions),
            new JsonNodeOptions { PropertyNameCaseInsensitive = true })!.AsObject();
        if (baseDirectory != null) ResolvePaths(json, baseDirectory);
        return json.ToJsonString(WriteJsonOptions);
    }

    public static string SaveAs(CopperScreenSettingsDraft draft, string baseDirectory, string? userDataDirectory = null)
    {
        var baseId = ValidateFileId(draft.Id);
        var profilesDirectory = GetUserProfilesDirectory(userDataDirectory);
        var used = ListProfiles(baseDirectory, userDataDirectory).Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var suffix = 1; ; suffix++)
        {
            var id = suffix == 1 && baseId.EndsWith("-copy", StringComparison.OrdinalIgnoreCase) ? baseId
                : baseId + "-copy" + (suffix == 1 ? "" : "-" + suffix);
            var path = Path.Combine(profilesDirectory, id + ".json");
            if (used.Contains(id) || File.Exists(path)) continue;
            var copy = draft.Clone();
            copy.Id = id;
            if (!copy.DisplayName.EndsWith(" Copy", StringComparison.OrdinalIgnoreCase)) copy.DisplayName += " Copy";
            try { SaveToPath(copy, path, overwrite: false, baseDirectory); }
            catch (IOException) when (File.Exists(path)) { continue; }
            draft.Id = id;
            draft.DisplayName = copy.DisplayName;
            draft.SourcePath = path;
            return path;
        }
    }

    internal static string ValidateFileId(string value)
    {
        var id = CopperScreenProfile.NormalizeProfileId(value);
        var device = id.Split('.')[0];
        if (string.IsNullOrWhiteSpace(id) || id is "." or ".." || id.EndsWith('.') ||
            id.IndexOfAny(['/', '\\', ':', '<', '>', '"', '|', '?', '*']) >= 0 || id.Any(char.IsControl) ||
            device.Equals("CON", StringComparison.OrdinalIgnoreCase) || device.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            device.Equals("AUX", StringComparison.OrdinalIgnoreCase) || device.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            (device.Length == 4 && (device.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || device.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && device[3] is >= '1' and <= '9'))
            throw new ArgumentException("Choose a profile file name without folders, reserved names or special file-name characters.");
        if (id.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) id = id[..^5];
        if (string.IsNullOrWhiteSpace(id) || id is "." or ".." || id.EndsWith('.'))
            throw new ArgumentException("A profile file name is required.");
        return id;
    }

    // Copy once per legacy installation, never deleting sources or replacing user data.
    // A marker is written only after all files succeed, so interrupted imports can retry.
    public static string? MigrateLegacyProfiles(string baseDirectory, string? userDataDirectory = null)
    {
        var legacy = FindDirectory(baseDirectory, "Profiles");
        if (legacy == null || string.Equals(legacy, GetUserProfilesDirectory(userDataDirectory), StringComparison.OrdinalIgnoreCase)) return null;
        var sourceId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(legacy.ToUpperInvariant())))[..16];
        var marker = Path.Combine(CopperScreenUserData.GetDirectory(userDataDirectory), "Migrations", "profiles-" + sourceId + ".json");
        if (File.Exists(marker)) return null;
        var errors = new List<string>();
        try
        {
            foreach (var source in Directory.GetFiles(legacy, "*.json").Order(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var original = File.ReadAllText(source);
                    var preset = Path.Combine(FindBundledPresetsDirectory(baseDirectory), Path.GetFileName(source));
                    if (File.Exists(preset) && original == File.ReadAllText(preset)) continue;
                    if (!CopperScreenProfile.TryLoad(source, baseDirectory, out var profile, out var error, userDataDirectory))
                        throw new InvalidOperationException(error);
                    var id = ValidateFileId(profile.Id);
                    var json = JsonNode.Parse(original, new JsonNodeOptions { PropertyNameCaseInsensitive = true },
                        new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip })!.AsObject();
                    ResolvePaths(json, baseDirectory);
                    json["id"] = id;
                    var destination = Path.Combine(GetUserProfilesDirectory(userDataDirectory), id + ".json");
                    var contents = json.ToJsonString(WriteJsonOptions);
                    if (File.Exists(destination))
                    {
                        if (File.ReadAllText(destination) == contents) continue;
                        // Preserve collisions separately, including edits made after a partial import.
                        id += "-imported-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(source).ToUpperInvariant())))[..12].ToLowerInvariant();
                        json["id"] = id;
                        json["displayName"] = profile.DisplayName + " (imported)";
                        destination = Path.Combine(GetUserProfilesDirectory(userDataDirectory), id + ".json");
                        contents = json.ToJsonString(WriteJsonOptions);
                        if (File.Exists(destination)) continue;
                    }
                    CopperScreenUserData.WriteAtomically(destination, contents, overwrite: false);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or OverflowException or FormatException)
                { errors.Add($"Could not import '{source}': {ex.Message} The original has been kept; fix it and restart to retry."); }
            }
            if (errors.Count == 0)
                CopperScreenUserData.WriteAtomically(marker, JsonSerializer.Serialize(new { Source = legacy, Version = 1 }), overwrite: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { errors.Add($"Could not import profiles from '{legacy}': {ex.Message} Check your user-data folder permissions and restart to retry."); }
        return errors.Count == 0 ? null : string.Join(Environment.NewLine, errors);
    }

    private static void ResolvePaths(JsonObject json, string baseDirectory)
    {
        Resolve(json["kickstart"], "path");
        if (json["media"]?["drives"] is JsonArray drives)
            foreach (var drive in drives) Resolve(drive, "diskPath");
        if (json["hardDrives"] is JsonArray hardDrives)
            foreach (var drive in hardDrives) Resolve(drive, "path");
        if (json["hostDirectories"] is JsonArray folders)
            foreach (var folder in folders) Resolve(folder, "path");
        var sounds = json["audio"]?["floppyDriveSounds"];
        if (sounds?["soundPack"]?.GetValue<string>() is { } pack && !string.Equals(pack, "default", StringComparison.OrdinalIgnoreCase))
            Resolve(sounds, "soundPack");

        void Resolve(JsonNode? node, string property)
        {
            if (node?[property]?.GetValue<string>() is { Length: > 0 } path)
                node[property] = CopperScreenStartupOptions.ResolveOptionalPath(path, baseDirectory);
        }
    }
}
