using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PokemonRebornLauncher.UI;

namespace PokemonRebornLauncher;

public partial class MainWindow
{
    private Cursor? _seasonCursor;

    private void InitializePointer()
    {
        PreviewMouseMove += (_, e) => MouseTrailView.Move(e.GetPosition(MouseTrailView));
        PreviewMouseLeftButtonDown += (_, e) => MouseTrailView.Burst(e.GetPosition(MouseTrailView));
        MouseLeave += (_, _) => MouseTrailView.Clear();
        Deactivated += (_, _) => MouseTrailView.Clear();
        Closed += (_, _) => { MouseTrailView.Clear(); _seasonCursor?.Dispose(); };
    }

    private void UpdatePointer(Color accent)
    {
        var next = SeasonalPointer.Create(accent);
        var previous = _seasonCursor;
        _seasonCursor = next;
        Resources["ThemeCursor"] = next;
        previous?.Dispose();
        MouseTrailView.Clear();
    }

    private async void MouseTrail_Changed(object sender, RoutedEventArgs e)
    {
        if (!_presentationReady || _suppressSettingsEvents) return;
        _settings.MouseTrailEnabled = MouseTrailCheckBox.IsChecked == true;
        ApplyMotion();
        await SavePresentationAsync();
    }

    internal void VerifyMouseEffects()
    {
        if (!_smokeTest) throw new InvalidOperationException("Nur für die Mausprüfung.");
        var accent = ((SolidColorBrush)FindResource("Violet")).Color;
        MouseTrailView.Configure(accent, true, false);
        for (int i = 0; i < 100; i++) MouseTrailView.Move(new Point(350 + i, 180 + Math.Sin(i * 0.04) * 25));
        MouseTrailView.Burst(new Point(450, 180));
        if (_seasonCursor is null || MouseTrailView.ParticleCount == 0 || MouseTrailView.ParticleCount > 40 || !MouseTrailView.IsAnimating)
            throw new InvalidOperationException("Mauszeiger oder Leuchtspur nicht initialisiert.");
        MouseTrailView.Configure(accent, true, true);
        MouseTrailView.Move(new Point(480, 210));
        if (MouseTrailView.ParticleCount != 0 || MouseTrailView.IsAnimating)
            throw new InvalidOperationException("Ruhige Effekte stoppen die Mausspur nicht.");
        MouseTrailCheckBox.IsChecked = false;
        if (_settings.MouseTrailEnabled) throw new InvalidOperationException("Mausspur-Schalter reagiert nicht.");
        MouseTrailCheckBox.IsChecked = true;
        MouseTrailView.Configure(accent, true, false);
        for (int i = 0; i < 16; i++) MouseTrailView.Move(new Point(370 + i * 5, 210 - i * 2));
        MouseTrailView.Burst(new Point(445, 180));
    }
}
