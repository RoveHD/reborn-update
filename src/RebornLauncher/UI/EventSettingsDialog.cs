using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shell;
using PokemonRebornLauncher.Services;

namespace PokemonRebornLauncher.UI;

internal sealed class EventSettingsDialog : Window
{
    private readonly CheckBox decorations, interactive, sounds, ambient;
    private readonly Slider volume;
    private string quality, intensity, ambientFrequency;
    public bool Accepted { get; private set; }

    public EventSettingsDialog(Window owner, LauncherSettings settings)
    {
        Owner = owner;
        Title = "Lebendige Jahreszeiten";
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 700;
        Height = 850;
        MaxHeight = SystemParameters.WorkArea.Height - 48;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Tag = owner.Tag;
        FontFamily = new FontFamily("pack://application:,,,/assets/fonts/#Barlow");
        FontSize = 16;
        foreach (string key in new[] { "Ink", "Surface", "Text", "Muted", "Faint", "Violet", "Mint", "Line", "CtaText", "ThemeCursor", "NavigationSurface", "NavigationHeader", "NavigationFooter", "NavigationLine" })
            Resources[key] = owner.FindResource(key);
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 76, GlassFrameThickness = new Thickness(0), ResizeBorderThickness = new Thickness(0), UseAeroCaptionButtons = false });
        Background = (Brush)FindResource("NavigationSurface");
        Foreground = (Brush)FindResource("Text");
        quality = settings.EventParticleQuality;
        intensity = settings.EventAnimationIntensity;
        ambientFrequency = settings.HalloweenAmbientFrequency;
        var stack = new StackPanel();
        stack.Children.Add(Description("Gestalte die Saisonobjekte, Animationen und Geräusche nach deinem Geschmack."));
        stack.Children.Add(Label("DEKORATION UND BEDIENUNG"));
        decorations = Toggle("Event-Dekorationen", settings.EventDecorationsEnabled);
        interactive = Toggle("Anklickbare Objekte", settings.InteractivePropsEnabled);
        stack.Children.Add(decorations);
        stack.Children.Add(interactive);
        stack.Children.Add(Label("PARTIKELEFFEKTE"));
        stack.Children.Add(ChoiceRow(new[] { ("Aus", "off"), ("Wenig", "low"), ("Mittel", "medium"), ("Viele", "high") }, quality, value => quality = value));
        stack.Children.Add(Label("ANIMATIONSINTENSITÄT"));
        stack.Children.Add(ChoiceRow(new[] { ("Reduziert", "reduced"), ("Normal", "normal"), ("Hoch", "high") }, intensity, value => intensity = value));
        sounds = Toggle("Event-Geräusche", settings.EventSoundsEnabled);
        sounds.Margin = new Thickness(0,20,0,5);
        stack.Children.Add(sounds);
        stack.Children.Add(Description("Für anklickbare Objekte und Halloween-Atmosphäre. Musik hat einen eigenen Schalter."));
        var volumeLabel = new TextBlock { FontSize = 16, Margin = new Thickness(0,8,0,0) };
        volume = new Slider { Minimum = 0, Maximum = 1, Value = settings.EventSoundVolume, Height = 44, SmallChange = .02, LargeChange = .1, IsMoveToPointEnabled = true, Style = (Style)owner.FindResource(typeof(Slider)), ToolTip = "Lautstärke der Event-Geräusche" };
        void UpdateLabel() => volumeLabel.Text = $"Effektlautstärke · {volume.Value:P0}";
        volume.ValueChanged += (_, _) => UpdateLabel();
        UpdateLabel();
        stack.Children.Add(volumeLabel);
        stack.Children.Add(volume);
        ambient = Toggle("Halloween-Ambient-Sounds", settings.HalloweenAmbientEnabled);
        ambient.Margin = new Thickness(0,16,0,8);
        stack.Children.Add(ambient);
        stack.Children.Add(Description("Seltenes Geisterlachen, Flüstern und Wind – nur an Halloween und mit aktivierten Event-Geräuschen. Pause beim Minimieren oder Spielen."));
        stack.Children.Add(Label("HÄUFIGKEIT ZUFÄLLIGER SOUNDS"));
        stack.Children.Add(ChoiceRow(new[] { ("Niedrig", "low"), ("Normal", "normal"), ("Hoch", "high") }, ambientFrequency, value => ambientFrequency = value));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = Button("Abbrechen"); cancel.IsCancel = true; cancel.Name = "CancelSettingsButton";
        cancel.Click += (_, _) => Close();
        var accept = Button("Übernehmen"); accept.IsDefault = true; accept.Name = "AcceptSettingsButton";
        accept.BorderBrush = (Brush)FindResource("Violet"); accept.BorderThickness = new Thickness(2);
        accept.Click += (_, _) => { Accepted = true; Close(); };
        buttons.Children.Add(cancel); buttons.Children.Add(accept);
        // Header and actions remain visible while the settings body scrolls.
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition());
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new Grid(); header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var heading = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        heading.Children.Add(new TextBlock { Text = "SAISON-EINSTELLUNGEN", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Mint") });
        heading.Children.Add(new TextBlock { Text = Title, FontSize = 30, FontWeight = FontWeights.SemiBold });
        header.Children.Add(heading);
        var close = Button("×"); close.Width = 44; close.Padding = new Thickness(0); close.Margin = new Thickness(16,0,0,0); close.ToolTip = "Schließen";
        close.Click += (_, _) => Close(); WindowChrome.SetIsHitTestVisibleInChrome(close, true); Grid.SetColumn(close, 1); header.Children.Add(close);
        layout.Children.Add(new Border { Background = (Brush)FindResource("NavigationHeader"), Padding = new Thickness(26,18,26,18), Child = header });
        var scroll = new ScrollViewer { Name = "EventSettingsScroller", VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(26,14,26,20), Content = stack };
        scroll.Style = owner.TryFindResource(typeof(ScrollViewer)) as Style;
        Resources[typeof(ScrollBar)] = owner.FindResource(typeof(ScrollBar));
        Grid.SetRow(scroll, 1); layout.Children.Add(scroll);
        var footer = new Border { Background = (Brush)FindResource("NavigationFooter"), BorderBrush = (Brush)FindResource("NavigationLine"), BorderThickness = new Thickness(0,1,0,0), Padding = new Thickness(26,16,18,16), Child = buttons };
        Grid.SetRow(footer, 2); layout.Children.Add(footer);
        Content = new Border { Background = Background, BorderBrush = (Brush)FindResource("NavigationLine"), BorderThickness = new Thickness(1), Child = layout };
        WindowWorkArea.Attach(this);
    }

    public void ApplyTo(LauncherSettings settings)
    {
        if (!Accepted) return;
        settings.EventDecorationsEnabled = decorations.IsChecked == true;
        settings.InteractivePropsEnabled = interactive.IsChecked == true;
        settings.EventSoundsEnabled = sounds.IsChecked == true;
        settings.EventSoundVolume = volume.Value;
        settings.HalloweenAmbientEnabled = ambient.IsChecked == true;
        settings.HalloweenAmbientFrequency = ambientFrequency;
        settings.EventParticleQuality = quality;
        settings.EventAnimationIntensity = intensity;
    }

    private CheckBox Toggle(string text, bool value) => new() { Content = text, IsChecked = value, Style = (Style)Owner.FindResource("SettingsToggle"), Margin = new Thickness(0,0,0,4) };
    private TextBlock Description(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 16, LineHeight = 24, Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0,0,0,12) };
    private TextBlock Label(string text) => new() { Text = text, FontSize = 16, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Mint"), Margin = new Thickness(0,18,0,10) };
    private Button Button(string text) => new() { Content = text, Style = (Style)Owner.FindResource("SettingsButton"), Margin = new Thickness(0,0,8,6), Padding = new Thickness(16,10,16,10) };
    private Panel ChoiceRow((string Label, string Id)[] choices, string current, Action<string> change)
    {
        var row = new WrapPanel();
        var buttons = new List<(Button Button, string Id)>();
        void Paint(string selected)
        {
            foreach (var item in buttons)
            {
                item.Button.BorderBrush = (Brush)FindResource(item.Id == selected ? "Mint" : "Line");
                item.Button.BorderThickness = new Thickness(item.Id == selected ? 2 : 1);
                item.Button.Foreground = (Brush)FindResource(item.Id == selected ? "Mint" : "Text");
                item.Button.FontWeight = item.Id == selected ? FontWeights.Bold : FontWeights.Normal;
            }
        }
        foreach (var choice in choices)
        {
            var button = Button(choice.Label);
            buttons.Add((button, choice.Id));
            button.Click += (_, _) => { change(choice.Id); Paint(choice.Id); };
            row.Children.Add(button);
        }
        Paint(current);
        return row;
    }
}
