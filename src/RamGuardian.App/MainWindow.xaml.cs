using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using RamGuardian.Core.Cleaning;
using RamGuardian.Core.Engine;
using RamGuardian.Core.Policy;
using RamGuardian.Core.Telemetry;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace RamGuardian.App;

public partial class MainWindow : Window, IDisposable
{
    private static readonly TimeSpan ForegroundPollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan BackgroundAutoPollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan BackgroundIdlePollInterval = TimeSpan.FromSeconds(8);
    private static readonly System.Windows.Media.Brush OffBrush = CreateBrush("#8F1F34");
    private static readonly System.Windows.Media.Brush OnBrush = CreateBrush("#1D7A47");
    private static readonly System.Windows.Media.Brush ExitBrush = CreateBrush("#701726");
    private static readonly AutoCleanSettings AutoCleanSettings = AutoCleanSettings.Default;

    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly SemaphoreSlim _cleanupGate = new(1, 1);
    private readonly DispatcherTimer _pollTimer;
    private readonly AppStateStore _stateStore;
    private readonly WindowsForegroundActivityDetector _foregroundDetector;
    private readonly WindowsMemoryCleanupExecutor _cleanupExecutor;
    private readonly WindowsMemoryTelemetryReader _telemetryReader;
    private readonly Forms.NotifyIcon _trayIcon;
    private bool _autoCleanEnabled;
    private bool _disposed;
    private bool _isCleaning;
    private bool _isExitRequested;
    private DateTimeOffset? _lastCleanupAt;
    private MemorySnapshot? _lastSnapshot;
    private DateTimeOffset? _pressureStartedAt;
    private int _refreshInFlight;

    public MainWindow()
    {
        InitializeComponent();

        _stateStore = new AppStateStore();
        _autoCleanEnabled = _stateStore.Load().AutoCleanEnabled;

        _telemetryReader = new WindowsMemoryTelemetryReader();
        _cleanupExecutor = new WindowsMemoryCleanupExecutor(_telemetryReader);
        _foregroundDetector = new WindowsForegroundActivityDetector();

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = Drawing.SystemIcons.Shield,
            Text = "RamGuardian",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu(),
        };
        _trayIcon.DoubleClick += (_, _) => ShowFromTray();

        _pollTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = ForegroundPollInterval,
        };
        _pollTimer.Tick += OnPollTimerTick;

        Loaded += OnLoaded;
        StateChanged += OnWindowStateChanged;

        UpdateVisualState();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pollTimer.Stop();
        _shutdownCts.Cancel();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _telemetryReader.Dispose();
        _cleanupGate.Dispose();
        _shutdownCts.Dispose();
    }

    private static System.Windows.Media.Brush CreateBrush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateTimerInterval();
        _pollTimer.Start();
        _ = RefreshSnapshotAsync(runAutoClean: false);
    }

    private async void OnPollTimerTick(object? sender, EventArgs e)
    {
        await RefreshSnapshotAsync(runAutoClean: true);
    }

    private async void OnCleanRamClicked(object sender, RoutedEventArgs e)
    {
        if (_isCleaning)
        {
            return;
        }

        try
        {
            var snapshot = _telemetryReader.CaptureSnapshot();
            var plan = RamGuardianPolicy.CreateManualCleanPlan(snapshot, _foregroundDetector.Detect());
            await ExecuteCleanupAsync(plan);
        }
        catch (Exception ex)
        {
            ShowTelemetryError(ex);
        }
    }

    private void OnAutoCleanClicked(object sender, RoutedEventArgs e)
    {
        _autoCleanEnabled = !_autoCleanEnabled;
        PersistState();
        UpdateVisualState();
        UpdateTrayText(_lastSnapshot);
        UpdateTimerInterval();
    }

    private void OnExitClicked(object sender, RoutedEventArgs e)
    {
        ExitApplication();
    }

    private void OnWindowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
            // Ignore drag failures triggered during rapid state changes.
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            HideToTray();
        }
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_isExitRequested)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();

        var openItem = new Forms.ToolStripMenuItem("Open");
        openItem.Click += (_, _) => ShowFromTray();

        var exitItem = new Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => Dispatcher.Invoke(ExitApplication);

        menu.Items.Add(openItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        return menu;
    }

    private async Task RefreshSnapshotAsync(bool runAutoClean)
    {
        if (_disposed || _isExitRequested)
        {
            return;
        }

        if (Interlocked.Exchange(ref _refreshInFlight, 1) == 1)
        {
            return;
        }

        try
        {
            var snapshot = _telemetryReader.CaptureSnapshot();
            var now = snapshot.CapturedAt;
            var underPressure = IsPressurePresent(snapshot);

            _pressureStartedAt = underPressure
                ? _pressureStartedAt ?? now
                : null;

            _lastSnapshot = snapshot;
            UpdateUsage(snapshot);

            if (!runAutoClean || !_autoCleanEnabled || _isCleaning)
            {
                return;
            }

            var sustainedPressureDuration = _pressureStartedAt.HasValue
                ? now - _pressureStartedAt.Value
                : TimeSpan.Zero;

            var plan = RamGuardianPolicy.EvaluateAutoClean(
                snapshot,
                new AutoCleanContext(
                    Now: now,
                    SustainedPressureDuration: sustainedPressureDuration,
                    LastCleanupAt: _lastCleanupAt,
                    Foreground: _foregroundDetector.Detect(),
                    CleanupInProgress: _isCleaning),
                AutoCleanSettings);

            if (plan.Mode != CleanupMode.None)
            {
                await ExecuteCleanupAsync(plan);
            }
        }
        catch (Exception ex)
        {
            ShowTelemetryError(ex);
        }
        finally
        {
            Interlocked.Exchange(ref _refreshInFlight, 0);
        }
    }

    private async Task ExecuteCleanupAsync(CleanupPlan plan)
    {
        if (_disposed || _isExitRequested)
        {
            return;
        }

        if (!await _cleanupGate.WaitAsync(0, _shutdownCts.Token))
        {
            return;
        }

        try
        {
            _isCleaning = true;
            UpdateVisualState();
            UpdateTrayText(_lastSnapshot);

            var result = await Task.Run(
                () => _cleanupExecutor.Execute(plan, _shutdownCts.Token),
                _shutdownCts.Token);

            _lastCleanupAt = result.After.CapturedAt;
            _lastSnapshot = result.After;
            UpdateUsage(result.After);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ShowTelemetryError(ex);
        }
        finally
        {
            _isCleaning = false;
            _cleanupGate.Release();
            UpdateVisualState();
            UpdateTrayText(_lastSnapshot);
            UpdateTimerInterval();
        }
    }

    private bool IsPressurePresent(MemorySnapshot snapshot)
    {
        var minimumAvailableBytes = Math.Max(
            AutoCleanSettings.MinimumAvailableBytes,
            (ulong)Math.Round(snapshot.TotalPhysicalBytes * AutoCleanSettings.MinimumAvailableRatio));

        return snapshot.LowMemoryResourceSignaled ||
               snapshot.AvailablePhysicalBytes <= minimumAvailableBytes ||
               snapshot.UsedCommitRatio >= AutoCleanSettings.CommitPressureRatio;
    }

    private void UpdateUsage(MemorySnapshot snapshot)
    {
        var used = FormatBytes(snapshot.UsedPhysicalBytes);
        var total = FormatBytes(snapshot.TotalPhysicalBytes);

        UsageTextBlock.Text = $"Current ram usage: {used} / {total} ({snapshot.MemoryLoadPercent}%)";
        UpdateTrayText(snapshot);
    }

    private void UpdateTrayText(MemorySnapshot? snapshot)
    {
        if (_disposed)
        {
            return;
        }

        var state = _isCleaning
            ? "Cleaning"
            : _autoCleanEnabled
                ? "Auto On"
                : "Auto Off";
        var usagePart = snapshot is null ? "--" : $"{snapshot.MemoryLoadPercent}% RAM";
        _trayIcon.Text = TrimTrayText($"RamGuardian | {usagePart} | {state}");
    }

    private static string TrimTrayText(string text)
    {
        const int maxLength = 63;
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private void UpdateVisualState()
    {
        CleanRamButton.Content = _isCleaning ? "Cleaning" : "Clean Ram";
        CleanRamButton.Background = _isCleaning ? OnBrush : OffBrush;
        CleanRamButton.IsEnabled = !_isCleaning && !_isExitRequested;

        AutoCleanButton.Content = _autoCleanEnabled ? "Auto-Clean On" : "Auto-Clean Off";
        AutoCleanButton.Background = _autoCleanEnabled ? OnBrush : OffBrush;
        AutoCleanButton.IsEnabled = !_isCleaning && !_isExitRequested;

        ExitButton.Background = ExitBrush;
        ExitButton.IsEnabled = !_isCleaning;
    }

    private void HideToTray()
    {
        ShowInTaskbar = false;
        Hide();
        UpdateTimerInterval();
    }

    private void ShowFromTray()
    {
        if (_disposed)
        {
            return;
        }

        void RestoreWindow()
        {
            ShowInTaskbar = true;
            Show();
            WindowState = WindowState.Normal;
            Activate();
            UpdateTimerInterval();
        }

        if (Dispatcher.CheckAccess())
        {
            RestoreWindow();
            return;
        }

        Dispatcher.Invoke(RestoreWindow);
    }

    private void PersistState()
    {
        _stateStore.Save(new AppState(_autoCleanEnabled));
    }

    private void UpdateTimerInterval()
    {
        if (_disposed)
        {
            return;
        }

        var runningInBackground = !IsVisible || !ShowInTaskbar;

        _pollTimer.Interval = runningInBackground
            ? _autoCleanEnabled
                ? BackgroundAutoPollInterval
                : BackgroundIdlePollInterval
            : ForegroundPollInterval;
    }

    private void ExitApplication()
    {
        if (_isExitRequested)
        {
            return;
        }

        _isExitRequested = true;
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private void ShowTelemetryError(Exception ex)
    {
        UsageTextBlock.Text = "Current ram usage: unavailable";
        _trayIcon.Text = TrimTrayText("RamGuardian | telemetry unavailable");
        System.Diagnostics.Debug.WriteLine(ex);
    }

    private static string FormatBytes(ulong bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024d && unitIndex < units.Length - 1)
        {
            size /= 1024d;
            unitIndex += 1;
        }

        return unitIndex == 0 ? $"{size:0} {units[unitIndex]}" : $"{size:0.0} {units[unitIndex]}";
    }
}
