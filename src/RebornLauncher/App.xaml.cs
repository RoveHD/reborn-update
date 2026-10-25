using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PokemonRebornLauncher.Services;

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
                File.WriteAllText(errorPath, JsonSerializer.Serialize(new { success = false, error = ex.Message }, new JsonSerializerOptions { WriteIndented = true }));
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
        var large = await RenderAsync(window, Path.Combine(fullOutput, "launcher-1280x800.png"));
        window.Width = 960;
        window.Height = 600;
        window.WindowState = WindowState.Normal;
        await Dispatcher.InvokeAsync(() => window.UpdateLayout());
        var compact = await RenderAsync(window, Path.Combine(fullOutput, "launcher-minimum.png"));
        window.PreviewPreviousVersion();
        window.Width = 1280 + frameWidth;
        window.Height = 800 + frameHeight;
        await Dispatcher.InvokeAsync(() => window.UpdateLayout());
        var previous = await RenderAsync(window, Path.Combine(fullOutput, "launcher-previous.png"));
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
                screenshots = new { large, minimum = compact, previous },
                previousVersionControlsVerified = true
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
