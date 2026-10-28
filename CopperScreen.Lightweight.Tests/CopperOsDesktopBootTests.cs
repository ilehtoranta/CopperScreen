using System.Buffers.Binary;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class CopperOsDesktopBootTests
{
    [Fact]
    public void BundledA1200BootsNativeDesktop()
    {
        var root = Path.Combine(Path.GetTempPath(), "copperos-desktop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var draft = CopperScreenBundledCopperOs.Prepare(CopperScreenLaunchWorkflow.SelectStartChoice(new(), 3), root);
            using var session = Assert.IsType<CopperScreenLightweightSession>(CopperScreenSession.Create(draft.ToStartupOptions(AppContext.BaseDirectory)));
            Assert.True(session.Machine.IsAga);
            Assert.Equal(Copper68k.M68kCpuModel.M68EC020, session.Machine.CpuModel);
            Assert.False(session.Machine.IsDriveMounted(0));
            uint Long(uint p) => p + 4 <= session.Machine.ChipRam.Length
                ? BinaryPrimitives.ReadUInt32BigEndian(session.Machine.ChipRam.Span.Slice((int)p, 4)) : 0;
            string Name(uint p)
            {
                var bytes = session.Machine.ChipRam.Span;
                if (p == 0 || p >= bytes.Length) return "";
                var end = (int)p;
                while (end < bytes.Length && end - p < 100 && bytes[end] != 0) end++;
                return System.Text.Encoding.ASCII.GetString(bytes[(int)p..end]);
            }
            uint Find(int list, string name)
            {
                var p = Long(Long(4) + (uint)list);
                for (var n = 0; n < 256 && p != 0 && Long(p) != 0; n++, p = Long(p))
                    if (Name(Long(p + 10)) == name) return p;
                return 0;
            }
            var output = Path.GetFullPath("artifacts/desktop-boot-capture");
            Directory.CreateDirectory(output);
            var fields = 0;
            uint Port() => Find(392, "CopperOS.Desktop");
            uint Intuition() => Find(378, "intuition.library");
            uint State() => Long(Long(Port() + 16) + 88);
            uint View() => Long(State() + 168);
            bool Ready()
            {
                var port = Port(); var intuition = Intuition();
                var task = Long(port + 16); var worker = Long(intuition + 244);
                return port != 0 && intuition != 0 && worker != 0 && task + 15 < session.Machine.ChipRam.Length &&
                    session.Machine.ChipRam.Span[(int)task + 15] == 4 &&
                    Long(worker + 108) == 0xFFFF && Long(worker + 112) == 0 && Long(port + 20) == port + 24;
            }
            void Run(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    session.RenderNextFrame(session.Framebuffer); fields++;
                    Assert.Null(session.FaultMessage);
                }
            }
            void Wait(uint view, string label)
            {
                for (var i = 0; i < 12000 && (!Ready() || View() != view); i += 20) Run(20);
                Run(4);
                NativeCapture.WritePng(Path.Combine(output, label + ".png"), session.Framebuffer, session.Width, session.Height);
                Assert.True(Ready() && View() == view, $"Desktop did not finish {label} after {fields} fields, PC={session.Machine.Cpu.ProgramCounter:X8}");
                var screen = Long(Intuition() + 60);
                Assert.NotEqual(0u, screen);
                Assert.NotEqual(0u, Long(screen + 4));
            }
            Wait(0, "desktop");
            Assert.InRange(fields, 1, 850); // Guest-work bound; independent of host speed.
            File.WriteAllText(Path.Combine(output, "boot-timing.json"), System.Text.Json.JsonSerializer.Serialize(new
                { fields, cycle = session.Machine.Cycle, guestSeconds = session.Machine.Cycle / 7093790.0 }));
            var redrawStart = fields;
            var redrawCycle = session.Machine.Cycle;
            session.KeyDown(AmigaRawKey.Tab); Run(4); session.KeyUp(AmigaRawKey.Tab); Run(4);
            bool Redrawn() => Ready() && Long(State() + 28) == 1 && Long(State() + 156) == 1;
            while (!Redrawn() && fields - redrawStart < 40) Run(1);
            Assert.True(Redrawn(), "Tab selection did not finish drawing");
            Assert.InRange(fields - redrawStart, 1, 17); // exec.6 takes 15; redraw.5 took 17, text.4 took 21.
            File.WriteAllText(Path.Combine(output, "redraw-timing.json"), System.Text.Json.JsonSerializer.Serialize(new
                { fields = fields - redrawStart, guestSeconds = (session.Machine.Cycle - redrawCycle) / 7093790.0 }));
            NativeCapture.WritePng(Path.Combine(output, "selected-desktop.png"), session.Framebuffer, session.Width, session.Height);
            session.KeyDown(AmigaRawKey.E); Run(4); session.KeyUp(AmigaRawKey.E); Run(4);
            Wait(2, "editor");
            session.Reset();
            Wait(0, "reset-desktop");
            session.SavePersistentStorage();
            session.Dispose();
            using var reopened = Assert.IsType<CopperScreenLightweightSession>(CopperScreenSession.Create(draft.ToStartupOptions(AppContext.BaseDirectory)));
            for (var i = 0; i < 2000; i++) reopened.RenderNextFrame(reopened.Framebuffer);
            Assert.Null(reopened.FaultMessage);
            Assert.Contains(unchecked((int)0xFF6688BB), reopened.Framebuffer); // Native desktop's blue tile/title pen.
            NativeCapture.WritePng(Path.Combine(output, "reopened-desktop.png"), reopened.Framebuffer, reopened.Width, reopened.Height);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
