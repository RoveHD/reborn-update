using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace PokemonRebornLauncher;

public partial class MainWindow
{
    internal async Task VerifyLightingMorphAsync(Func<string, Task> capture)
    {
        if (!_smokeTest) throw new InvalidOperationException("Nur für die Oberflächenprüfung.");
        static Color ColorOf(string hex) => (Color)ColorConverter.ConvertFromString(hex);
        Color Current(string key) => ((SolidColorBrush)Resources[key]).Color;
        static bool SameColor(Color left, Color right) => left.A == right.A && left.R == right.R && left.G == right.G && left.B == right.B;
        static double Luminance(Color color)
        {
            static double Linear(byte value) => value / 255.0 <= .04045 ? value / 255.0 / 12.92 : Math.Pow((value / 255.0 + .055) / 1.055, 2.4);
            return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
        }
        foreach (string theme in ThemeLooks.Keys)
        {
            PreviewTheme(theme, true);
            if (Luminance(Current("Ink")) > .08 || Luminance(Current("Surface")) > .09)
                throw new InvalidOperationException("Tagesfläche zu hell: " + theme);
            foreach (string key in new[] { "Text", "Muted", "Faint" })
                if ((Luminance(Current(key)) + .05) / (Luminance(Current("Surface")) + .05) < 4.5)
                    throw new InvalidOperationException("Textkontrast zu gering: " + theme + "/" + key);
            await capture(theme + "-day");
        }
        PreviewTheme("standard", false);
        await capture("standard-night");
        var night = Current("Ink");
        var day = ColorOf(DayPalettes["standard"][0]);
        _daylight = true;
        ApplyTheme("standard", _themeDay, animate: true);
        if (!SystemParameters.ClientAreaAnimation)
            throw new InvalidOperationException("Windows-Animationen deaktiviert: Morph-Prüfung benötigt aktivierte Animationen.");
        await Task.Delay(950);
        await Dispatcher.Yield(DispatcherPriority.Render);
        var middle = Current("Ink");
        if (SameColor(middle, night) || SameColor(middle, day) || !((SolidColorBrush)Resources["Ink"]).HasAnimatedProperties)
            throw new InvalidOperationException("Kein sichtbarer Farb-Zwischenstand.");
        await capture("standard-morph");
        // Encoding the scene can take several animation frames. Compare the
        // reversal with the color at the reversal, not the pre-capture sample.
        var beforeReversal = Current("Ink");
        _daylight = false;
        ApplyTheme("standard", _themeDay, animate: true);
        await Dispatcher.Yield(DispatcherPriority.Render);
        if (Math.Abs(Current("Ink").R - beforeReversal.R) > 3)
            throw new InvalidOperationException("Richtungswechsel springt zur Ausgangsfarbe.");
        await Task.Delay(LightingDuration + TimeSpan.FromMilliseconds(250));
        if (!SameColor(Current("Ink"), night)) throw new InvalidOperationException($"Nachtziel nicht erreicht: {Current("Ink")} statt {night}.");
        _daylight = true;
        ApplyTheme("standard", _themeDay, animate: true);
        await Task.Delay(LightingDuration + TimeSpan.FromMilliseconds(250));
        if (!SameColor(Current("Ink"), day) || PokemonArtwork.Source is DrawingImage)
            throw new InvalidOperationException("Tagziel oder Artwork-Überblendung nicht abgeschlossen.");
        await capture("standard-day-complete");
        _daylight = false;
        ApplyTheme("standard", _themeDay, animate: true);
        await Task.Delay(450);
        _settings.ReduceMotion = true;
        ApplyMotion();
        if (!SameColor(Current("Ink"), night) || PokemonArtwork.Source is DrawingImage) throw new InvalidOperationException($"Reduzierte Bewegung beendet Morph nicht: {Current("Ink")} statt {night}; Artwork {PokemonArtwork.Source?.GetType().Name ?? "integrierte Szene"}.");
        _daylight = true;
        ApplyTheme("standard", _themeDay, animate: true);
        if (!SameColor(Current("Ink"), day)) throw new InvalidOperationException("Reduzierte Bewegung animiert weiter.");
        _settings.ReduceMotion = false;
    }
}
