using System.Windows;

namespace PokemonRebornLauncher;

public partial class MainWindow
{
    private void InitializeGengarEncounter()
    {
        GengarView.BackdropAbsenceChanged += AtmosphereView.SetGengarAbsence;
        void UpdateGengarOrigin() => GengarView.SetSceneOrigin(
            AtmosphereView.GengarPointIn(GengarView));
        GengarView.LayoutUpdated += (_, _) => UpdateGengarOrigin();
        AtmosphereView.CameraChanged += _ => UpdateGengarOrigin();
    }
}
