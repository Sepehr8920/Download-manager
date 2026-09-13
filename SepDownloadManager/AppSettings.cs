using System;
using System.IO;
using System.Text;

namespace SepDownloadManager
{
    // Saves user settings
    public static class AppSettings
    {
        static string SettingsPath =>
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "SepDownloadManager",
                "settings.txt");

        // Max parts per download (default 8)
        public static int MaxConnections
        {
            get => ReadValue("parts", 8, 1, 16);
            set => WriteValue("parts", Math.Max(1, Math.Min(16, value)));
        }

        // Max simultaneous downloads (default 2)
        public static int MaxSimultaneous
        {
            get => ReadValue("simul", 2, 1, 10);
            set => WriteValue("simul", Math.Max(1, Math.Min(10, value)));
        }

        // ------- simple key=value store -------

        static int ReadValue(string key, int def, int min, int max)
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    foreach (var line in File.ReadAllLines(SettingsPath, Encoding.UTF8))
                    {
                        var p = line.Split('=');
                        if (p.Length == 2 && p[0] == key &&
                            int.TryParse(p[1], out int v) &&
                            v >= min && v <= max)
                            return v;
                    }
                }
            }
            catch { }
            return def;
        }

        static void WriteValue(string key, int value)
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var dict = new System.Collections.Generic.Dictionary<string, string>();

                if (File.Exists(SettingsPath))
                {
                    foreach (var line in File.ReadAllLines(SettingsPath, Encoding.UTF8))
                    {
                        var p = line.Split('=');
                        if (p.Length == 2) dict[p[0]] = p[1];
                    }
                }

                dict[key] = value.ToString();

                var sb = new StringBuilder();
                foreach (var kv in dict)
                    sb.AppendLine(kv.Key + "=" + kv.Value);

                File.WriteAllText(SettingsPath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }
}
