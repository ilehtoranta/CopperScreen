using System.Security.Cryptography;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class CopperOsLayerBridgeUpgradeTests
{
    [CopperOsSeedReplayFact("COPPERSCREEN_REDRAW5_HDF", "redraw.5")]
    public void RedrawFiveUpgradesExactlyWithBackupAndPreservesUnrelatedBytes()
    {
        var previous = File.ReadAllBytes(Environment.GetEnvironmentVariable("COPPERSCREEN_REDRAW5_HDF")!);
        Assert.Equal("EA6CC800F51832D41DADAA35B881214080617E7EF9F01644B1D371E3F652E5DC", Hash(previous));
        CheckUpgrade(previous);
    }

    [CopperOsSeedReplayFact("COPPERSCREEN_LAYER_BRIDGE6_HDF", "layer-bridge.6")]
    public void LayerBridgeSixUpgradesExactlyWithBackupAndPreservesUnrelatedBytes()
    {
        var previous = File.ReadAllBytes(Environment.GetEnvironmentVariable("COPPERSCREEN_LAYER_BRIDGE6_HDF")!);
        Assert.Equal("DE07A5D313E42C90013ED230C45CD907179E0598819EF900E86E5BE564CA33B6", Hash(previous));
        CheckUpgrade(previous);
    }

    [CopperOsSeedReplayFact("COPPERSCREEN_LAYER_RESULT7_HDF", "layer-result.7")]
    public void LayerResultSevenUpgradesExactlyWithBackupAndPreservesUnrelatedBytes()
    {
        var previous = File.ReadAllBytes(Environment.GetEnvironmentVariable("COPPERSCREEN_LAYER_RESULT7_HDF")!);
        Assert.Equal("E730A9F4DB97D450B4288DE20FB1D21658226530141293987B2A02C00E432ECC", Hash(previous));
        CheckUpgrade(previous);
    }

    [CopperOsSeedReplayFact("COPPERSCREEN_LAYER_ADMISSION8_HDF", "layer-admission.8")]
    public void LayerAdmissionEightUpgradesExactlyWithBackupAndPreservesUnrelatedBytes()
    {
        var previous = File.ReadAllBytes(Environment.GetEnvironmentVariable("COPPERSCREEN_LAYER_ADMISSION8_HDF")!);
        Assert.Equal("99BD9C60C27B474ADB42A3A1BDC786FD88B00E74D73CF8D96257058D05F8772D", Hash(previous));
        CheckUpgrade(previous);
    }

    [CopperOsSeedReplayFact("COPPERSCREEN_LAYER_LOCK9_HDF", "layer-lock.9")]
    public void LayerLockNineUpgradesExactlyWithBackupAndPreservesUnrelatedBytes()
    {
        var previous = File.ReadAllBytes(Environment.GetEnvironmentVariable("COPPERSCREEN_LAYER_LOCK9_HDF")!);
        Assert.Equal("9CC2927C8647FEA4A571B21C0805AF25DB4AAEF13508EAF5A1D068E1EF0A3FF5", Hash(previous));
        CheckUpgrade(previous);
    }

    private static void CheckUpgrade(byte[] previous)
    {
        using var stream = typeof(CopperScreenBundledCopperOs).Assembly.GetManifestResourceStream("CopperScreen.Bundled.CopperOS.hdf")!;
        using var output = new MemoryStream(); stream.CopyTo(output); var current = output.ToArray();
        var home = CopperScreenCopperOsSystemUpdate.ReadHome(current);
        var command = CopperScreenCopperOsSystemUpdate.ReadCommand(current);
        var gui = CopperScreenCopperOsSystemUpdate.ReadGui(current);
        Assert.True(CopperScreenCopperOsSystemUpdate.TryCreateSystemImage(previous, home, command, gui, out var clean));
        Assert.NotNull(clean); Assert.Equal(current, clean);

        // Preserve a user edit in an unrelated startup comment and a free block.
        var comment = previous.AsSpan().IndexOf("; CopperOS native desktop"u8);
        Assert.True(comment >= 0); previous[comment + 2] = (byte)'c'; previous[^10] = 0x5A;
        var expected = (byte[])current.Clone(); expected[comment + 2] = (byte)'c'; expected[^10] = 0x5A;
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath());
        var directory = Path.GetFullPath(Path.Combine(temporaryRoot, "copperos-layer-upgrade-" + Guid.NewGuid().ToString("N")));
        Assert.StartsWith(temporaryRoot, directory, StringComparison.OrdinalIgnoreCase);
        Directory.CreateDirectory(directory); var path = Path.Combine(directory, "CopperOS.hdf");
        try
        {
            File.WriteAllBytes(path, previous);
            using (var active = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                Assert.False(CopperScreenCopperOsSystemUpdate.TryUpgrade(path, home, command, gui));
            }
            Assert.Equal(previous, File.ReadAllBytes(path));
            Assert.True(CopperScreenCopperOsSystemUpdate.TryUpgrade(path, home, command, gui));
            Assert.Equal(expected, File.ReadAllBytes(path));
            var backup = Assert.Single(Directory.GetFiles(directory, "CopperOS.before-drawing-*.hdf"));
            Assert.Equal(previous, File.ReadAllBytes(backup));
            Assert.True(CopperScreenCopperOsSystemUpdate.TryUpgrade(path, home, command, gui));
            Assert.Single(Directory.GetFiles(directory, "CopperOS.before-drawing-*.hdf"));

            var custom = (byte[])previous.Clone();
            var oldGui = CopperScreenCopperOsSystemUpdate.ReadGui(custom);
            var guiAt = custom.AsSpan().IndexOf(oldGui.AsSpan(0, 64)); Assert.True(guiAt >= 0); custom[guiAt + 64] ^= 1;
            File.WriteAllBytes(path, custom);
            Assert.False(CopperScreenCopperOsSystemUpdate.TryUpgrade(path, home, command, gui));
            Assert.Equal(custom, File.ReadAllBytes(path));
            Assert.Single(Directory.GetFiles(directory, "CopperOS.before-drawing-*.hdf"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}

public sealed class CopperOsSeedReplayFactAttribute : FactAttribute
{
    public CopperOsSeedReplayFactAttribute(string environmentVariable, string seed)
    {
        if (!File.Exists(Environment.GetEnvironmentVariable(environmentVariable)))
            Skip = $"Set {environmentVariable} to the frozen {seed} HDF for this migration replay.";
    }
}
