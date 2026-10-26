using System.Windows;

namespace PokemonRebornLauncher;

public partial class MainWindow
{
    private void InitializeGengarEncounter()
    {
        GengarView.BackdropAbsenceChanged += AtmosphereView.SetGengarAbsence;
        void UpdateGengarOrigin() => GengarView.SetSceneOrigin(
            AtmosphereView.LandscapePointIn(GengarView, new Point(350d / 1672, 500d / 941)));
        GengarView.LayoutUpdated += (_, _) => UpdateGengarOrigin();
        AtmosphereView.CameraChanged += _ => UpdateGengarOrigin();
    }
}
