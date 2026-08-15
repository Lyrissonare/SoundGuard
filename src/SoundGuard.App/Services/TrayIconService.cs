using System.Drawing;
using System.Windows.Forms;
using SoundGuard.Core.Engine;

namespace SoundGuard.App.Services;

/// <summary>System tray icon with a right-click menu (show / mute / recover / exit) and balloon notifications.</summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _muteItem;

    public TrayIconService(Action showWindow, Action mute, Action recover, Action exit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("显示主界面", null, (_, _) => showWindow());
        menu.Items.Add(new ToolStripSeparator());

        _muteItem = new ToolStripMenuItem("静音");
        _muteItem.Click += (_, _) => mute();
        menu.Items.Add(_muteItem);

        var recoverItem = new ToolStripMenuItem("恢复音量");
        recoverItem.Click += (_, _) => recover();
        menu.Items.Add(recoverItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exit());

        _icon = new NotifyIcon
        {
            Icon = IconFactory.Create(),
            Text = "SoundGuard",
            Visible = true,
            ContextMenuStrip = menu,
        };

        _icon.DoubleClick += (_, _) => showWindow();
    }

    public void ShowBalloon(string title, string message, ProtectionState state)
    {
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = message;
        _icon.BalloonTipIcon = state == ProtectionState.Muted ? ToolTipIcon.Warning : ToolTipIcon.Info;
        _icon.ShowBalloonTip(3000);
    }

    public void SetMuted(bool muted) => _muteItem.Text = muted ? "已静音（点击恢复）" : "静音";

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
