using System.Security.Cryptography;
using System.Text.Json;
using CopperScreen;

namespace CopperScreen.Tests;

// Optional original-media replay. Synthetic boot-service coverage does not depend on local media.
public sealed class MinimalBootNativeReplayTests
{
    [MinimalBootReplayTheory]
    [InlineData(1)]
    [InlineData(2)]
    public void OriginalMediaRunsOnExtendedModel(int model)
    {
        var path = Environment.GetEnvironmentVariable("COPPERSCREEN_MINIMAL_BOOT_ADF")!;
        var output = Path.Combine(Path.GetFullPath(Environment.GetEnvironmentVariable("COPPERSCREEN_MINIMAL_BOOT_OUTPUT")
            ?? Path.Combine(Path.GetTempPath(), "minimal-boot-replay-" + Guid.NewGuid().ToString("N"))), "model-" + model);
        Directory.CreateDirectory(output);
        var draft = CopperScreenLaunchWorkflow.SelectStartChoice(new(), 6 + model);
        draft.DriveDiskPaths[0] = path;
        var disk = CopperScreenDiskImageArchive.LoadDiskImage(path);
        var firmware = new CopperScreenMinimalBoot(disk, false, draft.ChipRamKb * 1024, model == 2);
        using var session = new CopperScreenLightweightSession(draft.ToStartupOptions(AppContext.BaseDirectory));
        var frames = 0; var audibleFrames = 0;
        var checkpoints = new List<object>();
        string? failure = null;
        void Capture()
        {
            var capture = Path.Combine(output, $"frame-{frames:D5}.png");
            NativeCapture.WritePng(capture, session.Framebuffer, session.Width, session.Height);
            checkpoints.Add(new { frames, cycle = session.Machine.Cycle, pc = session.Machine.Cpu.ProgramCounter.ToString("X8"),
                colors = session.Framebuffer.Distinct().Count(), audibleFrames, capture });
        }
        try
        {
            for (; frames < 3000;)
            {
                session.RenderNextFrame(session.Framebuffer); frames++;
                foreach (var sample in session.Machine.AudioSamples.Span)
                    if (sample != 0) { audibleFrames++; break; }
                Assert.Null(session.Machine.UnsupportedActiveFeature);
                if (frames is 300 or 1000 or 2000 or 3000) Capture();
            }
        }
        catch (Exception ex) { failure = ex.ToString(); Capture(); }
        File.WriteAllText(Path.Combine(output, "replay.json"), JsonSerializer.Serialize(new
        {
            model = CopperScreenMachinePresets.Label((CopperScreenMachineModel)model),
            cpu = session.Machine.CpuModel.ToString(), chipRamBytes = session.Machine.ChipRam.Length,
            path, mediaSha256 = Convert.ToHexString(SHA256.HashData(disk.Data)),
            firmwareSha256 = Convert.ToHexString(SHA256.HashData(firmware.Rom)), frames, audibleFrames,
            failure, checkpoints, scope = "3000 fields of original-media startup; no gameplay or performance acceptance"
        }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Null(failure);
        Assert.Equal(3000, frames);
        Assert.True(audibleFrames > 0, "No non-silent guest audio was observed.");
        Assert.True(session.Framebuffer.Distinct().Count() > 2, "No multicolour guest output was observed.");
    }
}

public sealed class MinimalBootReplayTheoryAttribute : TheoryAttribute
{
    public MinimalBootReplayTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COPPERSCREEN_MINIMAL_BOOT_ADF")))
            Skip = "Supply COPPERSCREEN_MINIMAL_BOOT_ADF for original-media boot coverage.";
    }
}
