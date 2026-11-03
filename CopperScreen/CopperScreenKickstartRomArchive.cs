/*
 * Copyright (C) 2026 Ilkka Lehtoranta
 * SPDX-License-Identifier: MIT
 */

using System.IO.Compression;
using System.Buffers.Binary;

namespace CopperScreen;

internal static class CopperScreenKickstartRomArchive
{
	private const long MaximumRomImageSize = 16 * 1024 * 1024;
	private const int MaximumRomKeySize = 512 * 1024;
	private const string EncodedRomHeader = "AMIROMTYPE1";
	private static readonly string[] RomExtensions = [".rom", ".bin", ".kick"];

	internal static byte[] ReadNativeRom(string path, KickstartVersion versionHint)
	{
		var rom = ReadRomImage(path, CopperScreenKickstartSource.KickstartRom, versionHint);
		_ = IdentifyNativeVersion(rom);
		return rom;
	}

	internal static byte[] ReadNativeRom(string path, CopperScreenKickstartSource source, KickstartVersion versionHint, bool a1200 = false)
		=> ReadNativeRom(path, versionHint);

	internal static KickstartVersion IdentifyNativeVersion(ReadOnlySpan<byte> rom)
	{
		if (rom.Length == 262144 && BinaryPrimitives.ReadUInt16BigEndian(rom[12..]) == 34)
			return KickstartVersion.Kickstart13;
		var major = rom.Length >= 16 ? BinaryPrimitives.ReadUInt16BigEndian(rom[12..]) : 0;
		if (rom.Length == 524288 && major == 37)
			return KickstartVersion.Kickstart20;
		if (rom.Length == 524288 && major == 39)
			return KickstartVersion.Kickstart30;
		if (rom.Length == 524288 && major == 40)
			return KickstartVersion.Kickstart31;
		throw new NotSupportedException("Choose a supported native Kickstart ROM: version 1.3, 2.x, 3.0 or 3.1. Other Kickstart releases are not yet recognized.");
	}

	public static byte[] ReadRomImage(
		string path,
		CopperScreenKickstartSource source,
		KickstartVersion version)
	{
		if (!Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
		{
			return DecodeCloantoRomIfNeeded(path, File.ReadAllBytes(path));
		}

		using var archive = ZipFile.OpenRead(path);
		var candidates = archive.Entries
			.Where(entry => !string.IsNullOrEmpty(entry.Name) && IsRomEntry(entry))
			.ToArray();
		if (candidates.Length == 0)
		{
			throw new InvalidOperationException("ZIP archive does not contain a Kickstart ROM image (.rom, .bin, or .kick).");
		}

		var selected = SelectEntry(candidates, source, version);
		if (selected == null)
		{
			throw new InvalidOperationException(
				"ZIP archive contains multiple equally suitable Kickstart ROM images; use an archive with one ROM image or pass an extracted ROM.");
		}

		if (selected.Length > MaximumRomImageSize)
		{
			throw new InvalidOperationException($"Kickstart ROM entry '{selected.FullName}' is too large ({selected.Length} bytes).");
		}

		using var input = selected.Open();
		using var rom = new MemoryStream(checked((int)selected.Length));
		input.CopyTo(rom);
		return DecodeCloantoRomIfNeeded(path, rom.ToArray());
	}

	private static byte[] DecodeCloantoRomIfNeeded(string romPath, byte[] image)
	{
		var header = System.Text.Encoding.ASCII.GetBytes(EncodedRomHeader);
		if (image.Length < header.Length || !image.AsSpan(0, header.Length).SequenceEqual(header))
			return image;

		var directory = Path.GetDirectoryName(Path.GetFullPath(romPath))!;
		var keyPath = Path.Combine(directory, "rom.key");
		if (!File.Exists(keyPath))
			throw new InvalidDataException("This ROM is Amiga Forever encoded (AMIROMTYPE1). Put its rom.key file in the same folder, then select the ROM again.");

		var keyLength = new FileInfo(keyPath).Length;
		if (keyLength is <= 0 or > MaximumRomKeySize)
			throw new InvalidDataException("The rom.key file beside this encoded ROM is empty or too large.");

		var key = File.ReadAllBytes(keyPath);
		if (image.Length - header.Length > MaximumRomImageSize)
			throw new InvalidDataException("The encoded Kickstart ROM is too large.");

		var decoded = image.AsSpan(header.Length).ToArray();
		for (var i = 0; i < decoded.Length; i++)
			decoded[i] ^= key[i % key.Length];

		return decoded;
	}

	private static ZipArchiveEntry? SelectEntry(
		IReadOnlyList<ZipArchiveEntry> entries,
		CopperScreenKickstartSource source,
		KickstartVersion version)
	{
		var ranked = entries
			.Select(entry => (Entry: entry, Score: GetScore(entry, source, version)))
			.OrderByDescending(item => item.Score)
			.ThenBy(item => item.Entry.FullName, StringComparer.OrdinalIgnoreCase)
			.ToArray();

		return ranked.Length == 1 || ranked[0].Score > ranked[1].Score
			? ranked[0].Entry
			: null;
	}

	private static int GetScore(
		ZipArchiveEntry entry,
		CopperScreenKickstartSource source,
		KickstartVersion version)
	{
		var name = entry.FullName.Replace('\\', '/');
		var fileName = Path.GetFileNameWithoutExtension(name);
		var normalized = fileName.Replace("-", string.Empty).Replace("_", string.Empty).Replace(".", string.Empty);
		var score = 0;

		if (Path.GetExtension(entry.Name).Equals(".rom", StringComparison.OrdinalIgnoreCase))
		{
			score += 10;
		}

		if (normalized.Contains("kickstart", StringComparison.OrdinalIgnoreCase) ||
			normalized.Contains("kick", StringComparison.OrdinalIgnoreCase))
		{
			score += 20;
		}

		if (source == CopperScreenKickstartSource.DiagRom &&
			normalized.Contains("diagrom", StringComparison.OrdinalIgnoreCase))
		{
			score += 100;
		}

		var versionToken = version switch
		{
			KickstartVersion.Kickstart13 => new[] { "13", "130", "1.3" },
			KickstartVersion.Kickstart20 => new[] { "204", "2.04", "37.175", "20", "200", "2.0" },
			KickstartVersion.Kickstart30 => new[] { "30", "300", "3.0" },
			KickstartVersion.Kickstart31 => new[] { "31", "310", "3.1" },
			_ => Array.Empty<string>()
		};
		if (versionToken.Any(token => fileName.Contains(token, StringComparison.OrdinalIgnoreCase)))
		{
			score += 50;
		}
		// Prefer the supported A500+ release over another 2.0x ROM in a collection.
		// The selected bytes still have to pass the version/revision validation.
		if (version == KickstartVersion.Kickstart20 &&
			(normalized.Contains("204", StringComparison.OrdinalIgnoreCase) || normalized.Contains("37175", StringComparison.OrdinalIgnoreCase)))
			score += 50;

		return score;
	}

	private static bool IsRomEntry(ZipArchiveEntry entry)
		=> RomExtensions.Contains(Path.GetExtension(entry.Name), StringComparer.OrdinalIgnoreCase);
}
