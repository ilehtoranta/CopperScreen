namespace CopperDisk.Tests;

public sealed class DmsArchiveWriteTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "CopperDmsWrite-" + Guid.NewGuid().ToString("N"));
    public DmsArchiveWriteTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);

    [Fact]
    public void ReplacementPreservesHeaderMetadataAndRoundTripsMixedStoredAndRleTracks()
    {
        var original = DmsFixture.Create(new byte[901120], 0);
        for (var i = 28; i < 46; i++) original[i] = (byte)(i * 3);
        original[12] = 0x12; original[15] = 0x34; DmsFixture.SealHeader(original);
        var path = Path.Combine(root, "Source.dms"); File.WriteAllBytes(path, original);
        var sectors = new byte[901120]; new Random(12345).NextBytes(sectors.AsSpan(0, 11264));
        var at = 11264;
        foreach (var count in new[] { 1, 2, 3, 4, 254, 255, 256 })
        { sectors.AsSpan(at, count).Fill(0x90); at += count; sectors[at++] = 0x33; }
        using (var image = AmigaSectorImage.OpenDms(path))
        using (var replacement = new MemoryStream())
        {
            image.WriteDmsReplacement(new MemoryStream(sectors), replacement);
            var bytes = replacement.ToArray(); Assert.True(bytes.Length < original.Length);
            Assert.Equal(0, bytes[56 + 13]); Assert.Equal(1, bytes[56 + 20 + 11264 + 13]);
            for (var i = 0; i < 56; i++)
                if (i is not (>= 21 and <= 23 or >= 25 and <= 27 or >= 52 and <= 55)) Assert.Equal(original[i], bytes[i]);
            Assert.Equal(sectors, DmsDecoder.Decode(bytes));
            var output = Environment.GetEnvironmentVariable("COPPER_DMS_REFERENCE_OUTPUT");
            if (!string.IsNullOrEmpty(output))
            {
                Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output, "Mixed.dms"), bytes);
                File.WriteAllBytes(Path.Combine(output, "Mixed.adf"), sectors);
            }
        }
        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void ExistingCompressionModesCanBeRepackedAsStoredAndSimple(byte mode)
    {
        var original = AmigaDiskLoaderTests.CreateDms(new byte[901120], mode);
        var path = Path.Combine(root, "Compressed.dms"); File.WriteAllBytes(path, original);
        using var image = AmigaSectorImage.OpenDms(path); Assert.Null(image.WriteUnavailableReason);
        var modified = new byte[901120]; modified[12345] = 0x90; modified[^1] = 0x33;
        using var output = new MemoryStream(); image.WriteDmsReplacement(new MemoryStream(modified), output);
        Assert.Equal(modified, DmsDecoder.Decode(output.ToArray()));
        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("auxiliary")] [InlineData("trailing")] [InlineData("flags")] [InlineData("trackMetadata")]
    public void UnsupportedArchiveMetadataRemainsReadableButCannotBeDiscardedBySaving(string kind)
    {
        var original = DmsFixture.Create(new byte[901120]);
        if (kind == "auxiliary") original = DmsFixture.WithAuxiliary(original);
        if (kind == "trailing") original = original.Concat(new byte[5]).ToArray();
        if (kind == "flags") { original[10] = 2; DmsFixture.SealHeader(original); }
        if (kind == "trackMetadata") { original[60] = 1; var crc = DmsDecoder.CreateCrc(original.AsSpan(56, 18)); original[74] = (byte)(crc >> 8); original[75] = (byte)crc; }
        var path = Path.Combine(root, "Metadata.dms"); File.WriteAllBytes(path, original);
        using var image = AmigaSectorImage.OpenDms(path); Assert.NotNull(image.WriteUnavailableReason);
        var sectors = new byte[901120]; image.Volumes[0].Read(0, sectors); Assert.All(sectors, b => Assert.Equal(0, b));
        using var output = new MemoryStream();
        Assert.Throws<NotSupportedException>(() => image.WriteDmsReplacement(new MemoryStream(sectors), output));
        Assert.Empty(output.ToArray()); Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Fact]
    public void CancellationWriteErrorsAndReadbackCorruptionCannotProduceAVerifiedReplacement()
    {
        var original = DmsFixture.Create(new byte[901120]);
        var path = Path.Combine(root, "Source.dms"); File.WriteAllBytes(path, original);
        using var image = AmigaSectorImage.OpenDms(path);
        using var sectors = new MemoryStream(new byte[901120]);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        using var empty = new MemoryStream();
        Assert.ThrowsAny<OperationCanceledException>(() => image.WriteDmsReplacement(sectors, empty, cancelled.Token));
        Assert.Empty(empty.ToArray());
        using var corrupt = new DamagedStage();
        Assert.Throws<InvalidDataException>(() => image.WriteDmsReplacement(sectors, corrupt));
        using var failed = new DamagedStage { FailWrite = true };
        Assert.Throws<IOException>(() => image.WriteDmsReplacement(sectors, failed));
        using var duringWrite = new CancellationTokenSource();
        using var cancelledStage = new CancelAfterWrite(duringWrite);
        Assert.ThrowsAny<OperationCanceledException>(() => image.WriteDmsReplacement(sectors, cancelledStage, duringWrite.Token));
        Assert.Throws<InvalidDataException>(() => image.WriteDmsReplacement(new MemoryStream(new byte[901121]), empty));
        using var nonempty = new MemoryStream([1]);
        Assert.Throws<ArgumentException>(() => image.WriteDmsReplacement(sectors, nonempty));
        Assert.Equal(original, File.ReadAllBytes(path));
    }
    private sealed class DamagedStage : MemoryStream
    {
        internal bool FailWrite;
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
            => FailWrite ? ValueTask.FromException(new IOException("Injected staging write failure.")) : base.WriteAsync(buffer, ct);
        public override int Read(Span<byte> buffer)
        { var count = base.Read(buffer); if (count != 0) buffer[0] ^= 1; return count; }
    }
    private sealed class CancelAfterWrite(CancellationTokenSource source) : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        { base.Write(buffer.Span); source.Cancel(); return ValueTask.CompletedTask; }
    }
}
