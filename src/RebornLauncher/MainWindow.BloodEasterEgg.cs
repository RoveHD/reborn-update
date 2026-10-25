using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace PokemonRebornLauncher;

public partial class MainWindow
{
    private static readonly TimeSpan BloodClickWindow = TimeSpan.FromSeconds(4);
    private readonly Stopwatch _bloodClickClock = Stopwatch.StartNew();
    private TimeSpan _bloodFirstClick;
    private int _bloodClickCount;
    private bool _bloodEasterEggReady;

    private void InitializeBloodEasterEgg()
    {
        if (_bloodEasterEggReady) return;
        _bloodEasterEggReady = true;
        Activated += (_, _) => UpdateBloodEasterEggActivity();
        Deactivated += (_, _) => UpdateBloodEasterEggActivity();
        StateChanged += (_, _) => UpdateBloodEasterEggActivity();
        IsVisibleChanged += (_, _) => UpdateBloodEasterEggActivity();
        ReduceMotionCheckBox.Checked += OnBloodMotionSettingChanged;
        ReduceMotionCheckBox.Unchecked += OnBloodMotionSettingChanged;
        SystemParameters.StaticPropertyChanged += OnBloodSystemPreferenceChanged;
        Closed += OnBloodWindowClosed;
        UpdateBloodEasterEggActivity();
    }

    private void LogoBlood_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_bloodEasterEggReady || !CanShowBloodEasterEgg())
        {
            ResetBloodClickSequence();
            return;
        }

        var now = _bloodClickClock.Elapsed;
        if (_bloodClickCount == 0 || now - _bloodFirstClick > BloodClickWindow)
        {
            _bloodFirstClick = now;
            _bloodClickCount = 1;
            return;
        }

        _bloodClickCount++;
        if (_bloodClickCount < 5) return;
        ResetBloodClickSequence();

        Point origin;
        try
        {
            origin = LogoFrame.TranslatePoint(
                new Point(LogoFrame.ActualWidth / 2, LogoFrame.ActualHeight / 2), BloodView);
        }
        catch (InvalidOperationException)
        {
            return;
        }

        BloodView.Trigger(origin);
        e.Handled = true;
    }

    private void UpdateBloodEasterEggActivity()
    {
        if (!_bloodEasterEggReady) return;
        bool active = CanShowBloodEasterEgg();
        BloodView.Configure(active, IsMotionReduced);
        if (!active) ResetBloodClickSequence();
    }

    private bool CanShowBloodEasterEgg() => IsVisible && IsActive && WindowState != WindowState.Minimized
        && !_presentationClosed && !_gameRunning;

    private void OnBloodMotionSettingChanged(object sender, RoutedEventArgs e) => UpdateBloodEasterEggActivity();

    private void OnBloodSystemPreferenceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation))
            Dispatcher.Invoke(UpdateBloodEasterEggActivity);
    }

    private void OnBloodWindowClosed(object? sender, EventArgs e)
    {
        ResetBloodClickSequence();
        BloodView.Configure(active: false, reduceMotion: IsMotionReduced);
        ReduceMotionCheckBox.Checked -= OnBloodMotionSettingChanged;
        ReduceMotionCheckBox.Unchecked -= OnBloodMotionSettingChanged;
        SystemParameters.StaticPropertyChanged -= OnBloodSystemPreferenceChanged;
    }

    private void ResetBloodClickSequence()
    {
        _bloodClickCount = 0;
        _bloodFirstClick = TimeSpan.Zero;
    }
}
