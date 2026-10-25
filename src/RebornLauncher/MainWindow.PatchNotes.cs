using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;

namespace PokemonRebornLauncher;

public partial class MainWindow
{
    private Window? _patchNotesWindow;

    private void OpenPatchNotes_Click(object sender, RoutedEventArgs e)
    {
        SetNavigationState("changelog");
        if (_patchNotesWindow is not null) { _patchNotesWindow.Activate(); return; }
        var dialog = new Window
        {
            Owner = this, Title = "Was ist neu? – Pokémon Reborn",
            Width = Math.Min(760, SystemParameters.WorkArea.Width - 40),
            Height = Math.Min(660, SystemParameters.WorkArea.Height - 40),
            MinWidth = 420, MinHeight = 320, ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.CanResize,
            Background = (Brush)FindResource("Surface"), Foreground = (Brush)FindResource("Text")
        };
        // The notes remain the same live controls: refresh, offline errors and
        // version updates are visible immediately without a stale text copy.
        foreach (object key in Resources.Keys) dialog.Resources[key] = Resources[key];
        WindowChrome.SetWindowChrome(dialog, new WindowChrome
        {
            CaptionHeight = 38, GlassFrameThickness = new Thickness(0),
            ResizeBorderThickness = new Thickness(6), UseAeroCaptionButtons = false
        });
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        layout.RowDefinitions.Add(new RowDefinition());
        var close = new Button
        {
            Content = "Schließen", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right,
            Style = (Style)FindResource("SecondaryButton"), Padding = new Thickness(12, 4, 12, 4)
        };
        WindowChrome.SetIsHitTestVisibleInChrome(close, true);
        close.Click += (_, _) => dialog.Close();
        layout.Children.Add(close);
        Root.Children.Remove(PatchNotesHost);
        PatchNotesHost.Visibility = Visibility.Visible;
        Grid.SetRow(PatchNotesHost, 1);
        layout.Children.Add(PatchNotesHost);
        dialog.Content = new Border
        {
            Background = dialog.Background, Padding = new Thickness(24, 8, 24, 24), BorderThickness = new Thickness(1),
            BorderBrush = (Brush)FindResource("Line"), Child = layout
        };
        _patchNotesWindow = dialog;
        try { dialog.ShowDialog(); }
        finally
        {
            layout.Children.Remove(PatchNotesHost);
            PatchNotesHost.Visibility = Visibility.Collapsed;
            Grid.SetRow(PatchNotesHost, 0);
            Root.Children.Add(PatchNotesHost);
            _patchNotesWindow = null;
            SetNavigationState("home");
            OpenPatchNotesButton.Focus();
        }
    }
}
