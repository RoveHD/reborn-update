using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;

namespace PokemonRebornLauncher.UI;

internal sealed class LauncherDialog : Window
{
    internal MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;
    internal Button AcceptButton { get; }
    internal Button DismissButton { get; }

    internal LauncherDialog(Window owner, string message, string title, MessageBoxButton buttons)
    {
        Owner = owner; Title = title; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 510; MaxHeight = SystemParameters.WorkArea.Height - 40;
        SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None; ShowInTaskbar = false;
        foreach (string key in new[] { "Ink", "Surface", "Text", "Muted", "Faint", "Violet", "Mint", "Line" }) Resources[key] = owner.FindResource(key);
        Background = (Brush)owner.FindResource("Surface");
        Foreground = Brushes.White; FontFamily = new FontFamily("pack://application:,,,/assets/fonts/#Barlow");
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 42, GlassFrameThickness = new Thickness(0), ResizeBorderThickness = new Thickness(0), UseAeroCaptionButtons = false });
        var frame = new Border { Background = Background, BorderThickness = new Thickness(1), BorderBrush = (Brush)owner.FindResource("Line"), Padding = new Thickness(30) };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = "POKÉMON REBORN", FontSize = 11, Foreground = (Brush)owner.FindResource("Mint"), Margin = new Thickness(0, 0, 0, 15) });
        stack.Children.Add(new TextBlock { Text = title, FontFamily = new FontFamily("pack://application:,,,/assets/fonts/#Barlow Condensed"), FontSize = 32, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
        stack.Children.Add(new ScrollViewer { MaxHeight = 310, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new TextBlock { Text = message, FontSize = 15, LineHeight = 23, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(201, 191, 209)) } });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 25, 0, 0) };
        DismissButton = new Button { Content = "Abbrechen", IsCancel = true, MinWidth = 110, Style = (Style)owner.FindResource("SecondaryButton"), Margin = new Thickness(0, 0, 12, 0) };
        AcceptButton = new Button { Content = buttons == MessageBoxButton.YesNo ? "Fortfahren" : "Verstanden", IsDefault = true, MinWidth = 120, Style = (Style)owner.FindResource("SecondaryButton"), Background = (Brush)owner.FindResource("Line") };
        DismissButton.Click += (_, _) => { Result = MessageBoxResult.No; Close(); };
        AcceptButton.Click += (_, _) => { Result = buttons == MessageBoxButton.YesNo ? MessageBoxResult.Yes : MessageBoxResult.OK; Close(); };
        if (buttons == MessageBoxButton.YesNo) actions.Children.Add(DismissButton);
        actions.Children.Add(AcceptButton); stack.Children.Add(actions); frame.Child = stack; Content = frame;
        Loaded += (_, _) => { if (buttons == MessageBoxButton.YesNo) DismissButton.Focus(); else AcceptButton.Focus(); };
    }
}
