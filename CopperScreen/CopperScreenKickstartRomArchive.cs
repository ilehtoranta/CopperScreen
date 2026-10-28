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
	private static readonly string[] RomExtensions = [".rom", ".bin", ".kick"];

	internal static byte[] ReadNativeRom(string path, CopperScreenKickstartSource source, KickstartVersion version, bool a1200 = false)
	{
		var rom = ReadRomImage(path, source, version);
		if (a1200)
		{
			if (source != CopperScreenKickstartSource.KickstartRom || version != KickstartVersion.Kickstart30 ||
				rom.Length != 524288 || BinaryPrimitives.ReadUInt16BigEndian(rom.AsSpan(12)) != 39 ||
				BinaryPrimitives.ReadUInt16BigEndian(rom.AsSpan(14)) != 106)
				throw new NotSupportedException("Choose an A1200 Kickstart 3.0 ROM (512 KiB, v39.106) for the initial AGA profile.");
			return rom;
		}
		var actual = IdentifyNativeVersion(rom);
		if (actual != version ||
			(source == CopperScreenKickstartSource.Kickstart13Rom && actual != KickstartVersion.Kickstart13) ||
			(source == CopperScreenKickstartSource.Kickstart31Rom && actual != KickstartVersion.Kickstart31))
			throw new NotSupportedException("The ROM does not match the selected Kickstart version. Choose Kickstart 1.3, A500+ Kickstart 2.04 or A500 Kickstart 3.1 in Settings.");
		return rom;
	}

	internal static KickstartVersion IdentifyNativeVersion(ReadOnlySpan<byte> rom)
	{
		if (rom.Length == 262144 && BinaryPrimitives.ReadUInt16BigEndian(rom[12..]) == 34)
			return KickstartVersion.Kickstart13;
		if (rom.Length == 524288 && BinaryPrimitives.ReadUInt16BigEndian(rom[12..]) == 37 &&
			BinaryPrimitives.ReadUInt16BigEndian(rom[14..]) == 175)
			return KickstartVersion.Kickstart20;
		if (rom.Length == 524288 && BinaryPrimitives.ReadUInt16BigEndian(rom[12..]) == 40 &&
			BinaryPrimitives.ReadUInt16BigEndian(rom[14..]) == 63)
			return KickstartVersion.Kickstart31;
		throw new NotSupportedException("Choose native Kickstart 1.3 (256 KiB, v34), A500+ Kickstart 2.04 (512 KiB, v37.175) or A500/A600/A2000 Kickstart 3.1 (512 KiB, v40.63). Other machine ROMs are unavailable.");
	}

	public static byte[] ReadRomImage(
		string path,
		CopperScreenKickstartSource source,
		KickstartVersion version)
	{
		if (!Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
		{
			return File.ReadAllBytes(path);
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
		return rom.ToArray();
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
