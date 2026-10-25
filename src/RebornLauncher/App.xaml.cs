using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PokemonRebornLauncher.Services;
using PokemonRebornLauncher.UI;

namespace PokemonRebornLauncher;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            if (await SelfUpdateService.TryHandleArgumentsAsync(e.Args))
            {
                Shutdown();
                return;
            }

            if (e.Args.FirstOrDefault() == "--lighting-smoke-test")
            {
                if (e.Args.Length != 2) throw new ArgumentException("Verwendung: --lighting-smoke-test <Ausgabeordner>");
                var output = Path.GetFullPath(e.Args[1]);
                Directory.CreateDirectory(output);
                var preview = new MainWindow(smokeTest: true);
                MainWindow = preview;
                preview.Show();
                preview.Width = 1280;
                preview.Height = 800;
                await Dispatcher.InvokeAsync(() => preview.UpdateLayout());
                preview.VerifyLightingControls();
                await preview.VerifyLightingMorphAsync(async name => { await RenderAsync(preview, Path.Combine(output, name + ".png")); });
                File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { success = true, palettes = 8, continuousColors = true, reversal = true, reducedMotion = true, artworkBlend = true }));
                preview.Close();
                Shutdown();
                return;
            }

            if (e.Args.FirstOrDefault() == "--preview-all")
            {
                if (e.Args.Length > 2 || (e.Args.Length == 2 && e.Args[1] is not ("--day" or "--night")))
                    throw new ArgumentException("Verwendung: --preview-all [--day|--night]");
                bool[] periods = e.Args.Length == 1 ? [true, false] : [e.Args[1] == "--day"];
                foreach (string theme in ThemeCalendar.ThemeIds)
                foreach (bool daylight in periods) ShowThemePreview(theme, daylight);
                return;
            }

            var previewIndex = Array.FindIndex(e.Args, arg => string.Equals(arg, "--preview-theme", StringComparison.OrdinalIgnoreCase));
            if (previewIndex >= 0)
            {
                if (previewIndex != 0 || e.Args.Length is < 2 or > 3 || !ThemeCalendar.ThemeIds.Contains(e.Args[1], StringComparer.OrdinalIgnoreCase))
                    throw new ArgumentException($"Verwendung: --preview-theme <{string.Join("|", ThemeCalendar.ThemeIds)}> [--day|--night]");
                bool? daylight = e.Args.Length == 2 ? null : e.Args[2].ToLowerInvariant() switch
                {
                    "--day" => true,
                    "--night" => false,
                    _ => throw new ArgumentException("Verwendung: --preview-theme <Theme> [--day|--night]")
                };
                ShowThemePreview(ThemeCalendar.ThemeIds.First(theme => string.Equals(theme, e.Args[1], StringComparison.OrdinalIgnoreCase)), daylight);
                return;
            }

            var inheritedSmokeOutput = Environment.GetEnvironmentVariable("REBORN_LAUNCHER_SMOKE_OUTPUT");
            if (!string.IsNullOrWhiteSpace(inheritedSmokeOutput))
            {
                await RunSmokeTestAsync(inheritedSmokeOutput);
                return;
            }

            var smokeIndex = Array.FindIndex(e.Args, arg => string.Equals(arg, "--smoke-test", StringComparison.OrdinalIgnoreCase));
            if (smokeIndex >= 0)
            {
                if (smokeIndex + 1 >= e.Args.Length)
                    throw new ArgumentException("Verwendung: --smoke-test <Ausgabeordner>");
                await RunSmokeTestAsync(e.Args[smokeIndex + 1]);
                return;
            }

            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            var smokeOutput = Environment.GetEnvironmentVariable("REBORN_LAUNCHER_SMOKE_OUTPUT");
            if (string.IsNullOrWhiteSpace(smokeOutput))
            {
                var smokeIndex = Array.FindIndex(e.Args, arg => arg is "--smoke-test" or "--lighting-smoke-test");
                if (smokeIndex >= 0 && smokeIndex + 1 < e.Args.Length) smokeOutput = e.Args[smokeIndex + 1];
            }
            if (!string.IsNullOrWhiteSpace(smokeOutput))
            {
                var fullOutput = Path.GetFullPath(smokeOutput);
                var errorPath = Path.GetExtension(fullOutput).Equals(".json", StringComparison.OrdinalIgnoreCase)
                    ? fullOutput
                    : Path.Combine(fullOutput, "smoke-result.json");
                Directory.CreateDirectory(Path.GetDirectoryName(errorPath)!);
                File.WriteAllText(errorPath, JsonSerializer.Serialize(new { success = false, error = ex.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
                Shutdown(1);
                return;
            }
            MessageBox.Show(ex.Message, "Pokémon Reborn Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void ShowThemePreview(string themeId, bool? daylight)
    {
        bool isDaylight = daylight ?? (DateTime.Now.Hour is >= 7 and < 19);
        var window = new MainWindow(smokeTest: true);
        MainWindow = window;
        window.WindowState = WindowState.Normal;
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Width = 1280;
        window.Height = 800;
        window.PreviewTheme(themeId, daylight);
        window.AllowPreviewMusic();
        window.Title = $"Pokémon Reborn – {ThemeDisplayName(themeId)} · {(isDaylight ? "Tag" : "Nacht")}";
        window.Activate();
    }

    private static string ThemeDisplayName(string id) => id switch
    {
        "halloween" => "Halloween", "winter" => "Winter", "spring" => "Frühling", "summer" => "Sommer",
        "harvest" => "Herbst", "newyear" => "Neujahr", "arceus" => "Arceus", _ => "Standard"
    };

    private async Task RunSmokeTestAsync(string outputDirectory)
    {
        var requestedOutput = Path.GetFullPath(outputDirectory);
        var resultPath = Path.GetExtension(requestedOutput).Equals(".json", StringComparison.OrdinalIgnoreCase) ? requestedOutput : null;
        var fullOutput = resultPath is null ? requestedOutput : Path.GetDirectoryName(requestedOutput)!;
        Directory.CreateDirectory(fullOutput);
        var window = new MainWindow(smokeTest: true);
        MainWindow = window;
        window.WindowState = WindowState.Normal;
        window.Show();
        window.WindowState = WindowState.Normal;
        await Dispatcher.InvokeAsync(() => window.UpdateLayout());
        var content = (FrameworkElement)window.Content;
        var frameWidth = window.ActualWidth - content.ActualWidth;
        var frameHeight = window.ActualHeight - content.ActualHeight;
        window.Width = 1280 + frameWidth;
        window.Height = 800 + frameHeight;
        window.WindowState = WindowState.Normal;
        await Dispatcher.InvokeAsync(() => window.UpdateLayout());
        RequireClientSize(window, 1280, 800);
        window.VerifyLightingControls();
        window.VerifyVisibleControls();
        var large = await RenderAsync(window, Path.Combine(fullOutput, "launcher-1280x800.png"));
        window.Width = window.MinWidth;
        window.Height = window.MinHeight;
        window.WindowState = WindowState.Normal;
        await Dispatcher.InvokeAsync(() => window.UpdateLayout());
        window.VerifyVisibleControls();
        var compact = await RenderAsync(window, Path.Combine(fullOutput, "launcher-minimum.png"));
        window.PreviewPreviousVersion();
        window.Width = 1280 + frameWidth;
        window.Height = 800 + frameHeight;
        await Dispatcher.InvokeAsync(() => window.UpdateLayout());
        var previous = await RenderAsync(window, Path.Combine(fullOutput, "launcher-previous.png"));
        window.VerifyPresentationControls();
        var artwork = await RenderAsync(window, Path.Combine(fullOutput, "launcher-absol.png"));
        window.Width = window.MinWidth;
        window.Height = window.MinHeight;
        window.PreviewDownload();
        await Dispatcher.InvokeAsync(() => window.UpdateLayout());
        window.VerifyVisibleControls();
        var download = await RenderAsync(window, Path.Combine(fullOutput, "launcher-download.png"));
        window.EndDownloadPreview();
        var audioDuration = await LauncherMusic.RunMutedCodecSmokeCheckAsync(TimeSpan.FromSeconds(15), CancellationToken.None);
        var dialog = new LauncherDialog(window, "Version 7.0.0 ist verfügbar. Jetzt herunterladen und installieren?\n\nDeine bisherige Spielversion bleibt über den Versionswechsel verfügbar.", "Spielupdate verfügbar", MessageBoxButton.YesNo) { Tag = true };
        dialog.Show();
        await Dispatcher.InvokeAsync(() => dialog.UpdateLayout());
        var prompt = await RenderAsync(dialog, Path.Combine(fullOutput, "launcher-dialog.png"));
        dialog.DismissButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        if (dialog.Result != MessageBoxResult.No) throw new InvalidOperationException("Der Dialog hat Abbrechen nicht übernommen.");
        await LauncherMusic.VerifyMutedThemeTransitionsAsync();
        window.VerifyCalendarRollover();
        var propInteractions = window.VerifyEventInteractions();
        var eventSoundCodecs = await EventSoundBank.VerifyMutedCodecsAsync();
        var halloweenAmbient = await HalloweenAmbientAudio.VerifyMutedResourcesAsync();
        var effectSettings = new EventSettingsDialog(window, new LauncherSettings());
        effectSettings.Show();
        await Dispatcher.InvokeAsync(() => effectSettings.UpdateLayout());
        var effectSettingsImage = await RenderAsync(effectSettings, Path.Combine(fullOutput, "launcher-event-settings.png"));
        effectSettings.Close();
        var themes = new Dictionary<string, object>();
        var themeAudioSeconds = new Dictionary<string, double>();
        await SetClientSizeAsync(window, 1280, 800, frameWidth, frameHeight);
        foreach (string theme in ThemeCalendar.ThemeIds)
        {
            window.PreviewTheme(theme, true);
            await Dispatcher.InvokeAsync(() => window.UpdateLayout());
            themes[$"{theme}-day"] = await RenderAsync(window, Path.Combine(fullOutput, $"launcher-theme-{theme}-day.png"));
            window.PreviewTheme(theme, false);
            await Dispatcher.InvokeAsync(() => window.UpdateLayout());
            themes[$"{theme}-night"] = await RenderAsync(window, Path.Combine(fullOutput, $"launcher-theme-{theme}-night.png"));
            themeAudioSeconds[theme] = (await LauncherMusic.RunMutedCodecSmokeCheckAsync(TimeSpan.FromSeconds(15), CancellationToken.None, theme)).TotalSeconds;
        }
        var compactSizes = new Dictionary<string, object>();
        await SetClientSizeAsync(window, 960, 640, frameWidth, frameHeight);
        foreach (string theme in ThemeCalendar.ThemeIds)
        {
            window.PreviewTheme(theme, true);
            await Dispatcher.InvokeAsync(() => window.UpdateLayout());
            window.VerifyVisibleControls();
            window.VerifyPresentationControls();
            compactSizes[$"{theme}-960x640"] = await RenderAsync(window, Path.Combine(fullOutput, $"launcher-scene-{theme}-compact.png"));
        }
        foreach (var (name, width, height) in new[] { ("960x640", 960d, 640d), ("1100x720", 1100d, 720d), ("1280x800", 1280d, 800d) })
        {
            await SetClientSizeAsync(window, width, height, frameWidth, frameHeight);
            window.PreviewTheme("halloween", true);
            compactSizes[name] = await RenderAsync(window, Path.Combine(fullOutput, $"launcher-theme-halloween-day-{name}.png"));
        }
        window.WindowState = WindowState.Maximized;
        await Dispatcher.InvokeAsync(() => window.UpdateLayout());
        WindowWorkArea.VerifyBounds(window);
        window.VerifySceneryLayout();
        var maximizedTheme = await RenderAsync(window, Path.Combine(fullOutput, "launcher-theme-halloween-maximized.png"), allowMaximized: true);
        var maximizedThemes = new Dictionary<string, object>();
        foreach (string theme in ThemeCalendar.ThemeIds)
        {
            window.PreviewTheme(theme, false);
            await Dispatcher.InvokeAsync(() => window.UpdateLayout());
            WindowWorkArea.VerifyBounds(window);
            window.VerifyVisibleControls();
            maximizedThemes[theme] = await RenderAsync(window, Path.Combine(fullOutput, $"launcher-scene-{theme}-maximized.png"), allowMaximized: true);
        }
        await SetClientSizeAsync(window, window.MinWidth - frameWidth, window.MinHeight - frameHeight, frameWidth, frameHeight);
        window.PreviewTheme("halloween", true);
        await Dispatcher.InvokeAsync(() => window.UpdateLayout());
        window.VerifyVisibleControls();
        var seasonalCompact = await RenderAsync(window, Path.Combine(fullOutput, "launcher-season-minimum.png"));
        window.VerifyMouseEffects();
        var mouseEffects = await RenderAsync(window, Path.Combine(fullOutput, "launcher-mouse-trail.png"));
        window.VerifyFreshInstall();
        await Dispatcher.InvokeAsync(() => window.UpdateLayout());
        var freshInstall = await RenderAsync(window, Path.Combine(fullOutput, "launcher-fresh-install.png"));
        window.EndFreshInstallPreview();
        window.Close();
        if (resultPath is not null)
        {
            var result = new
            {
                success = File.Exists(Path.Combine(fullOutput, "launcher-1280x800.png")) && File.Exists(Path.Combine(fullOutput, "launcher-minimum.png")),
                processPath = Environment.ProcessPath,
                processId = Environment.ProcessId,
                launcherVersion = SelfUpdateService.CurrentVersion,
                arguments = Environment.GetCommandLineArgs().Skip(1).ToArray(),
                screenshots = new { large, minimum = compact, previous, artwork, download, prompt },
                nonScrollingThemeLayoutsVerified = themes.Count == ThemeCalendar.ThemeIds.Length * 2
                    && maximizedThemes.Count == ThemeCalendar.ThemeIds.Length
                    && ThemeCalendar.ThemeIds.All(theme => compactSizes.ContainsKey($"{theme}-960x640")),
                lightingModeVerified = true,
                balancedSceneLayoutVerified = true,
                previousVersionControlsVerified = true,
                presentationControlsVerified = true,
                compactControlsVisible = true,
                downloadControlsVerified = true,
                audioCodecVerified = true,
                audioDurationSeconds = audioDuration.TotalSeconds,
                dialogCancellationVerified = true,
                propInteractions, eventSoundCodecs, halloweenAmbient, effectSettingsImage, themes, themeAudioSeconds, compactSizes, maximizedTheme, maximizedThemes, seasonalCompact, calendarRolloverVerified = true, freshInstall, mouseEffects, mouseEffectsVerified = true, musicTransitionsVerified = true
            };
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        }
        Shutdown();
    }

    private static void RequireClientSize(Window window, double expectedWidth, double expectedHeight)
    {
        var content = (FrameworkElement)window.Content;
        if (Math.Abs(content.ActualWidth - expectedWidth) > 1 || Math.Abs(content.ActualHeight - expectedHeight) > 1)
            throw new InvalidOperationException($"Die Vorschau konnte nicht auf {expectedWidth:0}×{expectedHeight:0} DIP geändert werden (aktuell {content.ActualWidth:0}×{content.ActualHeight:0}).");
    }

    private static async Task SetClientSizeAsync(Window window, double width, double height, double frameWidth, double frameHeight)
    {
        window.WindowState = WindowState.Normal;
        window.Width = width + frameWidth;
        window.Height = height + frameHeight;
        await window.Dispatcher.InvokeAsync(() => window.UpdateLayout());
        RequireClientSize(window, width, height);
    }

    private static async Task<object> RenderAsync(Window window, string path, bool allowMaximized = false)
    {
        if (!allowMaximized) window.WindowState = WindowState.Normal;
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);
        var content = (FrameworkElement)window.Content;
        content.UpdateLayout();
        if (window is MainWindow mainWindow)
        {
            if (allowMaximized && window.WindowState != WindowState.Maximized)
                throw new InvalidOperationException("Die maximierte Launcher-Vorschau hat den maximierten Zustand verloren.");
            if (!allowMaximized && window.WindowState != WindowState.Normal)
            throw new InvalidOperationException("Die Launcher-Vorschau wurde vor dem Rendern maximiert.");
            mainWindow.VerifyVisibleControls();
        }
        var widthDip = content.ActualWidth;
        var heightDip = content.ActualHeight;
        if (window is MainWindow && !allowMaximized && (widthDip > 1600 || heightDip > 1200))
            throw new InvalidOperationException($"Die Launcher-Vorschau ist unerwartet groß ({widthDip:0}×{heightDip:0} DIP); vor dem Rendern muss das Fenster im Normalzustand bleiben.");
        var width = (int)Math.Ceiling(widthDip);
        var height = (int)Math.Ceiling(heightDip);
        if (width <= 0 || height <= 0) throw new InvalidOperationException("Die Launcher-Vorschau hat keine gültige Fenstergröße.");
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        await using var file = File.Create(path);
        encoder.Save(file);
        return new { file = Path.GetFileName(path), clientWidthDip = widthDip, clientHeightDip = heightDip, pixelWidth = width, pixelHeight = height };
    }
}
