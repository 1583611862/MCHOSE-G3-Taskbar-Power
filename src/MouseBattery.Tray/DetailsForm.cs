using System;
using System.Drawing;
using System.Windows.Forms;
using G3Power.Core;

namespace G3Power.Tray
{
    internal sealed class DetailsForm : Form
    {
        private readonly Label _titleLabel;
        private readonly Label _percentLabel;
        private readonly Label _statusLabel;
        private readonly Label _updatedLabel;
        private readonly Label _deviceLabel;
        private readonly Label _idLabel;
        private readonly Label _pathLabel;
        private readonly Label _frameLabel;
        private readonly Label _hintLabel;

        public DetailsForm()
        {
            Text = "G3 电量";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(360, 236);
            BackColor = Color.FromArgb(250, 250, 250);

            _titleLabel = CreateLabel("MCHOSE 鼠标", 16, 14, 328, 24);
            _titleLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);

            _percentLabel = CreateLabel("--", 16, 40, 128, 56);
            _percentLabel.Font = new Font("Segoe UI", 34f, FontStyle.Bold);

            _statusLabel = CreateLabel("未连接", 156, 48, 188, 22);
            _statusLabel.Font = new Font("Segoe UI", 10f, FontStyle.Bold);

            _updatedLabel = CreateLabel("", 156, 72, 188, 20);
            _updatedLabel.ForeColor = Color.FromArgb(90, 90, 90);

            Panel separator = new Panel();
            separator.Bounds = new Rectangle(16, 104, 328, 1);
            separator.BackColor = Color.FromArgb(220, 220, 220);
            Controls.Add(separator);

            _deviceLabel = CreateLabel("", 16, 112, 328, 20);
            _deviceLabel.ForeColor = Color.FromArgb(60, 60, 60);

            _idLabel = CreateLabel("", 16, 134, 328, 20);
            _idLabel.ForeColor = Color.FromArgb(60, 60, 60);

            _pathLabel = CreateLabel("", 16, 156, 328, 20);
            _pathLabel.ForeColor = Color.FromArgb(90, 90, 90);
            _pathLabel.AutoEllipsis = true;

            _frameLabel = CreateLabel("", 16, 178, 328, 20);
            _frameLabel.ForeColor = Color.FromArgb(90, 90, 90);
            _frameLabel.Font = new Font("Consolas", 9f);

            _hintLabel = CreateLabel("点击窗口外任意处关闭", 16, 206, 328, 18);
            _hintLabel.ForeColor = Color.FromArgb(140, 140, 140);
            _hintLabel.Font = new Font("Segoe UI", 8f);
        }

        private Label CreateLabel(string text, int x, int y, int width, int height)
        {
            Label label = new Label();
            label.Text = text;
            label.Bounds = new Rectangle(x, y, width, height);
            label.AutoSize = false;
            label.BackColor = Color.Transparent;
            Controls.Add(label);
            return label;
        }

        private bool _closing;

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);

            if (_closing || IsDisposed) return;
            _closing = true;
            Close();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _closing = false;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape && !_closing)
            {
                _closing = true;
                Close();
            }
        }

        public void Apply(BatterySnapshot snapshot, bool stale)
        {
            if (snapshot == null) return;

            string title = string.IsNullOrEmpty(snapshot.DeviceName) ? "MCHOSE 鼠标" : snapshot.DeviceName;
            _titleLabel.Text = title;

            if (snapshot.IsConnected && snapshot.HasPercent)
            {
                _percentLabel.Text = snapshot.Percent + "%";
                _percentLabel.ForeColor = BatteryIconRenderer.GetLevelColor(snapshot.Percent, true, true);
            }
            else if (snapshot.HasPercent)
            {
                _percentLabel.Text = snapshot.Percent + "%";
                _percentLabel.ForeColor = Color.FromArgb(140, 140, 140);
            }
            else
            {
                _percentLabel.Text = "--";
                _percentLabel.ForeColor = Color.FromArgb(140, 140, 140);
            }

            if (snapshot.IsConnected)
            {
                _statusLabel.Text = stale ? "已连接（数据待刷新）" : "已连接";
                _statusLabel.ForeColor = Color.FromArgb(39, 174, 96);
            }
            else
            {
                _statusLabel.Text = "未连接";
                _statusLabel.ForeColor = Color.FromArgb(192, 57, 43);
            }

            if (snapshot.LastUpdateUtc != DateTime.MinValue)
            {
                DateTime local = snapshot.LastUpdateUtc.ToLocalTime();
                _updatedLabel.Text = "更新于 " + local.ToString("HH:mm:ss");
            }
            else
            {
                _updatedLabel.Text = "尚无实时数据";
            }

            _deviceLabel.Text = "设备： " + (string.IsNullOrEmpty(snapshot.DeviceName) ? "未知" : snapshot.DeviceName);
            _idLabel.Text = "VID/PID： " + (string.IsNullOrEmpty(snapshot.DeviceId) ? "—" : snapshot.DeviceId)
                + "    UsagePage 0xFF02 / Usage 0x0002";

            string path = snapshot.DevicePath;
            if (string.IsNullOrEmpty(path))
                _pathLabel.Text = "路径： 未打开";
            else
                _pathLabel.Text = "路径： " + path;

            if (string.IsNullOrEmpty(snapshot.LastFrameHex))
                _frameLabel.Text = "最近帧： —";
            else
                _frameLabel.Text = "最近帧： " + snapshot.LastFrameHex;

            if (!string.IsNullOrEmpty(snapshot.LastError))
                _hintLabel.Text = snapshot.LastError;
            else
                _hintLabel.Text = "点击窗口外任意处关闭";
        }

        public void ShowNearCursor()
        {
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Point cursor = Cursor.Position;

            int x = cursor.X - Width + 24;
            int y = area.Bottom - Height - 8;

            if (x < area.Left + 8) x = area.Left + 8;
            if (x + Width > area.Right - 8) x = area.Right - Width - 8;
            if (y < area.Top + 8) y = area.Top + 8;

            Location = new Point(x, y);
            Show();
            Activate();
        }
    }
}
