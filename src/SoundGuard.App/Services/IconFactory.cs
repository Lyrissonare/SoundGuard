using System.Drawing;
using System.Drawing.Drawing2D;

namespace SoundGuard.App.Services;

/// <summary>Generates a simple tray icon at runtime so the repo needs no binary assets.</summary>
public static class IconFactory
{
    public static Icon Create()
    {
        var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var fill = new SolidBrush(Color.FromArgb(61, 214, 140));
            g.FillEllipse(fill, 2, 2, 28, 28);

            // Headphone-style arc.
            using var pen = new Pen(Color.FromArgb(18, 21, 26), 3f);
            g.DrawArc(pen, 9, 9, 14, 14, -60, 300);
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }
}
