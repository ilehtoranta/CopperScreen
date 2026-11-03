using CopperScreen;

namespace CopperScreen.Tests;

public sealed class CopperStartA500PlusProbeTests
{
    [Fact]
    public void A500PlusBundledCopperStartReachesDesktop()
    {
        var root = Path.Combine(Path.GetTempPath(), "copperstart-a500plus-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var draft = CopperScreenLaunchWorkflow.SelectStartChoice(new(), 5);
            draft = CopperScreenBundledCopperOs.Prepare(draft, root);
            using var session = Assert.IsType<CopperScreenLightweightSession>(
                CopperScreenSession.Create(draft.ToStartupOptions(AppContext.BaseDirectory)));
            Assert.False(session.Machine.IsAga);
            Assert.Equal(Copper68k.M68kCpuModel.M68000, session.Machine.CpuModel);
            Assert.Equal(1024 * 1024, session.Machine.ChipRam.Length);
            for (var field = 0; field < 2500; field++)
            {
                session.RenderNextFrame(session.Framebuffer);
                Assert.False(session.IsPaused, $"Paused at field {field}, PC={session.Machine.Cpu.ProgramCounter:X8}: {session.FaultMessage}");
                Assert.Null(session.FaultMessage);
                Assert.Null(session.Machine.UnsupportedActiveFeature);
            }
            Assert.Contains(unchecked((int)0xFF6688BB), session.Framebuffer);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
