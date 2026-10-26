using System.Windows;
using PokemonRebornLauncher.UI;

namespace PokemonRebornLauncher;

public partial class MainWindow
{
    private readonly SeasonalWeatherOverlay _seasonalWeather = new();

    private void InitializeSeasonalWeather()
    {
        // Weather crosses the whole launcher but stays below fireworks, Gengar and the pointer.
        Root.Children.Insert(Root.Children.IndexOf(FireworksView), _seasonalWeather);
        AtmosphereView.ConfigurationChanged += ConfigureSeasonalWeather;
        LayoutUpdated += UpdateWeatherReadingZones;
        Closed += (_, _) =>
        {
            AtmosphereView.ConfigurationChanged -= ConfigureSeasonalWeather;
            LayoutUpdated -= UpdateWeatherReadingZones;
            _seasonalWeather.Configure(_activeTheme, _daylight, false, 0, 1, true, false);
        };
    }

    private void ConfigureSeasonalWeather(string theme, bool daylight, bool active, bool reduceMotion)
    {
        int limit = _settings.EventParticleQuality switch { "off" => 0, "low" => 45, "high" => 150, _ => 90 };
        double intensity = _settings.EventAnimationIntensity switch { "high" => 1.6, "reduced" => .4, _ => 1 };
        _seasonalWeather.Configure(theme, daylight, _settings.EventDecorationsEnabled, limit, intensity, reduceMotion, active);
        UpdateWeatherReadingZones(this, EventArgs.Empty);
    }

    private void UpdateWeatherReadingZones(object? sender, EventArgs args)
    {
        var zones = new List<Rect>();
        foreach (FrameworkElement element in new FrameworkElement[] { HeroPanel, OperationsPanel, NavigationPanel })
        {
            // Only the active in-window page contributes a reading zone.
            if (!element.IsVisible || !Root.IsAncestorOf(element) || element.ActualWidth <= 0 || element.ActualHeight <= 0) continue;
            var bounds = element.TransformToAncestor(Root).TransformBounds(new Rect(element.RenderSize));
            bounds.Inflate(8, 8);
            zones.Add(bounds);
        }
        _seasonalWeather.SetReadingZones(zones);
    }
}
