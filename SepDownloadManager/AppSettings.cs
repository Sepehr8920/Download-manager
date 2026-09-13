using System;
using System.IO;
using System.Text;

namespace SepDownloadManager
{
    // Saves user settings like max connections
    public static class AppSettings
    {
        static string SettingsPath =>
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "SepDownloadManager",
                "settings.txt");

        // Default: 8 connections
        public static int MaxConnections
        {
            get
            {
                try
                {
                    if (File.Exists(SettingsPath))
                    {
                        var text = File.ReadAllText(SettingsPath, Encoding.UTF8);
                        if (int.TryParse(text.Trim(), out int val) &&
                            val >= 1 && val <= 16)
                            return val;
                    }
                }
                catch { }

                return 8;
            }
            set
            {
                try
                {
                    string dir = Path.GetDirectoryName(SettingsPath);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    int v = Math.Max(1, Math.Min(16, value));
                    File.WriteAllText(SettingsPath, v.ToString(), Encoding.UTF8);
                }
                catch { }
            }
        }
    }
}
