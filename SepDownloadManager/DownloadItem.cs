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

        // Speed smoothing
        DateTime lastTime = DateTime.UtcNow;
        long lastBytes;
        readonly Queue<double> speedHistory = new Queue<double>();
        const int SpeedHistorySize = 6; // average over last few samples

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

            if (Total <= 0)
                await FetchTotalSize();

            if (Total <= 0 || Parts == 1)
                await SingleDownload();
            else
                await MultiDownload();
        }

        async Task FetchTotalSize()
        {
            var client = HttpClientProvider.Get();

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

        // ============================================
        // Single-part download
        // ============================================
        async Task SingleDownload()
        {
            bool resume = File.Exists(OutputPath) && Downloaded > 0;

            var client = HttpClientProvider.Get();
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
                    FileShare.Read,
                    128 * 1024))
                {
                    byte[] buffer = new byte[128 * 1024]; // 128 KB buffer
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
                    () => DownloadPartWithRetry(start, end, index),
                    cts.Token));
            }

            await Task.WhenAll(tasks);
            cts.Token.ThrowIfCancellationRequested();

            // Verify total size before merging
            long totalOnDisk = 0;
            for (int i = 0; i < Parts; i++)
            {
                string partPath = Path.Combine(TempDir, i + ".part");
                if (!File.Exists(partPath))
                    throw new Exception("Missing part file #" + i);
                totalOnDisk += new FileInfo(partPath).Length;
            }

            if (totalOnDisk != Total)
                throw new Exception(
                    $"Size mismatch: expected {Total} bytes but got {totalOnDisk}. File may be corrupt.");

            // Merge parts
            using (var output = new FileStream(
                OutputPath, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024))
            {
                for (int i = 0; i < Parts; i++)
                {
                    string partPath = Path.Combine(TempDir, i + ".part");

                    using (var input = new FileStream(
                        partPath, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024))
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

        async Task DownloadPartWithRetry(long start, long end, int index)
        {
            const int maxRetries = 3;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    await DownloadPart(start, end, index);
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    if (attempt == maxRetries - 1)
                        throw;

                    // Wait before retry
                    await Task.Delay(2000, cts.Token);
                }
            }
        }

        async Task DownloadPart(long start, long end, int index)
        {
            string partPath = Path.Combine(TempDir, index + ".part");

            long alreadyDone = File.Exists(partPath)
                ? new FileInfo(partPath).Length
                : 0;

            // Already finished?
            if (alreadyDone >= (end - start + 1))
                return;

            long from = start + alreadyDone;

            var client = HttpClientProvider.Get();
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
                    FileShare.Read,
                    128 * 1024))
                {
                    byte[] buffer = new byte[128 * 1024]; // 128 KB buffer
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

        // ============================================
        // Smoothed speed calculation
        // ============================================
        void UpdateSpeed()
        {
            var now = DateTime.UtcNow;
            double seconds = (now - lastTime).TotalSeconds;

            if (seconds < 0.5) return;

            double current = (Downloaded - lastBytes) / seconds;

            lastBytes = Downloaded;
            lastTime = now;

            // Keep a small history and average it
            speedHistory.Enqueue(current);
            while (speedHistory.Count > SpeedHistorySize)
                speedHistory.Dequeue();

            double sum = 0;
            foreach (var s in speedHistory)
                sum += s;

            Speed = sum / speedHistory.Count;
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
                speedHistory.Clear();
            }
        }

        // ============================================
        // Resume
        // ============================================
        public async Task ResumeAsync()
        {
            lastBytes = Downloaded;
            lastTime = DateTime.UtcNow;
            speedHistory.Clear();

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
