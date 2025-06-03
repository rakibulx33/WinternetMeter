using System;
using System.Drawing;
using System.Windows.Forms;
using WinternetMeter;
using WinternetMeter.Properties;

namespace WinternetMeter
{
    public class FloatingSpeedForm : Form
    {
        private Label label;
        private Point mouseDownLocation;
        private bool dragging = false;

        public FloatingSpeedForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = false;
            this.BackColor = Color.Black;
            this.TransparencyKey = Color.Black;
            ForeColor = Properties.Settings.Default.TextColor;
            label = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(Properties.Settings.Default.FontStyle ?? "Segoe UI", 12, FontStyle.Regular),
                ForeColor = Properties.Settings.Default.TextColor,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0),
                BorderStyle = BorderStyle.None,
            };
            Controls.Add(label);

            // Enable dragging the floating window from anywhere
            this.MouseDown += FloatingSpeedForm_MouseDown;
            this.MouseMove += FloatingSpeedForm_MouseMove;
            this.MouseUp += FloatingSpeedForm_MouseUp;
            label.MouseDown += FloatingSpeedForm_MouseDown;
            label.MouseMove += FloatingSpeedForm_MouseMove;
            label.MouseUp += FloatingSpeedForm_MouseUp;
        }

        public void SetSpeed(string download, string upload)
        {
            label.Text = $"⬆️ {upload}\n⬇️ {download}";
            // Resize form to fit label
            using (var g = label.CreateGraphics())
            {
                var size = g.MeasureString(label.Text, label.Font);
                // Add some padding
                int padW = 28, padH = 18;
                this.Width = (int)size.Width + padW;
                this.Height = (int)size.Height + padH;
            }
        }

        public bool AllowDrag { get; set; } = true;
        public string CurrentFontFamily { get; private set; } = "Segoe UI";

        public void SetFontSize(int size)
        {
            try {
                label.Font = new Font(CurrentFontFamily, size, FontStyle.Bold);
            } catch { /* Ignore invalid font size */ }
        }
        public void SetFontFamily(string fontFamily)
        {
            CurrentFontFamily = fontFamily;
            try {
                label.Font = new Font(CurrentFontFamily, Properties.Settings.Default.TextSize, FontStyle.Bold);
            } catch { /* Ignore invalid font family */ }
        }

        private void FloatingSpeedForm_MouseDown(object? sender, MouseEventArgs e)
        {
            if (AllowDrag && e.Button == MouseButtons.Left)
            {
                dragging = true;
                mouseDownLocation = e.Location;
            }
        }

        private void FloatingSpeedForm_MouseMove(object? sender, MouseEventArgs e)
        {
            if (AllowDrag && dragging && e.Button == MouseButtons.Left)
            {
                this.Left += e.X - mouseDownLocation.X;
                this.Top += e.Y - mouseDownLocation.Y;
            }
        }

        private void FloatingSpeedForm_MouseUp(object? sender, MouseEventArgs e)
        {
            if (AllowDrag && e.Button == MouseButtons.Left)
                dragging = false;
        }

        public override void Refresh()
        {
            base.Refresh();
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84;
            const int HTCAPTION = 2;
            if (m.Msg == WM_NCHITTEST)
            {
                if (AllowDrag)
                {
                    m.Result = (IntPtr)HTCAPTION;
                    return;
                }
            }
            base.WndProc(ref m);
        }
    }
}
