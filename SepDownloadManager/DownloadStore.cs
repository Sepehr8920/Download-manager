using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SepDownloadManager
{
    // This class saves the download list to a simple text file,
    // so it remembers them the next time you open the app
    public class SavedDownload
    {
        public string Url;
        public string OutputPath;
        public int Parts;
        public long Total;
        public long Downloaded;
        public string State;
    }

    public static class DownloadStore
    {
        static string StorePath =>
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "SepDownloadManager",
                "downloads.txt");

        public static void Save(List<DownloadItem> items)
        {
            try
            {
                string dir = Path.GetDirectoryName(StorePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var sb = new StringBuilder();

                foreach (var item in items)
                {
                    // Format: one download per line, separated by |
                    sb.AppendLine(string.Join("|",
                        item.Url.ToString(),
                        item.OutputPath,
                        item.Parts.ToString(),
                        item.Total.ToString(),
                        item.Downloaded.ToString(),
                        item.State));
                }

                File.WriteAllText(StorePath, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
                // Ignore if save fails
            }
        }

        public static List<SavedDownload> LoadAll()
        {
            var list = new List<SavedDownload>();

            try
            {
                if (!File.Exists(StorePath))
                    return list;

                var lines = File.ReadAllLines(StorePath, Encoding.UTF8);

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var p = line.Split('|');

                    if (p.Length < 6)
                        continue;

                    list.Add(new SavedDownload
                    {
                        Url = p[0],
                        OutputPath = p[1],
                        Parts = int.Parse(p[2]),
                        Total = long.Parse(p[3]),
                        Downloaded = long.Parse(p[4]),
                        State = p[5]
                    });
                }
            }
            catch
            {
                // If reading fails, return empty list
            }

            return list;
        }
    }
}
