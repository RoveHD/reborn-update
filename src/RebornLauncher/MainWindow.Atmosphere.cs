using System.Windows;
using System.Windows.Media;

namespace PokemonRebornLauncher;

public partial class MainWindow
{
    private void InitializeAtmosphere()
    {
        InitializeSeasonalWeather();
        AtmosphereView.AttachForeground(NearSceneLayer);
        var sceneCamera = new TranslateTransform();
        StageArtArea.RenderTransform = sceneCamera;
        AtmosphereView.CameraChanged += position => { sceneCamera.X = position.X; sceneCamera.Y = position.Y; };
        PreviewMouseMove += (_, e) => AtmosphereView.SetPointer(e.GetPosition(AtmosphereView));
        MouseLeave += (_, _) => AtmosphereView.ResetPointer();
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible) AtmosphereView.Configure(_activeTheme, _daylight, false, IsMotionReduced);
            else if (_presentationReady) ApplyMotion(restartEntrance: false);
        };
        Closed += (_, _) => AtmosphereView.Configure(_activeTheme, _daylight, false, true);
    }
}
