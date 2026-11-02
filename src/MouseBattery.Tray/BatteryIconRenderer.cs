using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using G3Power.Core;
using Microsoft.Win32;

namespace G3Power.Tray
{
    internal static class BatteryIconRenderer
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static Icon Create(int percent, bool hasValue, bool connected, IconStyle style, int size)
        {
            if (size < 16) size = 16;

            using (Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    graphics.Clear(Color.Transparent);

                    if (style == IconStyle.Battery)
                        DrawBattery(graphics, size, percent, hasValue, connected);
                    else
                        DrawNumber(graphics, size, percent, hasValue, connected);
                }

                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon temporary = Icon.FromHandle(handle))
                    {
                        return (Icon)temporary.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }

        private static void DrawNumber(Graphics graphics, int size, int percent, bool hasValue, bool connected)
        {
            Color background = GetLevelColor(percent, hasValue, connected);
            Color foreground = GetTextColor(background);

            float inset = Math.Max(0.5f, size * 0.03f);
            RectangleF bounds = new RectangleF(inset, inset, size - inset * 2f, size - inset * 2f);

            using (GraphicsPath path = CreateRoundedRectangle(bounds, size * 0.20f))
            {
                using (SolidBrush brush = new SolidBrush(background))
                {
                    graphics.FillPath(brush, path);
                }

                using (Pen pen = new Pen(Color.FromArgb(70, 0, 0, 0), 1f))
                {
                    graphics.DrawPath(pen, path);
                }
            }

            string text;
            if (hasValue) text = percent.ToString();
            else text = connected ? "?" : "--";

            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                format.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip;

                float fontSize = size * 0.80f;
                float widthLimit = text.Length >= 3 ? size * 0.72f : size * 0.80f;
                Font font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);

                SizeF limit = new SizeF(size * 4f, size * 4f);
                SizeF measured = graphics.MeasureString(text, font, limit, format);

                int guard = 0;
                while (measured.Width > widthLimit && guard < 16)
                {
                    fontSize *= 0.92f;
                    font.Dispose();
                    font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                    measured = graphics.MeasureString(text, font, limit, format);
                    guard++;
                }

                using (font)
                using (SolidBrush textBrush = new SolidBrush(foreground))
                {
                    RectangleF layout = new RectangleF(0f, -size * 0.03f, size, size);
                    graphics.DrawString(text, font, textBrush, layout, format);
                }
            }
        }

        private static void DrawBattery(Graphics graphics, int size, int percent, bool hasValue, bool connected)
        {
            float unit = size / 24f;

            RectangleF body = new RectangleF(1.6f * unit, 5.0f * unit, 18.8f * unit, 14.0f * unit);
            float bodyRadius = 3.2f * unit;
            float stroke = Math.Max(1f, 1.7f * unit);

            RectangleF nub = new RectangleF(20.4f * unit, 10.2f * unit, 2.2f * unit, 3.6f * unit);

            Color shell = GetShellColor(connected);
            Color level = GetLevelColor(percent, hasValue, connected);

            using (GraphicsPath nubPath = CreateRoundedRectangle(nub, 0.9f * unit))
            using (SolidBrush nubBrush = new SolidBrush(shell))
            {
                graphics.FillPath(nubBrush, nubPath);
            }

            using (GraphicsPath bodyPath = CreateRoundedRectangle(body, bodyRadius))
            using (Pen pen = new Pen(shell, stroke))
            {
                graphics.DrawPath(pen, bodyPath);
            }

            if (!connected || !hasValue || percent <= 0) return;

            float padding = Math.Max(0.7f, stroke * 0.7f);
            RectangleF inner = new RectangleF(
                body.X + padding,
                body.Y + padding,
                body.Width - padding * 2f,
                body.Height - padding * 2f);

            float fillWidth = inner.Width * (percent / 100f);
            if (fillWidth < 0.8f) fillWidth = 0.8f;

            RectangleF fill = new RectangleF(inner.X, inner.Y, fillWidth, inner.Height);
            using (GraphicsPath fillPath = CreateRoundedRectangle(fill, Math.Max(0.4f, 0.9f * unit)))
            using (SolidBrush fillBrush = new SolidBrush(level))
            {
                graphics.FillPath(fillBrush, fillPath);
            }
        }

        private static Color GetShellColor(bool connected)
        {
            if (IsLightTaskbar())
                return connected ? Color.FromArgb(96, 102, 108) : Color.FromArgb(150, 130, 136, 142);

            return connected ? Color.White : Color.FromArgb(150, 255, 255, 255);
        }

        private static bool IsLightTaskbar()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
                {
                    if (key == null) return false;

                    object value = key.GetValue("SystemUsesLightTheme");
                    if (value is int) return (int)value == 1;
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        private static GraphicsPath CreateRoundedRectangle(RectangleF bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath();

            float limit = Math.Min(bounds.Width, bounds.Height) / 2f;
            if (radius > limit) radius = limit;

            if (radius <= 0.1f)
            {
                path.AddRectangle(bounds);
                return path;
            }

            float diameter = radius * 2f;
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180f, 90f);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270f, 90f);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0f, 90f);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90f, 90f);
            path.CloseFigure();
            return path;
        }

        public static Color GetLevelColor(int percent, bool hasValue, bool connected)
        {
            if (!connected) return Color.FromArgb(127, 140, 141);
            if (!hasValue) return Color.FromArgb(127, 140, 141);
            if (percent >= 50) return Color.FromArgb(46, 204, 113);
            if (percent <= 20) return Color.FromArgb(231, 76, 60);
            return Color.FromArgb(241, 196, 15);
        }

        private static Color GetTextColor(Color background)
        {
            int luminance = (background.R * 299 + background.G * 587 + background.B * 114) / 1000;
            return luminance > 150 ? Color.FromArgb(33, 33, 33) : Color.White;
        }
    }
}
