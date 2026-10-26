using System.Windows;
using System.Windows.Media;
using PokemonRebornLauncher.Services;

namespace PokemonRebornLauncher;

public partial class MainWindow
{
    private void InitializeAtmosphere()
    {
        InitializeSeasonalWeather();
        AtmosphereView.AttachForeground(NearSceneLayer);
        var sceneCamera = new TranslateTransform();
        StageArtArea.RenderTransform = sceneCamera;
        void AttachSceneDecorations()
        {
            Point? branch = _activeTheme == "halloween"
                ? AtmosphereView.ForegroundPointIn(EventPropsView, new Point(
                    SceneDecorationLayout.HalloweenSpiderBranchX, SceneDecorationLayout.HalloweenSpiderBranchY)) : null;
            EventPropsView.SetSpiderAttachment(branch);
            var anchors = new Dictionary<PropKind, Point>();
            if (_activeTheme == "halloween")
                foreach (var anchor in SceneDecorationLayout.HalloweenAnchorsForAspect(AtmosphereView.ActualWidth / Math.Max(1, AtmosphereView.ActualHeight)))
                    if ((anchor.Kind == PropKind.Bat
                        ? AtmosphereView.CastlePointIn(EventPropsView, new Point(anchor.X, anchor.Y))
                        : AtmosphereView.LandscapePointIn(EventPropsView, new Point(anchor.X, anchor.Y))) is Point point)
                        anchors[anchor.Kind] = point;
            EventPropsView.SetLandscapeAttachments(anchors);
        }
        AtmosphereView.CameraChanged += position =>
        {
            sceneCamera.X = position.X; sceneCamera.Y = position.Y;
            AttachSceneDecorations();
        };
        // Layout supplies the final rendered image size after FitCover changes it.
        // The setter ignores identical points, so this does not cause a layout loop.
        StageArtArea.LayoutUpdated += (_, _) => AttachSceneDecorations();
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
