using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SepDownloadManager
{
    public class MainForm : Form
    {
        TextBox urlBox, fileBox;
        Button addButton, settingsButton;
        ProgressBar progress;
        Label status;
        DataGridView grid;

        readonly List<DownloadItem> items = new List<DownloadItem>();
        Timer refreshTimer;
        DownloadQueue queue;

        public MainForm()
        {
            Text = "Sep Download Manager — v2";
            Width = 1100;
            Height = 600;
            MinimumSize = new Size(900, 500);
            StartPosition = FormStartPosition.CenterScreen;

            // ---------- Top bar ----------
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
                Width = 660,
                Font = new Font("Segoe UI", 9)
            };
            top.Controls.Add(urlBox);

            top.Controls.Add(new Label
            {
                Left = 12,
                Top = 32,
                Text = "Download URL",
                AutoSize = true,
                ForeColor = SystemColors.GrayText
            });

            fileBox = new TextBox
            {
                Left = 10,
                Top = 45,
                Width = 660,
                Text = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile),
                    "Downloads")
            };
            top.Controls.Add(fileBox);

            addButton = new Button
            {
                Left = 685,
                Top = 10,
                Width = 120,
                Height = 60,
                Text = "Start\nDownload",
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            addButton.Click += (s, e) => AddDownload();
            top.Controls.Add(addButton);

            settingsButton = new Button
            {
                Left = 815,
                Top = 10,
                Width = 100,
                Height = 60,
                Text = "Settings"
            };
            settingsButton.Click += (s, e) => OpenSettings();
            top.Controls.Add(settingsButton);

            // ---------- Table ----------
            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AllowUserToResizeRows = false,
                Font = new Font("Segoe UI", 9)
            };

            grid.Columns.Add("file", "File Name");
            grid.Columns.Add("size", "Total Size");
            grid.Columns.Add("downloaded", "Downloaded");
            grid.Columns.Add("remaining", "Remaining");
            grid.Columns.Add("speed", "Speed");
            grid.Columns.Add("priority", "Priority");
            grid.Columns.Add("state", "Status");

            var actionCol = new DataGridViewButtonColumn
            {
                Name = "actions",
                HeaderText = "Actions",
                Width = 200,
                FlatStyle = FlatStyle.Flat
            };
            grid.Columns.Add(actionCol);

            grid.CellClick += Grid_CellClick;
            grid.CellDoubleClick += Grid_CellDoubleClick;

            Controls.Add(grid);
            grid.BringToFront();

            // ---------- Bottom bar ----------
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
                Width = 400,
                Text = "Ready"
            };
            bottom.Controls.Add(status);

            refreshTimer = new Timer();
            refreshTimer.Interval = 500;
            refreshTimer.Tick += (s, e) => RefreshAllRows();
            refreshTimer.Start();

            LoadSavedItems();

            // Create the queue manager
            queue = new DownloadQueue(items);
        }

        void OpenSettings()
        {
            using (var dlg = new SettingsForm())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    status.Text =
                        $"Parts: {AppSettings.MaxConnections}   |   " +
                        $"Simultaneous: {AppSettings.MaxSimultaneous}";
                }
            }
        }

        void LoadSavedItems()
        {
            var saved = DownloadStore.LoadAll();

            foreach (var s in saved)
            {
                var item = new DownloadItem(
                    new Uri(s.Url),
                    s.OutputPath,
                    s.Parts);

                item.Downloaded = s.Downloaded;
                item.Total = s.Total;
                item.Priority = s.Priority ?? "Normal";

                if (s.State == "Downloading")
                    item.State = "Paused";
                else
                    item.State = s.State;

                items.Add(item);

                int row = grid.Rows.Add(
                    Path.GetFileName(s.OutputPath),
                    item.Total > 0 ? FormatBytes(item.Total) : "?",
                    FormatBytes(item.Downloaded),
                    item.Total > 0 ? FormatBytes(item.Total - item.Downloaded) : "?",
                    "-",
                    item.Priority,
                    item.State);

                item.Row = row;
                UpdateActionButton(item);
            }

            if (items.Count > 0)
                status.Text = $"{items.Count} download(s) loaded";
        }

        void AddDownload()
        {
            if (!Uri.TryCreate(
                    urlBox.Text.Trim(),
                    UriKind.Absolute,
                    out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                MessageBox.Show(
                    "Please enter a valid URL (starting with http or https).",
                    "Invalid URL",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string dir = fileBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(dir))
                dir = Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile);

            Directory.CreateDirectory(dir);

            string name = Path.GetFileName(uri.LocalPath);
            if (string.IsNullOrWhiteSpace(name))
                name = "download.bin";

            string outputPath = Path.Combine(dir, name);

            int counter = 1;
            string baseName = Path.GetFileNameWithoutExtension(name);
            string ext = Path.GetExtension(name);

            while (File.Exists(outputPath))
            {
                outputPath = Path.Combine(dir, $"{baseName} ({counter}){ext}");
                counter++;
            }

            var item = new DownloadItem(
                uri,
                outputPath,
                AppSettings.MaxConnections);

            item.State = "In Queue";
            item.Priority = "Normal";

            items.Add(item);

            int row = grid.Rows.Add(
                Path.GetFileName(outputPath),
                "Checking...",
                "0 B",
                "?",
                "-",
                item.Priority,
                item.State);

            item.Row = row;
            UpdateActionButton(item);

            urlBox.Clear();
            DownloadStore.Save(items);

            status.Text = "Added to queue";
        }

        void RefreshAllRows()
        {
            if (IsDisposed || !IsHandleCreated) return;

            foreach (var item in items)
            {
                if (item.Row < 0 || item.Row >= grid.Rows.Count) continue;

                var row = grid.Rows[item.Row];

                row.Cells[1].Value = item.Total > 0 ? FormatBytes(item.Total) : "?";
                row.Cells[2].Value = FormatBytes(item.Downloaded);
                row.Cells[3].Value = item.Total > 0
                    ? FormatBytes(item.Total - item.Downloaded) : "?";
                row.Cells[4].Value = item.Speed > 0
                    ? FormatBytes((long)item.Speed) + "/s" : "-";
                row.Cells[5].Value = item.Priority;
                row.Cells[6].Value = item.State;
            }
        }

        void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            // reserved for later (details window)
        }

        void Grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (e.ColumnIndex != grid.Columns["actions"].Index) return;

            var item = items[e.RowIndex];
            var menu = new ContextMenuStrip();

            if (item.State == "Downloading")
            {
                menu.Items.Add("⏸ Pause", null, (s, ev) =>
                {
                    item.Pause();
                    UpdateActionButton(item);
                    DownloadStore.Save(items);
                });
            }

            if (item.State == "Paused")
            {
                menu.Items.Add("▶️ Resume", null, (s, ev) =>
                {
                    item.State = "In Queue";
                    UpdateActionButton(item);
                    DownloadStore.Save(items);
                });
            }

            if (item.State == "In Queue" || item.State == "Waiting")
            {
                menu.Items.Add("⬆️ Move Up Priority", null, (s, ev) =>
                {
                    item.Priority = "High";
                    UpdateActionButton(item);
                    DownloadStore.Save(items);
                });

                menu.Items.Add("⬇️ Move Down Priority", null, (s, ev) =>
                {
                    item.Priority = "Low";
                    UpdateActionButton(item);
                    DownloadStore.Save(items);
                });

                menu.Items.Add("🟡 Normal Priority", null, (s, ev) =>
                {
                    item.Priority = "Normal";
                    UpdateActionButton(item);
                    DownloadStore.Save(items);
                });
            }

            if (item.State != "Completed" && item.State != "Cancelled")
            {
                menu.Items.Add("❌ Cancel", null, (s, ev) =>
                {
                    item.Cancel();
                    UpdateActionButton(item);
                    DownloadStore.Save(items);
                });
            }

            if (item.State == "Completed" ||
                item.State == "Cancelled" ||
                item.State == "Error")
            {
                menu.Items.Add("🗑 Remove from list", null, (s, ev) =>
                {
                    items.Remove(item);
                    grid.Rows.RemoveAt(item.Row);
                    for (int i = 0; i < items.Count; i++)
                        items[i].Row = i;
                    DownloadStore.Save(items);
                });
            }

            if (menu.Items.Count > 0)
                menu.Show(grid, grid.PointToClient(Cursor.Position));
        }

        void UpdateActionButton(DownloadItem item)
        {
            if (item.Row < 0 || item.Row >= grid.Rows.Count) return;

            string text;

            switch (item.State)
            {
                case "Downloading":
                    text = "⏸ Pause  |  ❌ Cancel";
                    break;
                case "Paused":
                    text = "▶️ Resume  |  ❌ Cancel";
                    break;
                case "In Queue":
                case "Waiting":
                    text = "⏳ In Queue  |  ❌ Cancel";
                    break;
                case "Completed":
                    text = "✅ Done  |  🗑 Remove";
                    break;
                case "Cancelled":
                    text = "❌ Cancelled  |  🗑 Remove";
                    break;
                case "Error":
                    text = "⚠️ Error  |  🗑 Remove";
                    break;
                default:
                    text = "⏳ " + item.State;
                    break;
            }

            grid.Rows[item.Row].Cells["actions"].Value = text;
            grid.Rows[item.Row].Cells["priority"].Value = item.Priority;
            grid.Rows[item.Row].Cells["state"].Value = item.State;
        }

        public static string FormatBytes(long n)
        {
            if (n < 0) return "?";

            if (n < 1024) return n + " B";

            double x = n / 1024.0;
            if (x < 1024) return x.ToString("0.0") + " KB";

            x /= 1024;
            if (x < 1024) return x.ToString("0.0") + " MB";

            return (x / 1024).ToString("0.00") + " GB";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            DownloadStore.Save(items);
            base.OnFormClosing(e);
        }
    }
}
