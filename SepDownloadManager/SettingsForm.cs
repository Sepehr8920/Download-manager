using System;
using System.Drawing;
using System.Windows.Forms;

namespace SepDownloadManager
{
    public class SettingsForm : Form
    {
        NumericUpDown connBox;

        public SettingsForm()
        {
            Text = "Settings";
            Width = 380;
            Height = 200;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;

            var label = new Label
            {
                Left = 20,
                Top = 20,
                Width = 320,
                Text = "Maximum simultaneous connections (1–16):"
            };
            Controls.Add(label);

            connBox = new NumericUpDown
            {
                Left = 20,
                Top = 50,
                Width = 100,
                Minimum = 1,
                Maximum = 16,
                Value = AppSettings.MaxConnections
            };
            Controls.Add(connBox);

            var hint = new Label
            {
                Left = 130,
                Top = 53,
                Width = 220,
                ForeColor = SystemColors.GrayText,
                Text = "Higher = faster (8 is recommended)."
            };
            Controls.Add(hint);

            var okBtn = new Button
            {
                Left = 180,
                Top = 110,
                Width = 75,
                Text = "OK",
                DialogResult = DialogResult.OK
            };
            okBtn.Click += (s, e) =>
            {
                AppSettings.MaxConnections = (int)connBox.Value;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(okBtn);

            var cancelBtn = new Button
            {
                Left = 265,
                Top = 110,
                Width = 75,
                Text = "Cancel",
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(cancelBtn);

            AcceptButton = okBtn;
            CancelButton = cancelBtn;
        }
    }
}
