using System.Windows;
using System.Windows.Threading;
using SoundGuard.App.ViewModels;
using SoundGuard.Core.Audio;
using SoundGuard.Core.Config;
using SoundGuard.Core.Engine;
using SoundGuard.Core.Logging;
using SoundGuard.Core.System;

namespace SoundGuard.App.Services;

/// <summary>
/// Composes the whole application: builds the engine from the real WASAPI/NAudio backends, wires it
/// to the view model and tray icon, and manages window lifetime, game-mode polling and autostart.
/// </summary>
public sealed class AppController : IDisposable
{
    private readonly AppConfig _config;
    private readonly ConfigStore _store;
    private readonly MasterVolumeController _audio;
    private readonly WasapiLoopbackCaptureSource _capture;
    private readonly ProtectionEngine _engine;
    private readonly ProtectionLogger _logger;
    private readonly MainViewModel _vm;
    private readonly DispatcherTimer _gameModeTimer;

    private MainWindow? _window;
    private TrayIconService? _tray;
    private ProtectionState _lastState = ProtectionState.Safe;
    private bool _disposed;

    public AppController(AppConfig config, ConfigStore store)
    {
        _config = config;
        _store = store;

        _audio = new MasterVolumeController();
        _capture = new WasapiLoopbackCaptureSource();
        _engine = new ProtectionEngine(config.Protection, _audio, _capture);
        _logger = new ProtectionLogger();
        _vm = new MainViewModel(config.Protection);

        _engine.Updated += OnUpdated;
        _engine.NotificationRequested += OnNotification;
        _engine.StateChanged += OnStateChanged;

        WireCommands();

        _gameModeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _gameModeTimer.Tick += (_, _) =>
        {
            bool fullscreen = FullscreenDetector.IsForegroundFullscreen();
            if (_vm.IsGameMode != fullscreen)
            {
                _vm.IsGameMode = fullscreen;
                _engine.SetGameMode(fullscreen);
            }
        };
    }

    public void Start()
    {
        Autostart.SetEnabled(_config.StartWithWindows);

        try
        {
            _engine.Start();
            _vm.FormatText = _engine.Format.ToString();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"音频捕获启动失败：{ex.Message}", "SoundGuard", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        _tray = new TrayIconService(ShowMainWindow, _engine.MuteForUser, _engine.RecoverNow, Exit);
        _gameModeTimer.Start();
    }

    public void ShowMainWindow()
    {
        _window ??= new MainWindow(_vm);
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void WireCommands()
    {
        _vm.ToggleBypassCommand = new RelayCommand(_ =>
        {
            _vm.IsBypass = !_vm.IsBypass;
            _engine.Bypass = _vm.IsBypass;
        });
        _vm.MuteCommand = new RelayCommand(_ => _engine.MuteForUser());
        _vm.RecoverCommand = new RelayCommand(_ => _engine.RecoverNow());
        _vm.SaveSettingsCommand = new RelayCommand(_ => _store.Save(_config));
    }

    private void OnUpdated(AnalysisResult result)
    {
        // Raised on the analysis thread; marshal to the UI dispatcher.
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _vm.Apply(result);

            // Log protection triggers on state transitions (not every block).
            if (result.State != _lastState && result.State != ProtectionState.Bypass)
            {
                _lastState = result.State;
                _logger.Log(new ProtectionEvent(
                    DateTime.UtcNow,
                    ForegroundWindow.GetProcessName(),
                    result.ShortTermLufs,
                    result.TruePeakDb,
                    result.State,
                    $"{(result.DominantStage == LimiterStage.Peak ? "peak" : "loudness")} GR {result.GainReductionDb:F1} dB"));
            }
        });
    }

    private void OnNotification(string title, string message, ProtectionState state)
    {
        Application.Current?.Dispatcher.BeginInvoke(() => _tray?.ShowBalloon(title, message, state));
    }

    private void OnStateChanged(ProtectionState state)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _tray?.SetMuted(state == ProtectionState.Muted);
            _vm.SetProtectionState(state);
        });
    }

    private void Exit()
    {
        Dispose();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _gameModeTimer.Stop();
        _engine.Dispose();
        _tray?.Dispose();
        _store.Save(_config);
    }
}
