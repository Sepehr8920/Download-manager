using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SepDownloadManager
{
    public class SavedDownload
    {
        public string Url;
        public string OutputPath;
        public int Parts;
        public long Total;
        public long Downloaded;
        public string State;
        public string Priority = "Normal";
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
                    sb.AppendLine(string.Join("|",
                        item.Url.ToString(),
                        item.OutputPath,
                        item.Parts.ToString(),
                        item.Total.ToString(),
                        item.Downloaded.ToString(),
                        item.State,
                        item.Priority));
                }

                File.WriteAllText(StorePath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
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
                        State = p[5],
                        Priority = p.Length >= 7 ? p[6] : "Normal"
                    });
                }
            }
            catch { }

            return list;
        }
    }
}
