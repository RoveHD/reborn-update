using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace PokemonRebornLauncher;

public partial class MainWindow
{
    private string _activeNavigation = "home";

    private void InitializeNavigation() => SetNavigationState("home");

    private void HomeNav_Click(object sender, RoutedEventArgs e) => SetNavigationState("home");
    private void ExtrasNav_Click(object sender, RoutedEventArgs e) => SetNavigationState("extras");
    private void SettingsNav_Click(object sender, RoutedEventArgs e) => SetNavigationState("settings");

    private void SetNavigationState(string active)
    {
        if (HomeNavButton is null) return;
        active = active is "home" or "changelog" or "extras" or "settings" ? active : "home";
        _activeNavigation = active;
        bool onHome = active == "home";
        // The scene lives beside this scroller, so changing pages only hides
        // the home controls. Retaining their layout keeps the same exclusion
        // geometry for the props when a different page is resized.
        HeroScroller.Visibility = onHome ? Visibility.Visible : Visibility.Hidden;
        StageArtArea.IsHitTestVisible = onHome;
        StageArtArea.IsEnabled = onHome;
        NavigationPanel.Visibility = onHome ? Visibility.Collapsed : Visibility.Visible;
        ExtrasHost.Visibility = active == "extras" ? Visibility.Visible : Visibility.Collapsed;
        SettingsHost.Visibility = active == "settings" ? Visibility.Visible : Visibility.Collapsed;
        PatchNotesHost.Visibility = active == "changelog" ? Visibility.Visible : Visibility.Collapsed;

        NavigationTitle.Text = active switch
        {
            "changelog" => "Was ist neu?",
            "extras" => "Extras",
            "settings" => "Einstellungen",
            _ => "Startseite"
        };
        if (active == "changelog")
        {
            NavigationSubtitle.SetBinding(TextBlock.TextProperty, new Binding(nameof(TextBlock.Text)) { Source = PatchNotesSubtitle });
        }
        else
        {
            BindingOperations.ClearBinding(NavigationSubtitle, TextBlock.TextProperty);
            NavigationSubtitle.Text = active switch
            {
                "extras" => "Versionen, Updates und Spielordner verwalten",
                "settings" => "Darstellung, Musik und Jahreszeiten anpassen",
                _ => string.Empty
            };
        }

        foreach (var (button, id) in new[]
        {
            (HomeNavButton, "home"), (OpenPatchNotesButton, "changelog"),
            (ExtrasNavButton, "extras"), (SettingsNavButton, "settings")
        })
        {
            bool selected = id == active;
            button.Background = selected ? (Brush)FindResource("Surface") : Brushes.Transparent;
            button.SetResourceReference(Control.ForegroundProperty, selected ? "Text" : "Muted");
            button.BorderBrush = selected ? (Brush)FindResource("Violet") : Brushes.Transparent;
            button.BorderThickness = selected ? new Thickness(0, 0, 0, 2) : new Thickness(0);
        }
    }
}
