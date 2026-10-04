using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Styling;

namespace CopperScreen;

internal static class CopperScreenAppearance
{
    public static readonly IBrush Background = Brush("#1B1A19");
    public static readonly IBrush Surface = Brush("#242321");
    public static readonly IBrush Inset = Brush("#201F1D");
    public static readonly IBrush ControlSurface = Brush("#302E2B");
    public static readonly IBrush PopupSurface = Brush("#2A2825");
    public static readonly IBrush Hover = Brush("#3C3833");
    public static readonly IBrush Text = Brush("#EEE9E2");
    public static readonly IBrush MutedText = Brush("#BDB6AD");
    public static readonly IBrush QuietBorder = Brush("#393530");
    public static readonly IBrush FieldBorder = Brush("#81766B");
    public static readonly IBrush Accent = Brush("#DCA578");
    public static readonly IBrush AccentHover = Brush("#EBB88C");
    public static readonly IBrush AccentPressed = Brush("#C48F63");
    public static readonly IBrush OnAccent = Brush("#21180F");
    public static readonly IBrush ProtectedSurface = Brush("#39322C");
    public static readonly IBrush ProtectedText = Brush("#CDBEAD");

    public static void Apply(Application app)
    {
        app.Resources["ButtonBackgroundPointerOver"] = Hover;
        app.Resources["ButtonBackgroundPressed"] = QuietBorder;
        app.Resources["ButtonForegroundPointerOver"] = Text;
        app.Resources["ButtonForegroundPressed"] = Text;
        app.Resources["ComboBoxBackgroundPointerOver"] = Hover;
        app.Resources["ComboBoxBackgroundPressed"] = ControlSurface;
        app.Resources["ComboBoxBorderBrushPointerOver"] = MutedText;
        app.Styles.Add(new Style(selector => selector.OfType<Window>())
        {
            Setters = { new Setter(TemplatedControl.ForegroundProperty, Text) }
        });
        app.Styles.Add(new Style(selector => selector.OfType<ComboBox>())
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, ControlSurface),
                new Setter(TemplatedControl.ForegroundProperty, Text),
                new Setter(TemplatedControl.BorderBrushProperty, FieldBorder),
                new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(8))
            }
        });
        app.Styles.Add(new Style(selector => selector.OfType<FlyoutPresenter>())
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, PopupSurface),
                new Setter(TemplatedControl.ForegroundProperty, Text),
                new Setter(TemplatedControl.BorderBrushProperty, QuietBorder),
                new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(1)),
                new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(12)),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(16))
            }
        });
    }

    public static void ConfigureKeyboardFocus(TemplatedControl control)
    {
        control.FocusAdorner = new FuncTemplate<Control>(() =>
        {
            var outline = new Border
            {
                BorderBrush = ReferenceEquals(control.Background, Accent) ? Text : Accent,
                BorderThickness = new Thickness(2), CornerRadius = control.CornerRadius, IsHitTestVisible = false
            };
            outline.Bind(Border.WidthProperty, new Binding("Bounds.Width") { Source = control });
            outline.Bind(Border.HeightProperty, new Binding("Bounds.Height") { Source = control });
            return outline;
        });
    }

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
}
