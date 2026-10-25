using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace PokemonRebornLauncher.UI;

/// <summary>Keeps the custom frame inside the current monitor's usable desktop.</summary>
internal sealed class WindowWorkArea
{
    private const int WmGetMinMaxInfo = 0x0024;
    private const int WmSettingChange = 0x001A;
    private const int WmDisplayChange = 0x007E;
    private const int WmWindowPosChanged = 0x0047;
    private const int WmDpiChanged = 0x02E0;
    private const int WmEnterSizeMove = 0x0231;
    private const int WmExitSizeMove = 0x0232;
    private const uint MonitorDefaultToNearest = 2;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private static readonly ConditionalWeakTable<Window, WindowWorkArea> Attachments = new();

    private readonly Window _window;
    private readonly double _requestedMinWidth;
    private readonly double _requestedMinHeight;
    private HwndSource? _source;
    private nint _handle;
    private bool _refreshQueued;
    private bool _refreshing;
    private bool _inSizeMove;
    private bool _closed;

    private WindowWorkArea(Window window)
    {
        _window = window;
        _requestedMinWidth = window.MinWidth;
        _requestedMinHeight = window.MinHeight;
        window.SourceInitialized += OnSourceInitialized;
        window.Loaded += OnLoaded;
        window.StateChanged += OnStateChanged;
        window.Closed += OnClosed;
        if (PresentationSource.FromVisual(window) is HwndSource source)
            InitializeSource(source);
    }

    internal static void Attach(Window window) => Attachments.GetValue(window, static value => new WindowWorkArea(value));

    internal static void VerifyBounds(Window window)
    {
        nint handle = new WindowInteropHelper(window).Handle;
        nint monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == 0 || !GetMonitorInfo(monitor, ref info) || !GetWindowRect(handle, out var bounds))
            throw new InvalidOperationException("Die Monitor-Arbeitsfläche konnte nicht geprüft werden.");
        if (bounds.Left < info.Work.Left - 1 || bounds.Top < info.Work.Top - 1 || bounds.Right > info.Work.Right + 1 || bounds.Bottom > info.Work.Bottom + 1)
            throw new InvalidOperationException("Launcher-Fenster liegt außerhalb der Arbeitsfläche und könnte die Taskleiste überdecken.");
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(_window) is HwndSource source)
            InitializeSource(source);
    }

    private void InitializeSource(HwndSource source)
    {
        if (_source is not null) return;
        _source = source;
        _handle = source.Handle;
        source.AddHook(WindowProc);
        // This runs before first presentation. Loaded repeats the bounds check after
        // WPF has applied CenterScreen/CenterOwner and the initial layout.
        Refresh(initial: true);
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => QueueRefresh();
    private void OnStateChanged(object? sender, EventArgs e) => QueueRefresh();

    private nint WindowProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        switch (message)
        {
            case WmGetMinMaxInfo:
                if (TryGetMonitor(out var monitor))
                {
                    var bounds = CalculateMaxBounds(monitor.Monitor, monitor.Work);
                    var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
                    info.MaxPosition = new NativePoint(bounds.Left, bounds.Top);
                    info.MaxSize = new NativePoint(bounds.Right, bounds.Bottom);
                    double scale = GetDpiForWindow(hwnd) / 96d;
                    if (scale > 0)
                    {
                        info.MinTrackSize = new NativePoint(
                            (int)Math.Min(Math.Ceiling(_requestedMinWidth * scale), bounds.Right),
                            (int)Math.Min(Math.Ceiling(_requestedMinHeight * scale), bounds.Bottom));
                    }
                    // WPF's original minimum must not force a larger maximized window.
                    info.MaxTrackSize = new NativePoint(bounds.Right, bounds.Bottom);
                    Marshal.StructureToPtr(info, lParam, false);
                    handled = true;
                }
                break;
            case WmEnterSizeMove:
                _inSizeMove = true;
                break;
            case WmExitSizeMove:
                _inSizeMove = false;
                QueueRefresh();
                break;
            case WmDpiChanged:
            case WmDisplayChange:
            case WmSettingChange:
            case WmWindowPosChanged:
                // Let WPF finish handling DPI/layout before converting pixel dimensions.
                if (!_refreshing) QueueRefresh();
                break;
        }
        return 0;
    }

    // The position is monitor-relative; dimensions remain physical pixels. Subtraction
    // (not Abs) also works for monitors left/above the primary and top/left taskbars.
    internal static NativeRect CalculateMaxBounds(NativeRect monitor, NativeRect work) => new()
    {
        Left = work.Left - monitor.Left,
        Top = work.Top - monitor.Top,
        Right = work.Right - work.Left,
        Bottom = work.Bottom - work.Top
    };

    private void QueueRefresh()
    {
        if (_closed || _refreshQueued || _handle == 0) return;
        _refreshQueued = true;
        _window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            _refreshQueued = false;
            if (!_closed) Refresh(initial: false);
        }));
    }

    private void Refresh(bool initial)
    {
        if (_refreshing || _closed || !TryGetMonitor(out var monitor)) return;
        uint dpi = GetDpiForWindow(_handle);
        if (dpi == 0) return;
        double scale = dpi / 96d;
        int workWidth = monitor.Work.Right - monitor.Work.Left;
        int workHeight = monitor.Work.Bottom - monitor.Work.Top;
        if (workWidth <= 0 || workHeight <= 0) return;

        _refreshing = true;
        try
        {
            // Only WPF properties use DIPs. Restore the intended minimum on a larger
            // monitor instead of permanently lowering it after visiting a small one.
            _window.SetCurrentValue(Window.MinWidthProperty, Math.Min(_requestedMinWidth, workWidth / scale));
            _window.SetCurrentValue(Window.MinHeightProperty, Math.Min(_requestedMinHeight, workHeight / scale));
            if (initial && _window.WindowState == WindowState.Normal)
            {
                if (_window.Width > workWidth / scale)
                    _window.SetCurrentValue(Window.WidthProperty, workWidth / scale);
                if (_window.Height > workHeight / scale)
                    _window.SetCurrentValue(Window.HeightProperty, workHeight / scale);
            }

            if (_window.WindowState == WindowState.Minimized || !GetWindowRect(_handle, out var current)) return;
            int width;
            int height;
            int left;
            int top;
            if (_window.WindowState == WindowState.Maximized)
            {
                left = monitor.Work.Left;
                top = monitor.Work.Top;
                width = workWidth;
                height = workHeight;
            }
            else
            {
                // Do not constrain the position while dragging across monitor edges.
                // The final check runs on WM_EXITSIZEMOVE with the new monitor/DPI.
                if (_inSizeMove) return;
                width = Math.Clamp(current.Right - current.Left,
                    (int)Math.Min(Math.Ceiling(_requestedMinWidth * scale), workWidth), workWidth);
                height = Math.Clamp(current.Bottom - current.Top,
                    (int)Math.Min(Math.Ceiling(_requestedMinHeight * scale), workHeight), workHeight);
                left = Math.Clamp(current.Left, monitor.Work.Left, monitor.Work.Right - width);
                top = Math.Clamp(current.Top, monitor.Work.Top, monitor.Work.Bottom - height);
            }
            if (current.Left != left || current.Top != top || current.Right - current.Left != width || current.Bottom - current.Top != height)
                SetWindowPos(_handle, 0, left, top, width, height, SwpNoZOrder | SwpNoActivate);
        }
        finally { _refreshing = false; }
    }

    private bool TryGetMonitor(out MonitorInfo info)
    {
        info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        nint monitor = MonitorFromWindow(_handle, MonitorDefaultToNearest);
        return monitor != 0 && GetMonitorInfo(monitor, ref info)
            && info.Work.Right > info.Work.Left && info.Work.Bottom > info.Work.Top;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        _source?.RemoveHook(WindowProc);
        _source = null;
        _handle = 0;
        _window.SourceInitialized -= OnSourceInitialized;
        _window.Loaded -= OnLoaded;
        _window.StateChanged -= OnStateChanged;
        _window.Closed -= OnClosed;
        Attachments.Remove(_window);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint(int x, int y) { public int X = x; public int Y = y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor, Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hwnd, out NativeRect rect);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);
}
