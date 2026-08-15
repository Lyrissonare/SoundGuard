using System.ComponentModel;
using System.Windows;
using SoundGuard.App.ViewModels;

namespace SoundGuard.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    // Minimizing hides to tray instead of sitting in the taskbar.
    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized)
            Hide();
    }

    // Closing the window hides to tray; the app is exited from the tray menu.
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
