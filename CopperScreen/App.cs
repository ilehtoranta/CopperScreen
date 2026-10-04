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
			desktop.MainWindow = new MainWindow(Program.StartupArgs);
		}

		base.OnFrameworkInitializationCompleted();
	}
}
