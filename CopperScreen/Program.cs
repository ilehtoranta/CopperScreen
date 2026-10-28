using System.Runtime;
using System.Diagnostics;
using Avalonia;

namespace CopperScreen;

internal static class Program
{
	public static string[] StartupArgs { get; private set; } = Array.Empty<string>();

	[STAThread]
	public static void Main(string[] args)
	{
		StartupArgs = args ?? Array.Empty<string>();
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
