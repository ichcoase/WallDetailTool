using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace WallDetailTool
{
    // User-wide settings apply to every drawing opened by this Windows user.
    internal static class PluginSettings
    {
        internal const double DefaultLineWidthMillimetres = 12.0;
        private const double MaximumLineWidthMillimetres = 1000.0;

        private static string SettingsPath
        {
            get
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(root, "WallDetailTool", "settings.ini");
            }
        }

        internal static double ReadLineWidthMillimetres()
        {
            return ReadLineWidthMillimetres(SettingsPath);
        }

        internal static double ReadLineWidthMillimetres(string path)
        {
            if (!File.Exists(path)) return DefaultLineWidthMillimetres;
            double value;
            if (!double.TryParse(File.ReadAllText(path, Encoding.UTF8).Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out value) || !ValidWidth(value))
                throw new InvalidOperationException("插件统一线筋宽度设置无效，请重新保存一个大于0的毫米值。");
            return value;
        }

        internal static void SaveLineWidthMillimetres(double value)
        {
            SaveLineWidthMillimetres(SettingsPath, value);
        }

        internal static void SaveLineWidthMillimetres(string path, double value)
        {
            if (!ValidWidth(value))
                throw new InvalidOperationException("插件统一线筋宽度须大于0且不超过1000毫米。");
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, value.ToString("R", CultureInfo.InvariantCulture), new UTF8Encoding(false));
        }

        private static bool ValidWidth(double value)
        {
            return Commands.Finite(value) && value > 0 && value <= MaximumLineWidthMillimetres;
        }
    }
}
