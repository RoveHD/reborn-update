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
                var smokeIndex = Array.FindIndex(e.Args, arg => string.Equals(arg, "--smoke-test", StringComparison.OrdinalIgnoreCase));
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

    private async Task RunSmokeTestAsync(string outputDirectory)
    {
        var requestedOutput = Path.GetFullPath(outputDirectory);
        var resultPath = Path.GetExtension(requestedOutput).Equals(".json", StringComparison.OrdinalIgnoreCase) ? requestedOutput : null;
        var fullOutput = resultPath is null ? requestedOutput : Path.GetDirectoryName(requestedOutput)!;
        Directory.CreateDirectory(fullOutput);
        var window = new MainWindow(smokeTest: true);
        MainWindow = window;
        window.Show();
        await Dispatcher.InvokeAsync(() => window.UpdateLayout());
        var content = (FrameworkElement)window.Content;
        var frameWidth = window.ActualWidth - content.ActualWidth;
        var frameHeight = window.ActualHeight - content.ActualHeight;
        window.Width = 1280 + frameWidth;
        window.Height = 800 + frameHeight;
        await Dispatcher.InvokeAsync(() => window.UpdateLayout());
        RequireClientSize(window, 1280, 800);
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
        var themes = new Dictionary<string, object>();
        var themeAudioSeconds = new Dictionary<string, double>();
        window.Width = 1280 + frameWidth;
        window.Height = 800 + frameHeight;
        foreach (string theme in ThemeCalendar.ThemeIds)
        {
            window.PreviewTheme(theme);
            await Dispatcher.InvokeAsync(() => window.UpdateLayout());
            themes[theme] = await RenderAsync(window, Path.Combine(fullOutput, $"launcher-theme-{theme}.png"));
            themeAudioSeconds[theme] = (await LauncherMusic.RunMutedCodecSmokeCheckAsync(TimeSpan.FromSeconds(15), CancellationToken.None, theme)).TotalSeconds;
        }
        window.Width = window.MinWidth;
        window.Height = window.MinHeight;
        window.PreviewTheme("halloween");
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
                previousVersionControlsVerified = true,
                presentationControlsVerified = true,
                compactControlsVisible = true,
                downloadControlsVerified = true,
                audioCodecVerified = true,
                audioDurationSeconds = audioDuration.TotalSeconds,
                dialogCancellationVerified = true,
                themes, themeAudioSeconds, seasonalCompact, calendarRolloverVerified = true, freshInstall, mouseEffects, mouseEffectsVerified = true, musicTransitionsVerified = true
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

    private static async Task<object> RenderAsync(Window window, string path)
    {
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);
        var content = (FrameworkElement)window.Content;
        content.UpdateLayout();
        var widthDip = content.ActualWidth;
        var heightDip = content.ActualHeight;
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
