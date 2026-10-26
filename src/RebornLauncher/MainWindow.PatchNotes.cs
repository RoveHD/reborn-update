using System.Windows;

namespace PokemonRebornLauncher;

public partial class MainWindow
{
    private void OpenPatchNotes_Click(object sender, RoutedEventArgs e) => SetNavigationState("changelog");
}
