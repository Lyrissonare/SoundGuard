using System.Threading;
using System.Windows;
using SoundGuard.App.Services;
using SoundGuard.Core.Config;

namespace SoundGuard.App;

public partial class App : Application
{
    private Mutex? _mutex;
    private AppController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, "SoundGuard.SingleInstance", out bool isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show("SoundGuard 已在运行。", "SoundGuard", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var store = new ConfigStore();
        AppConfig config = store.Load();

        _controller = new AppController(config, store);
        _controller.Start();

        // Show the main window on startup; minimize-to-tray is opt-in via config.
        if (!config.MinimizeToTray)
            _controller.ShowMainWindow();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        _mutex?.ReleaseMutex();
        base.OnExit(e);
    }
}
