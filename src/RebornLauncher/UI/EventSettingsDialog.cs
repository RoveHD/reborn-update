using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
        Width = 570;
        SizeToContent = SizeToContent.Height;
        MaxHeight = SystemParameters.WorkArea.Height - 40;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Tag = owner.Tag;
        FontFamily = new FontFamily("pack://application:,,,/assets/fonts/#Barlow");
        foreach (string key in new[] { "Ink", "Surface", "Text", "Muted", "Faint", "Violet", "Mint", "Line", "ThemeCursor" })
            Resources[key] = owner.FindResource(key);
        Background = (Brush)FindResource("Surface");
        Foreground = (Brush)FindResource("Text");
        quality = settings.EventParticleQuality;
        intensity = settings.EventAnimationIntensity;
        ambientFrequency = settings.HalloweenAmbientFrequency;
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = Title, FontSize = 28, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,8) });
        stack.Children.Add(new TextBlock { Text = "Kleine Entdeckungen rund um deine Pokémon. Die Spielbedienung bleibt frei.", TextWrapping = TextWrapping.Wrap, FontSize = 15, Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0,0,0,20) });
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
        stack.Children.Add(new TextBlock { Text = "Für anklickbare Objekte und Halloween-Atmosphäre. Musik hat einen eigenen Schalter.", TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0,0,0,12) });
        var volumeLabel = new TextBlock { FontSize = 14 };
        volume = new Slider { Minimum = 0, Maximum = 1, Value = settings.EventSoundVolume, Height = 30, Style = (Style)owner.FindResource(typeof(Slider)), ToolTip = "Lautstärke der Event-Geräusche" };
        void UpdateLabel() => volumeLabel.Text = $"Effektlautstärke · {volume.Value:P0}";
        volume.ValueChanged += (_, _) => UpdateLabel();
        UpdateLabel();
        stack.Children.Add(volumeLabel);
        stack.Children.Add(volume);
        ambient = Toggle("Halloween-Ambient-Sounds", settings.HalloweenAmbientEnabled);
        ambient.Margin = new Thickness(0,16,0,8);
        stack.Children.Add(ambient);
        stack.Children.Add(new TextBlock { Text = "Seltene leise Geisterlachen, Flüstern und Wind – nur im Halloween-Theme und mit aktivierten Event-Geräuschen. Pause beim Minimieren oder Spielen.", TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = (Brush)FindResource("Muted") });
        stack.Children.Add(Label("HÄUFIGKEIT ZUFÄLLIGER SOUNDS"));
        stack.Children.Add(ChoiceRow(new[] { ("Niedrig", "low"), ("Normal", "normal"), ("Hoch", "high") }, ambientFrequency, value => ambientFrequency = value));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,22,0,0) };
        var cancel = Button("Abbrechen"); cancel.IsCancel = true;
        cancel.Click += (_, _) => Close();
        var accept = Button("Übernehmen"); accept.IsDefault = true;
        accept.Click += (_, _) => { Accepted = true; Close(); };
        buttons.Children.Add(cancel); buttons.Children.Add(accept);
        stack.Children.Add(buttons);
        Content = new Border { Background = Background, BorderBrush = (Brush)FindResource("Line"), BorderThickness = new Thickness(1), Padding = new Thickness(28), Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = MaxHeight - 58, Content = stack } };
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

    private CheckBox Toggle(string text, bool value) => new() { Content = text, IsChecked = value, FontSize = 16, Style = (Style)Owner.FindResource("ToggleCheckBox"), Margin = new Thickness(0,0,0,8), MinHeight = 36 };
    private TextBlock Label(string text) => new() { Text = text, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Mint"), Margin = new Thickness(0,16,0,8) };
    private Button Button(string text) => new() { Content = text, Style = (Style)Owner.FindResource("SecondaryButton"), Margin = new Thickness(0,0,8,0), Padding = new Thickness(12,8,12,8) };
    private Panel ChoiceRow((string Label, string Id)[] choices, string current, Action<string> change)
    {
        var row = new WrapPanel();
        var buttons = new List<(Button Button, string Id)>();
        void Paint(string selected)
        {
            foreach (var item in buttons)
            {
                item.Button.BorderBrush = (Brush)FindResource(item.Id == selected ? "Mint" : "Line");
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
