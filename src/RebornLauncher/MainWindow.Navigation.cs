using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;

namespace PokemonRebornLauncher;

public partial class MainWindow
{
    private Window? _extrasWindow;
    private Window? _settingsWindow;

    private void InitializeNavigation() => SetNavigationState("home");

    private void HomeNav_Click(object sender, RoutedEventArgs e) => SetNavigationState("home");

    private void ExtrasNav_Click(object sender, RoutedEventArgs e)
    {
        SetNavigationState("extras");
        ShowNavigationDialog(settings: false);
    }

    private void SettingsNav_Click(object sender, RoutedEventArgs e)
    {
        SetNavigationState("settings");
        ShowNavigationDialog(settings: true);
    }

    private void SetNavigationState(string active)
    {
        if (HomeNavButton is null) return;
        foreach (var (button, id) in new[]
        {
            (HomeNavButton, "home"), (OpenPatchNotesButton, "changelog"),
            (ExtrasNavButton, "extras"), (SettingsNavButton, "settings")
        })
        {
            bool selected = id == active;
            if (selected)
            {
                button.SetResourceReference(Control.BackgroundProperty, "Surface");
                button.SetResourceReference(Control.ForegroundProperty, "Text");
                button.SetResourceReference(Control.BorderBrushProperty, "Violet");
            }
            else
            {
                button.Background = Brushes.Transparent;
                button.SetResourceReference(Control.ForegroundProperty, "Muted");
                button.BorderBrush = Brushes.Transparent;
            }
        }
    }

    private void ShowNavigationDialog(bool settings)
    {
        var activeDialog = settings ? _settingsWindow : _extrasWindow;
        if (activeDialog is not null) { activeDialog.Activate(); return; }

        Border host = settings ? SettingsHost : ExtrasHost;
        host.Visibility = Visibility.Visible;
        Root.Children.Remove(host);
        var dialog = new Window
        {
            Owner = this,
            Title = settings ? "Einstellungen – Pokémon Reborn" : "Extras – Pokémon Reborn",
            Width = Math.Min(settings ? 650 : 640, SystemParameters.WorkArea.Width - 40),
            Height = Math.Min(settings ? 690 : 560, SystemParameters.WorkArea.Height - 60),
            MinWidth = 500, MinHeight = 420, ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.CanResize,
            Background = (Brush)FindResource("Ink"), Foreground = (Brush)FindResource("Text")
        };
        foreach (object key in Resources.Keys) dialog.Resources[key] = Resources[key];
        WindowChrome.SetWindowChrome(dialog, new WindowChrome
        {
            CaptionHeight = 46, GlassFrameThickness = new Thickness(0),
            ResizeBorderThickness = new Thickness(6), UseAeroCaptionButtons = false
        });

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(46) });
        layout.RowDefinitions.Add(new RowDefinition());
        var titleBar = new Grid { Background = (Brush)FindResource("Surface") };
        titleBar.ColumnDefinitions.Add(new ColumnDefinition());
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new TextBlock
        {
            Text = settings ? "Einstellungen" : "Extras",
            FontFamily = new FontFamily("pack://application:,,,/assets/fonts/#Barlow Condensed"),
            FontSize = 19, FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("Text"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 0, 0, 0)
        };
        titleBar.Children.Add(title);
        var close = new Button
        {
            Content = "×", ToolTip = "Schließen", Width = 44, IsCancel = true,
            Style = (Style)FindResource("WindowControlButton"),
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(close, 1);
        WindowChrome.SetIsHitTestVisibleInChrome(close, true);
        close.Click += (_, _) => dialog.Close();
        titleBar.Children.Add(close);
        layout.Children.Add(titleBar);

        Grid.SetRow(host, 1);
        host.Padding = new Thickness(26, 16, 26, 22);
        layout.Children.Add(host);
        dialog.Content = layout;
        if (settings) _settingsWindow = dialog;
        else _extrasWindow = dialog;
        try { dialog.ShowDialog(); }
        finally
        {
            layout.Children.Remove(host);
            host.Visibility = Visibility.Collapsed;
            host.Padding = new Thickness(28);
            Grid.SetRow(host, 0);
            Root.Children.Add(host);
            if (settings) _settingsWindow = null;
            else _extrasWindow = null;
            SetNavigationState("home");
            (settings ? SettingsNavButton : ExtrasNavButton).Focus();
        }
    }

}
