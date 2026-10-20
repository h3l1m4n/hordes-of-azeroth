using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace HordesOfAzeroth.Launcher
{
    static class WindowNative
    {
        const int DarkTitleBar = 20;
        const int CaptionColor = 35;

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr icon);

        public static void UseDarkTitleBar(Window window, System.Windows.Media.Color caption)
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            int on = 1;
            DwmSetWindowAttribute(handle, DarkTitleBar, ref on, sizeof(int));
            int colour = caption.R | caption.G << 8 | caption.B << 16;
            DwmSetWindowAttribute(handle, CaptionColor, ref colour, sizeof(int));
        }

        public static Icon LoadOwnIcon(int size)
        {
            System.Windows.Resources.StreamResourceInfo resource = Application.GetResourceStream(new Uri("pack://application:,,,/art/icon.ico"));
            if (resource == null)
                return CreateIcon(size);
            using (resource.Stream)
                return new Icon(resource.Stream, size, size);
        }

        public static Icon CreateIcon(int size)
        {
            using (Bitmap bitmap = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    RectangleF orb = new RectangleF(size * 0.06f, size * 0.06f, size * 0.88f, size * 0.88f);
                    using (GraphicsPath path = new GraphicsPath())
                    {
                        path.AddEllipse(orb);
                        using (PathGradientBrush fill = new PathGradientBrush(path))
                        {
                            fill.CenterPoint = new PointF(size * 0.5f, size * 0.38f);
                            fill.CenterColor = Color.FromArgb(255, 255, 217, 119);
                            fill.SurroundColors = new[] { Color.FromArgb(255, 124, 26, 20) };
                            g.FillPath(fill, path);
                        }
                    }
                    using (Pen rim = new Pen(Color.FromArgb(255, 40, 14, 10), Math.Max(1f, size / 16f)))
                        g.DrawEllipse(rim, orb);
                }
                IntPtr handle = bitmap.GetHicon();
                Icon icon = (Icon)Icon.FromHandle(handle).Clone();
                DestroyIcon(handle);
                return icon;
            }
        }

        public static BitmapSource ToImageSource(Icon icon)
        {
            return Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }
    }
}
