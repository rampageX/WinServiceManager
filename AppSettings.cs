using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace WinServMgr
{
    internal sealed class AppSettings
    {
        private readonly string path;
        internal Rectangle MainBounds;
        internal Rectangle DetailsBounds;
        internal bool MainMaximized;
        internal bool DetailsMaximized;
        internal bool ShowStopped = true;
        internal bool FavoritesOnly;
        internal readonly HashSet<string> Favorites =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal AppSettings(string filePath)
        {
            path = filePath;
        }

        internal static AppSettings Load(string filePath)
        {
            var settings = new AppSettings(filePath);
            if (!File.Exists(filePath)) return settings;
            string section = "";
            foreach (string line in File.ReadAllLines(filePath, Encoding.UTF8))
            {
                string text = line.Trim();
                if (text.Length == 0 || text.StartsWith(";") || text.StartsWith("#")) continue;
                if (text.StartsWith("[") && text.EndsWith("]"))
                {
                    section = text.Substring(1, text.Length - 2);
                    continue;
                }
                int separator = text.IndexOf('=');
                if (separator < 0) continue;
                string key = text.Substring(0, separator).Trim();
                string value = text.Substring(separator + 1).Trim();
                bool enabled;
                switch (section.ToLowerInvariant())
                {
                    case "windows":
                        if (key.Equals("MainBounds", StringComparison.OrdinalIgnoreCase))
                            settings.MainBounds = ParseBounds(value);
                        else if (key.Equals("DetailsBounds", StringComparison.OrdinalIgnoreCase))
                            settings.DetailsBounds = ParseBounds(value);
                        else if (key.Equals("MainMaximized", StringComparison.OrdinalIgnoreCase) &&
                            bool.TryParse(value, out enabled)) settings.MainMaximized = enabled;
                        else if (key.Equals("DetailsMaximized", StringComparison.OrdinalIgnoreCase) &&
                            bool.TryParse(value, out enabled)) settings.DetailsMaximized = enabled;
                        break;
                    case "filters":
                        if (key.Equals("ShowStopped", StringComparison.OrdinalIgnoreCase) &&
                            bool.TryParse(value, out enabled)) settings.ShowStopped = enabled;
                        else if (key.Equals("FavoritesOnly", StringComparison.OrdinalIgnoreCase) &&
                            bool.TryParse(value, out enabled)) settings.FavoritesOnly = enabled;
                        break;
                    case "favorites":
                        if (value != "1" || key.Length == 0) break;
                        try
                        {
                            string name = Uri.UnescapeDataString(key);
                            if (name.Length > 0 && name.Length <= 256 &&
                                !name.Any(char.IsControl))
                                settings.Favorites.Add(name);
                        }
                        catch (UriFormatException) { }
                        break;
                }
            }
            return settings;
        }

        internal void Save()
        {
            var lines = new List<string> {
                "; WinServiceManager portable settings",
                "; Bounds = X,Y,Width,Height (normal window)",
                "[Windows]",
                "MainBounds=" + FormatBounds(MainBounds),
                "DetailsBounds=" + FormatBounds(DetailsBounds),
                "MainMaximized=" + MainMaximized,
                "DetailsMaximized=" + DetailsMaximized,
                "",
                "[Filters]",
                "ShowStopped=" + ShowStopped,
                "FavoritesOnly=" + FavoritesOnly,
                "",
                "[Favorites]",
                "; ServiceName=1 (escaped if necessary)"
            };
            foreach (string name in Favorites.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                lines.Add(Uri.EscapeDataString(name) + "=1");

            // Write on the same volume and replace atomically. Never truncate
            // the previous settings if writing the new file fails.
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllLines(temp, lines, new UTF8Encoding(true));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        internal static Rectangle ParseBounds(string value)
        {
            string[] parts = value.Split(',');
            int x, y, w, h;
            if (parts.Length != 4 ||
                !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y) ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out w) ||
                !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out h) ||
                w <= 0 || h <= 0 || w > 32768 || h > 32768 ||
                x < -100000 || x > 100000 || y < -100000 || y > 100000)
                return Rectangle.Empty;
            return new Rectangle(x, y, w, h);
        }

        private static string FormatBounds(Rectangle bounds)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}",
                bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }

        internal static Rectangle FitToScreen(Rectangle desired, Size minimum, Rectangle fallback)
        {
            Screen monitor = Screen.AllScreens.FirstOrDefault(s =>
                Rectangle.Intersect(s.WorkingArea, desired).Width >= 64 &&
                Rectangle.Intersect(s.WorkingArea, desired).Height >= 64);
            if (monitor == null)
            {
                monitor = Screen.FromRectangle(fallback);
                desired = new Rectangle(fallback.Location, desired.Size);
            }
            Rectangle area = monitor.WorkingArea;
            int width = Math.Max(minimum.Width, Math.Min(desired.Width, area.Width));
            int height = Math.Max(minimum.Height, Math.Min(desired.Height, area.Height));
            return new Rectangle(
                Math.Max(area.Left, Math.Min(desired.X, area.Right - width)),
                Math.Max(area.Top, Math.Min(desired.Y, area.Bottom - height)),
                width, height);
        }

        internal static void RestoreWindow(Form form, Rectangle bounds,
            bool maximized, Rectangle fallback)
        {
            if (!bounds.IsEmpty)
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Bounds = FitToScreen(bounds, form.MinimumSize, fallback);
            }
            if (maximized) form.WindowState = FormWindowState.Maximized;
        }

        internal static Rectangle CaptureBounds(Form form)
        {
            return form.WindowState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;
        }
    }
}
