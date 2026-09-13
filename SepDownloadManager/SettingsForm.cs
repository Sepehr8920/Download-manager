using System;
using System.Drawing;
using System.Windows.Forms;

namespace SepDownloadManager
{
    public class SettingsForm : Form
    {
        NumericUpDown connBox, simulBox;

        public SettingsForm()
        {
            Text = "Settings";
            Width = 420;
            Height = 260;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;

            // ---- Parts ----
            Controls.Add(new Label
            {
                Left = 20,
                Top = 20,
                Width = 370,
                Text = "Parts per download (1–16):"
            });

            connBox = new NumericUpDown
            {
                Left = 20,
                Top = 45,
                Width = 100,
                Minimum = 1,
                Maximum = 16,
                Value = AppSettings.MaxConnections
            };
            Controls.Add(connBox);

            Controls.Add(new Label
            {
                Left = 130,
                Top = 48,
                Width = 260,
                ForeColor = SystemColors.GrayText,
                Text = "Higher = faster (8 recommended)"
            });

            // ---- Simultaneous downloads ----
            Controls.Add(new Label
            {
                Left = 20,
                Top = 90,
                Width = 370,
                Text = "Max simultaneous downloads (1–10):"
            });

            simulBox = new NumericUpDown
            {
                Left = 20,
                Top = 115,
                Width = 100,
                Minimum = 1,
                Maximum = 10,
                Value = AppSettings.MaxSimultaneous
            };
            Controls.Add(simulBox);

            Controls.Add(new Label
            {
                Left = 130,
                Top = 118,
                Width = 260,
                ForeColor = SystemColors.GrayText,
                Text = "Others will wait in queue"
            });

            // ---- Buttons ----
            var okBtn = new Button
            {
                Left = 200,
                Top = 170,
                Width = 85,
                Text = "OK",
                DialogResult = DialogResult.OK
            };
            okBtn.Click += (s, e) =>
            {
                AppSettings.MaxConnections = (int)connBox.Value;
                AppSettings.MaxSimultaneous = (int)simulBox.Value;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(okBtn);

            var cancelBtn = new Button
            {
                Left = 295,
                Top = 170,
                Width = 85,
                Text = "Cancel",
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(cancelBtn);

            AcceptButton = okBtn;
            CancelButton = cancelBtn;
        }
    }
}
