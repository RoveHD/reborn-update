namespace PokemonRebornLauncher;

public partial class MainWindow
{
    /// <summary>
    /// The Pokémon is painted into the seasonal backdrop now. Keep the previous
    /// per-theme selections in settings for compatibility, but never use them to
    /// replace or cycle the fixed illustration.
    /// </summary>
    private void ShowArtwork()
    {
        HideSeparatePokemonActors();
        PokemonNameText.Text = ScenePokemonFor(_activeTheme).DisplayName;
        UpdateResponsiveLayout();
    }
}
