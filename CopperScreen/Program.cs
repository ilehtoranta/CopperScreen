using System.Buffers.Binary;
using System.Runtime;
using System.Diagnostics;
using Avalonia;

namespace CopperScreen;

internal static class Program
{
	public static string[] StartupArgs { get; private set; } = Array.Empty<string>();
	internal static string? DesktopSmokeDataDirectory { get; private set; }

	[STAThread]
	public static void Main(string[] args)
	{
		StartupArgs = args ?? Array.Empty<string>();
		if (StartupArgs is ["--desktop-smoke", var directory, .. var smokeOptions])
		{
			DesktopSmokeDataDirectory = Path.GetFullPath(directory);
			if (smokeOptions is ["--boot-mini"])
				VerifyBundledMiniBoot(DesktopSmokeDataDirectory);
			else if (smokeOptions.Length != 0)
				throw new ArgumentException("Usage: --desktop-smoke <data directory> [--boot-mini]");
			StartupArgs = [];
			RunDesktop(DesktopSmokeDataDirectory);
			return;
		}
#if COPPERSCREEN_INSTALLED_TESTS
        if (StartupArgs.FirstOrDefault() == "--installed-rdb")
        {
            CopperScreenInstalledRdbProof.Run(StartupArgs, (session, launch) =>
            {
                StartupArgs = launch;
                RunDesktop(session.DataDirectory);
            });
            return;
        }
        if (StartupArgs.FirstOrDefault() == "--installed-soak")
        {
            CopperScreenInstalledSoak.Run(StartupArgs, (session, launch) =>
            {
                StartupArgs = launch;
                CopperScreenInstalledSoak.ActiveSession = session;
                RunDesktop(session.DataDirectory);
            });
            return;
        }
		if (StartupArgs.FirstOrDefault() == "--installed-test")
		{
			CopperScreenInstalledProbe.Run(StartupArgs, session =>
			{
				StartupArgs = [];
				CopperScreenInstalledProbe.ActiveSession = session;
				RunDesktop(session.DataDirectory);
			});
			return;
		}
#endif
		RunDesktop();
	}

	private static void VerifyBundledMiniBoot(string dataDirectory)
	{
		// The pinned Mini ROM reports its boot screen in guest Chip RAM.
		const int screenStateAddress = 0x74c;
		const uint splash = 6, waitingForDisk = 1;
		var options = CopperScreenStartupOptions.Parse(["--minimal-disk-boot"], AppContext.BaseDirectory,
			userDataDirectory: dataDirectory);
		using var session = new CopperScreenLightweightSession(options);
		var sawSplash = false;
		for (var field = 0; field < 300; field++)
		{
			session.RenderNextFrame(session.Framebuffer);
			if (session.FaultMessage is { } fault)
				throw new InvalidOperationException(fault);
			var screen = BinaryPrimitives.ReadUInt32BigEndian(session.Machine.ChipRam.Span[screenStateAddress..]);
			sawSplash |= screen == splash;
			if (sawSplash && screen == waitingForDisk && session.Framebuffer.Distinct().Take(3).Count() == 3)
			{
				Console.WriteLine("CopperStart Mini boot smoke passed.");
				return;
			}
		}
		throw new InvalidOperationException("CopperStart Mini did not show its splash and reach the insert-disk screen.");
	}

	private static void RunDesktop(string? userDataDirectory = null)
	{
		CopperScreenCrashLog.Install(StartupArgs, userDataDirectory);
		try
		{
			// Keep the normal Windows priority; starting the app needs no scheduling privilege.
			//GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
			GCSettings.LatencyMode = GCLatencyMode.Interactive;
			BuildAvaloniaApp().StartWithClassicDesktopLifetime(StartupArgs);
		}
		catch (Exception ex)
		{
			CopperScreenCrashLog.WriteException("Program.Main", ex, ex);
			throw;
		}
		finally
		{
			CopperScreenCrashLog.Shutdown();
		}
	}

	private static AppBuilder BuildAvaloniaApp()
	{
		return AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.LogToTrace();
	}
}
