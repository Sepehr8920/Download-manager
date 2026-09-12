using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SepDownloadManager
{
    public class DownloadItem
    {
        public Uri Url;
        public string OutputPath;
        public int Parts;

        public int Row = -1;

        public long Total = -1;
        public long Downloaded;
        public double Speed;

        public string State = "Waiting";

        CancellationTokenSource cts;

        DateTime lastTime = DateTime.UtcNow;
        long lastBytes;

        string TempDir => OutputPath + ".parts";

        public DownloadItem(Uri url, string outputPath, int parts)
        {
            Url = url;
            OutputPath = outputPath;
            Parts = parts;
        }

        // ============================================
        // Start download
        // ============================================
        public async Task StartAsync()
        {
            cts = new CancellationTokenSource();
            State = "Downloading";

            // If total size is unknown, ask the server
            if (Total <= 0)
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(30);

                    using (var head = new HttpRequestMessage(HttpMethod.Head, Url))
                    using (var hr = await client.SendAsync(
                        head,
                        HttpCompletionOption.ResponseHeadersRead,
                        cts.Token))
                    {
                        if (!hr.IsSuccessStatusCode)
                            throw new Exception("Server rejected request: " + hr.StatusCode);

                        Total = hr.Content.Headers.ContentLength ?? -1;
                    }
                }
            }

            if (Total <= 0 || Parts == 1)
                await SingleDownload();
            else
                await MultiDownload();
        }

        // ============================================
        // Single-part download
        // ============================================
        async Task SingleDownload()
        {
            bool resume = File.Exists(OutputPath) && Downloaded > 0;

            using (var client = new HttpClient())
            {
                var req = new HttpRequestMessage(HttpMethod.Get, Url);

                if (resume)
                    req.Headers.Range =
                        new System.Net.Http.Headers.RangeHeaderValue(Downloaded, null);

                using (var response = await client.SendAsync(
                    req,
                    HttpCompletionOption.ResponseHeadersRead,
                    cts.Token))
                {
                    if (resume && (int)response.StatusCode != 206)
                    {
                        resume = false;
                        Downloaded = 0;
                    }

                    response.EnsureSuccessStatusCode();

                    using (var input = await response.Content.ReadAsStreamAsync())
                    using (var output = new FileStream(
                        OutputPath,
                        resume ? FileMode.Append : FileMode.Create,
                        FileAccess.Write,
                        FileShare.Read))
                    {
                        byte[] buffer = new byte[64 * 1024];
                        int read;

                        while ((read = await input.ReadAsync(
                            buffer, 0, buffer.Length, cts.Token)) > 0)
                        {
                            await output.WriteAsync(buffer, 0, read, cts.Token);

                            Downloaded += read;
                            UpdateSpeed();
                        }
                    }
                }
            }

            State = "Completed";
        }

        // ============================================
        // Multi-part download
        // ============================================
        async Task MultiDownload()
        {
            Directory.CreateDirectory(TempDir);

            long chunk = Total / Parts;
            var tasks = new List<Task>();

            for (int i = 0; i < Parts; i++)
            {
                long start = i * chunk;
                long end = (i == Parts - 1) ? Total - 1 : start + chunk - 1;
                int index = i;

                tasks.Add(Task.Run(
                    () => DownloadPart(start, end, index),
                    cts.Token));
            }

            await Task.WhenAll(tasks);
            cts.Token.ThrowIfCancellationRequested();

            // Merge parts
            using (var output = new FileStream(
                OutputPath, FileMode.Create, FileAccess.Write))
            {
                for (int i = 0; i < Parts; i++)
                {
                    string partPath = Path.Combine(TempDir, i + ".part");

                    using (var input = new FileStream(
                        partPath, FileMode.Open, FileAccess.Read))
                    {
                        await input.CopyToAsync(output);
                    }

                    File.Delete(partPath);
                }
            }

            if (Directory.Exists(TempDir))
                Directory.Delete(TempDir, true);

            State = "Completed";
        }

        async Task DownloadPart(long start, long end, int index)
        {
            string partPath = Path.Combine(TempDir, index + ".part");
            long alreadyDone = File.Exists(partPath)
                ? new FileInfo(partPath).Length
                : 0;

            long from = start + alreadyDone;

            using (var client = new HttpClient())
            {
                var req = new HttpRequestMessage(HttpMethod.Get, Url);
                req.Headers.Range =
                    new System.Net.Http.Headers.RangeHeaderValue(from, end);

                using (var res = await client.SendAsync(
                    req,
                    HttpCompletionOption.ResponseHeadersRead,
                    cts.Token))
                {
                    if ((int)res.StatusCode != 206)
                        throw new Exception("Server does not support multi-part downloads.");

                    using (var input = await res.Content.ReadAsStreamAsync())
                    using (var output = new FileStream(
                        partPath,
                        alreadyDone > 0 ? FileMode.Append : FileMode.Create,
                        FileAccess.Write,
                        FileShare.Read))
                    {
                        byte[] buffer = new byte[64 * 1024];
                        int read;

                        while ((read = await input.ReadAsync(
                            buffer, 0, buffer.Length, cts.Token)) > 0)
                        {
                            await output.WriteAsync(buffer, 0, read, cts.Token);

                            Interlocked.Add(ref Downloaded, read);
                            UpdateSpeed();
                        }
                    }
                }
            }
        }

        void UpdateSpeed()
        {
            var now = DateTime.UtcNow;
            double seconds = (now - lastTime).TotalSeconds;

            if (seconds >= 0.5)
            {
                Speed = (Downloaded - lastBytes) / seconds;
                lastBytes = Downloaded;
                lastTime = now;
            }
        }

        // ============================================
        // Pause
        // ============================================
        public void Pause()
        {
            if (cts != null && State == "Downloading")
            {
                State = "Paused";
                cts.Cancel();
                Speed = 0;
            }
        }

        // ============================================
        // Resume
        // ============================================
        public async Task ResumeAsync()
        {
            lastBytes = Downloaded;
            lastTime = DateTime.UtcNow;

            await StartAsync();
        }

        // ============================================
        // Cancel
        // ============================================
        public void Cancel()
        {
            if (cts != null)
                cts.Cancel();

            State = "Cancelled";
            Speed = 0;

            // Delete unfinished files
            try
            {
                if (File.Exists(OutputPath))
                    File.Delete(OutputPath);

                if (Directory.Exists(TempDir))
                    Directory.Delete(TempDir, true);
            }
            catch { }
        }
    }
}
