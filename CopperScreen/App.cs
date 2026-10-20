using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace CopperScreen;

internal sealed class App : Application
{
	public override void Initialize()
	{
		RequestedThemeVariant = ThemeVariant.Dark;
		var theme = new FluentTheme();
		theme.Palettes[ThemeVariant.Dark] = new ColorPaletteResources
		{
			Accent = Color.Parse("#DCA578"),
			BaseHigh = Color.Parse("#EEE9E2"),
			BaseMediumHigh = Color.Parse("#BDB6AD")
		};
		Styles.Add(theme);
		CopperScreenAppearance.Apply(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
#if COPPERSCREEN_INSTALLED_TESTS
			desktop.MainWindow = new MainWindow(Program.StartupArgs, CopperScreenInstalledProbe.ActiveSession ?? CopperScreenInstalledSoak.ActiveSession ?? CopperScreenInstalledRdbProof.ActiveSession);
            if (CopperScreenInstalledRdbProof.ActiveSession != null) CopperScreenInstalledRdbProof.Observe((MainWindow)desktop.MainWindow);
            else if (CopperScreenInstalledSoak.ActiveSession != null) CopperScreenInstalledSoak.Observe((MainWindow)desktop.MainWindow);
            else CopperScreenInstalledProbe.Observe(desktop.MainWindow);
#else
			desktop.MainWindow = new MainWindow(Program.StartupArgs);
#endif
		}

		base.OnFrameworkInitializationCompleted();
	}
}
