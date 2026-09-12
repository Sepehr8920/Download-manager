using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SepDownloadManager
{
    public class MainForm : Form
    {
        TextBox urlBox, fileBox;
        NumericUpDown partsBox;
        Button addButton, pauseButton, resumeButton;
        ProgressBar progress;
        Label status;
        DataGridView grid;

        readonly List<DownloadItem> items = new List<DownloadItem>();

        public MainForm()
        {
            Text = "Sep Download Manager — v1";
            Width = 900;
            Height = 560;
            MinimumSize = new Size(760, 460);
            StartPosition = FormStartPosition.CenterScreen;

            var top = new Panel
            {
                Dock = DockStyle.Top,
                Height = 105,
                Padding = new Padding(10)
            };

            Controls.Add(top);

            urlBox = new TextBox
            {
                Left = 10,
                Top = 10,
                Width = 560
            };

            top.Controls.Add(urlBox);

            var urlHint = new Label
            {
                Left = 12,
                Top = 32,
                Text = "URL",
                AutoSize = true,
                ForeColor = SystemColors.GrayText
            };

            top.Controls.Add(urlHint);

            partsBox = new NumericUpDown
            {
                Left = 580,
                Top = 10,
                Width = 75,
                Minimum = 1,
                Maximum = 16,
                Value = 4
            };

            top.Controls.Add(partsBox);

            top.Controls.Add(new Label
            {
                Left = 660,
                Top = 13,
                Text = "parts",
                AutoSize = true
            });

            fileBox = new TextBox
            {
                Left = 10,
                Top = 45,
                Width = 560,
                Text = System.IO.Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile),
                    "Downloads")
            };

            top.Controls.Add(fileBox);

            addButton = new Button
            {
                Left = 580,
                Top = 43,
                Width = 155,
                Height = 28,
                Text = "Add & Download"
            };

            addButton.Click += async (s, e) => await AddDownload();

            top.Controls.Add(addButton);

            pauseButton = new Button
            {
                Left = 745,
                Top = 10,
                Width = 120,
                Text = "Pause"
            };

            pauseButton.Click += (s, e) => SelectedPause();

            top.Controls.Add(pauseButton);

            resumeButton = new Button
            {
                Left = 745,
                Top = 43,
                Width = 120,
                Text = "Resume"
            };

            resumeButton.Click += async (s, e) => await SelectedResume();

            top.Controls.Add(resumeButton);

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode =
                    DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode =
                    DataGridViewSelectionMode.FullRowSelect
            };

            grid.Columns.Add("file", "File");
            grid.Columns.Add("size", "Size");
            grid.Columns.Add("downloaded", "Downloaded");
            grid.Columns.Add("speed", "Speed");
            grid.Columns.Add("state", "State");

            Controls.Add(grid);

            var bottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 55,
                Padding = new Padding(10)
            };

            Controls.Add(bottom);

            progress = new ProgressBar
            {
                Left = 10,
                Top = 8,
                Width = 600,
                Height = 22
            };

            bottom.Controls.Add(progress);

            status = new Label
            {
                Left = 620,
                Top = 12,
                Width = 240,
                Text = "Ready"
            };

            bottom.Controls.Add(status);
        }

        async Task AddDownload()
        {
            if (!Uri.TryCreate(
                    urlBox.Text.Trim(),
                    UriKind.Absolute,
                    out var uri) ||
                (uri.Scheme != "http" &&
                 uri.Scheme != "https"))
            {
                MessageBox.Show(
                    "Enter a valid HTTP/HTTPS URL.");

                return;
            }

            string dir = fileBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(dir))
            {
                dir = Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile);
            }

            Directory.CreateDirectory(dir);

            string name =
                System.IO.Path.GetFileName(uri.LocalPath);

            if (string.IsNullOrWhiteSpace(name))
            {
                name = "download.bin";
            }

            string outputPath =
                System.IO.Path.Combine(dir, name);

            var item = new DownloadItem(
                uri,
                outputPath,
                (int)partsBox.Value);

            items.Add(item);

            int row = grid.Rows.Add(
                name,
                "Checking...",
                "0 B",
                "-",
                "Starting");

            item.Row = row;

            try
            {
                await item.StartAsync(
                    () => RefreshRow(item));

                status.Text = "Completed";
            }
            catch (OperationCanceledException)
            {
                status.Text = "Paused";
                RefreshRow(item);
            }
            catch (Exception ex)
            {
                item.State = "Error";
                status.Text = ex.Message;
                RefreshRow(item);
            }
        }

        void RefreshRow(DownloadItem x)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            BeginInvoke((Action)(() =>
            {
                if (x.Row < 0 ||
                    x.Row >= grid.Rows.Count)
                    return;

                grid.Rows[x.Row].Cells[1].Value =
                    x.Total >= 0
                        ? FormatBytes(x.Total)
                        : "?";

                grid.Rows[x.Row].Cells[2].Value =
                    FormatBytes(x.Downloaded);

                grid.Rows[x.Row].Cells[3].Value =
                    x.Speed > 0
                        ? FormatBytes((long)x.Speed) + "/s"
                        : "-";

                grid.Rows[x.Row].Cells[4].Value =
                    x.State;

                if (x.Total > 0)
                {
                    long percentage =
                        x.Downloaded * 100L / x.Total;

                    progress.Value = Math.Max(
                        0,
                        Math.Min(
                            100,
                            (int)percentage));
                }
                else
                {
                    progress.Value = 0;
                }
            }));
        }

        void SelectedPause()
        {
            if (grid.SelectedRows.Count == 0)
                return;

            var x =
                items[grid.SelectedRows[0].Index];

            x.Pause();

            RefreshRow(x);
        }

        async Task SelectedResume()
        {
            if (grid.SelectedRows.Count == 0)
                return;

            var x =
                items[grid.SelectedRows[0].Index];

            if (x.State != "Paused")
                return;

            try
            {
                await x.ResumeAsync(
                    () => RefreshRow(x));

                status.Text = "Completed";
            }
            catch (OperationCanceledException)
            {
                status.Text = "Paused";
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;

                x.State = "Error";

                RefreshRow(x);
            }
        }

        static string FormatBytes(long n)
        {
            if (n < 1024)
                return n + " B";

            double x = n / 1024.0;

            if (x < 1024)
                return x.ToString("0.0") + " KB";

            x /= 1024;

            if (x < 1024)
                return x.ToString("0.0") + " MB";

            return (x / 1024).ToString("0.00") + " GB";
        }
    }

    public class DownloadItem
    {
        public readonly Uri Url;
        public readonly string OutputPath;
        public readonly int Parts;

        public int Row = -1;

        public long Total = -1;
        public long Downloaded;

        public double Speed;

        public string State = "Queued";

        CancellationTokenSource cts;

        DateTime lastTime = DateTime.UtcNow;
        long lastBytes;

        public DownloadItem(
            Uri url,
            string outputPath,
            int parts)
        {
            Url = url;
            OutputPath = outputPath;
            Parts = parts;
        }

        public async Task StartAsync(Action update)
        {
            cts = new CancellationTokenSource();

            State = "Downloading";

            update();

            using (var client = new HttpClient())
            {
                client.Timeout =
                    TimeSpan.FromMinutes(30);

                using (var head =
                    new HttpRequestMessage(
                        HttpMethod.Head,
                        Url))
                using (var hr =
                    await client.SendAsync(
                        head,
                        HttpCompletionOption.ResponseHeadersRead,
                        cts.Token))
                {
                    if (!hr.IsSuccessStatusCode)
                    {
                        throw new Exception(
                            "Server rejected request: " +
                            hr.StatusCode);
                    }

                    Total =
                        hr.Content.Headers.ContentLength
                        ?? -1;
                }
            }

            if (Total <= 0 || Parts == 1)
            {
                await SingleDownload(update);
            }
            else
            {
                await MultiDownload(update);
            }
        }

        async Task SingleDownload(Action update)
        {
            using (var client = new HttpClient())
            using (var response =
                await client.GetAsync(
                    Url,
                    HttpCompletionOption.ResponseHeadersRead,
                    cts.Token))
            {
                response.EnsureSuccessStatusCode();

                using (var input =
                    await response.Content.ReadAsStreamAsync())
                using (var output =
                    new FileStream(
                        OutputPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.Read))
                {
                    byte[] buffer =
                        new byte[64 * 1024];

                    int read;

                    while ((read =
                        await input.ReadAsync(
                            buffer,
                            0,
                            buffer.Length,
                            cts.Token)) > 0)
                    {
                        await output.WriteAsync(
                            buffer,
                            0,
                            read,
                            cts.Token);

                        Downloaded += read;

                        UpdateSpeed();

                        update();
                    }
                }
            }

            State = "Completed";

            update();
        }

        async Task MultiDownload(Action update)
        {
            string tempDir =
                OutputPath + ".parts";

            Directory.CreateDirectory(tempDir);

            var tasks = new List<Task>();

            long chunk =
                Total / Parts;

            for (int i = 0; i < Parts; i++)
            {
                long start =
                    i * chunk;

                long end =
                    (i == Parts - 1)
                        ? Total - 1
                        : start + chunk - 1;

                int index = i;

                tasks.Add(
                    Task.Run(
                        () => DownloadPart(
                            start,
                            end,
                            index,
                            tempDir,
                            update),
                        cts.Token));
            }

            await Task.WhenAll(tasks);

            cts.Token.ThrowIfCancellationRequested();

            using (var output =
                new FileStream(
                    OutputPath,
                    FileMode.Create,
                    FileAccess.Write))
            {
                for (int i = 0; i < Parts; i++)
                {
                    string partPath =
                        System.IO.Path.Combine(
                            tempDir,
                            i + ".part");

                    using (var input =
                        new FileStream(
                            partPath,
                            FileMode.Open,
                            FileAccess.Read))
                    {
                        await input.CopyToAsync(
                            output);
                    }

                    File.Delete(partPath);
                }
            }

            Directory.Delete(
                tempDir,
                true);

            State = "Completed";

            update();
        }

        async Task DownloadPart(
            long start,
            long end,
            int index,
            string tempDir,
            Action update)
        {
            using (var client = new HttpClient())
            {
                var req =
                    new HttpRequestMessage(
                        HttpMethod.Get,
                        Url);

                req.Headers.Range =
                    new System.Net.Http.Headers
                        .RangeHeaderValue(
                            start,
                            end);

                using (var res =
                    await client.SendAsync(
                        req,
                        HttpCompletionOption.ResponseHeadersRead,
                        cts.Token))
                {
                    if ((int)res.StatusCode != 206)
                    {
                        throw new Exception(
                            "Server does not support " +
                            "multi-part downloads.");
                    }

                    string partPath =
                        System.IO.Path.Combine(
                            tempDir,
                            index + ".part");

                    using (var input =
                        await res.Content
                            .ReadAsStreamAsync())
                    using (var output =
                        new FileStream(
                            partPath,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.Read))
                    {
                        byte[] buffer =
                            new byte[64 * 1024];

                        int read;

                        while ((read =
                            await input.ReadAsync(
                                buffer,
                                0,
                                buffer.Length,
                                cts.Token)) > 0)
                        {
                            await output.WriteAsync(
                                buffer,
                                0,
                                read,
                                cts.Token);

                            Interlocked.Add(
                                ref Downloaded,
                                read);

                            UpdateSpeed();

                            update();
                        }
                    }
                }
            }
        }

        void UpdateSpeed()
        {
            var now = DateTime.UtcNow;

            double seconds =
                (now - lastTime).TotalSeconds;

            if (seconds >= 0.5)
            {
                Speed =
                    (Downloaded - lastBytes)
                    / seconds;

                lastBytes =
                    Downloaded;

                lastTime = now;
            }
        }

        public void Pause()
        {
            if (cts != null &&
                State == "Downloading")
            {
                State = "Paused";

                cts.Cancel();
            }
        }

        public async Task ResumeAsync(
            Action update)
        {
            // v1:
            // Resume دوباره از ابتدا شروع می‌شود.

            Downloaded = 0;
            Speed = 0;
            lastBytes = 0;
            lastTime = DateTime.UtcNow;

            State = "Downloading";

            await StartAsync(update);
        }
    }
}
