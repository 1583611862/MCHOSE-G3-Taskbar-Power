using System;
using System.Drawing;
using System.Windows.Forms;
using G3Power.Core;

namespace G3Power.Tray
{
    internal sealed class TrayContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _menu;
        private readonly Timer _timer;
        private readonly BatteryMonitor _monitor;
        private readonly AppSettings _settings;

        private readonly ToolStripMenuItem _headerItem;
        private readonly ToolStripMenuItem _deviceItem;
        private readonly ToolStripMenuItem _statusItem;
        private readonly ToolStripMenuItem _autoStartItem;
        private readonly ToolStripMenuItem _numberStyleItem;
        private readonly ToolStripMenuItem _batteryStyleItem;

        private readonly BatteryCacheEntry _cache;

        private DetailsForm _detailsForm;
        private Icon _currentIcon;
        private int _shownPercent = -1;
        private bool _shownConnected;
        private bool _shownHasValue;
        private bool _shownStale;
        private DateTime _savedUpdateUtc = DateTime.MinValue;

        public TrayContext()
        {
            _settings = AppSettings.Load();
            _cache = BatteryStateStore.Load();

            _monitor = new BatteryMonitor();
            _monitor.VendorId = _settings.VendorId;
            _monitor.ProductId = _settings.ProductId;
            _monitor.BcdDecode = _settings.BcdDecode;

            _menu = new ContextMenuStrip();
            _menu.ShowImageMargin = false;

            _headerItem = new ToolStripMenuItem("正在读取电量…");
            _headerItem.Enabled = false;

            _deviceItem = new ToolStripMenuItem("设备：—");
            _deviceItem.Enabled = false;

            _statusItem = new ToolStripMenuItem("状态：初始化");
            _statusItem.Enabled = false;

            ToolStripMenuItem detailsItem = new ToolStripMenuItem("查看详情", null, OnShowDetails);
            ToolStripMenuItem reconnectItem = new ToolStripMenuItem("立即重连", null, OnReconnect);

            _autoStartItem = new ToolStripMenuItem("开机自动启动", null, OnToggleAutoStart);
            _autoStartItem.Checked = AutoStart.IsEnabled();

            ToolStripMenuItem styleItem = new ToolStripMenuItem("托盘图标样式");
            _numberStyleItem = new ToolStripMenuItem("数字", null, OnSelectNumberStyle);
            _batteryStyleItem = new ToolStripMenuItem("电池图形", null, OnSelectBatteryStyle);
            _numberStyleItem.Checked = _settings.Style == IconStyle.Number;
            _batteryStyleItem.Checked = _settings.Style == IconStyle.Battery;
            styleItem.DropDownItems.Add(_numberStyleItem);
            styleItem.DropDownItems.Add(_batteryStyleItem);

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出", null, OnExit);

            _menu.Items.Add(_headerItem);
            _menu.Items.Add(_deviceItem);
            _menu.Items.Add(_statusItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(detailsItem);
            _menu.Items.Add(reconnectItem);
            _menu.Items.Add(_autoStartItem);
            _menu.Items.Add(styleItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(exitItem);

            _notifyIcon = new NotifyIcon();
            _notifyIcon.ContextMenuStrip = _menu;
            _notifyIcon.Text = "G3 电量：读取中";
            _notifyIcon.Visible = true;
            _notifyIcon.MouseClick += OnIconClick;
            _notifyIcon.DoubleClick += OnShowDetails;

            UpdateIcon(0, false, false, true);

            _monitor.Start();

            _timer = new Timer();
            _timer.Interval = 1000;
            _timer.Tick += OnTick;
            _timer.Start();
        }

        private void OnTick(object sender, EventArgs e)
        {
            BatterySnapshot snapshot = _monitor.GetSnapshot();
            bool stale = false;
            bool connected = snapshot.IsConnected;
            bool hasValue = snapshot.HasPercent;
            int percent = snapshot.Percent;
            DateTime updateUtc = snapshot.LastUpdateUtc;

            if (!hasValue && _cache.HasPercent)
            {
                percent = _cache.Percent;
                hasValue = true;
                stale = true;
                updateUtc = _cache.Utc;
            }
            else if (hasValue && updateUtc != DateTime.MinValue)
            {
                stale = (DateTime.UtcNow - updateUtc).TotalSeconds > _settings.StaleSeconds;
            }

            if (!snapshot.IsConnected && !_cache.HasPercent) hasValue = false;

            if (percent != _shownPercent || connected != _shownConnected ||
                hasValue != _shownHasValue || stale != _shownStale)
            {
                UpdateIcon(percent, hasValue, connected, stale);
            }

            _headerItem.Text = hasValue
                ? string.Format("电量：{0}%{1}", percent, stale ? "（缓存）" : string.Empty)
                : "电量：未知";

            string deviceName = snapshot.DeviceName;
            if (string.IsNullOrEmpty(deviceName) && !string.IsNullOrEmpty(_cache.DeviceName))
                deviceName = _cache.DeviceName;

            _deviceItem.Text = "设备：" + (string.IsNullOrEmpty(deviceName) ? "未找到" : deviceName) +
                "（" + _settings.VendorId.ToString("X4") + ":" + _settings.ProductId.ToString("X4") + "）";

            if (connected)
            {
                _statusItem.Text = stale ? "状态：已连接（数据超过 " + _settings.StaleSeconds + " 秒未刷新）" : "状态：已连接";
            }
            else
            {
                _statusItem.Text = string.IsNullOrEmpty(snapshot.LastError)
                    ? "状态：未连接"
                    : "状态：未连接 — " + snapshot.LastError;
            }

            if (hasValue && !stale && updateUtc != _savedUpdateUtc && updateUtc != DateTime.MinValue)
            {
                _savedUpdateUtc = updateUtc;
                BatteryStateStore.Save(percent, updateUtc, snapshot.DeviceName);
            }

            if (_detailsForm != null && !_detailsForm.IsDisposed && _detailsForm.Visible)
            {
                _detailsForm.Apply(snapshot, stale);
            }
        }

        private void UpdateIcon(int percent, bool hasValue, bool connected, bool stale)
        {
            _shownPercent = percent;
            _shownHasValue = hasValue;
            _shownConnected = connected;
            _shownStale = stale;

            int size = SystemInformation.SmallIconSize.Width;
            if (size < 16) size = 16;

            Icon icon = BatteryIconRenderer.Create(percent, hasValue, connected, _settings.Style, size);

            _notifyIcon.Icon = icon;
            if (_currentIcon != null) _currentIcon.Dispose();
            _currentIcon = icon;

            string text;
            if (!connected && !hasValue)
                text = "G3 电量：设备未连接";
            else if (!hasValue)
                text = "G3 电量：等待数据";
            else if (stale)
                text = string.Format("G3 电量 {0}%\n缓存数据，等待刷新", percent);
            else
                text = string.Format("G3 电量 {0}%\n{1}", percent, connected ? "已连接" : "未连接");

            if (text.Length > 63) text = text.Substring(0, 63);
            _notifyIcon.Text = text;
        }

        private void OnIconClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) ShowDetails();
        }

        private void OnShowDetails(object sender, EventArgs e)
        {
            ShowDetails();
        }

        private void ShowDetails()
        {
            if (_detailsForm == null || _detailsForm.IsDisposed)
            {
                _detailsForm = new DetailsForm();
            }

            BatterySnapshot snapshot = _monitor.GetSnapshot();
            bool stale = false;

            if (!snapshot.HasPercent && _cache.HasPercent)
            {
                snapshot.HasPercent = true;
                snapshot.Percent = _cache.Percent;
                snapshot.LastUpdateUtc = _cache.Utc;
                stale = true;
            }
            else if (snapshot.LastUpdateUtc != DateTime.MinValue)
            {
                stale = (DateTime.UtcNow - snapshot.LastUpdateUtc).TotalSeconds > _settings.StaleSeconds;
            }

            _detailsForm.Apply(snapshot, stale);
            _detailsForm.ShowNearCursor();
        }

        private void OnReconnect(object sender, EventArgs e)
        {
            _monitor.RequestReconnect();
        }

        private void OnToggleAutoStart(object sender, EventArgs e)
        {
            bool enable = !_autoStartItem.Checked;
            AutoStart.SetEnabled(enable);
            _autoStartItem.Checked = AutoStart.IsEnabled();
        }

        private void OnSelectNumberStyle(object sender, EventArgs e)
        {
            SetStyle(IconStyle.Number);
        }

        private void OnSelectBatteryStyle(object sender, EventArgs e)
        {
            SetStyle(IconStyle.Battery);
        }

        private void SetStyle(IconStyle style)
        {
            _settings.Style = style;
            _settings.Save();

            _numberStyleItem.Checked = style == IconStyle.Number;
            _batteryStyleItem.Checked = style == IconStyle.Battery;

            UpdateIcon(_shownPercent, _shownHasValue, _shownConnected, _shownStale);
        }

        private void OnExit(object sender, EventArgs e)
        {
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer.Dispose();
                }

                if (_monitor != null) _monitor.Dispose();

                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }

                if (_currentIcon != null)
                {
                    _currentIcon.Dispose();
                    _currentIcon = null;
                }

                if (_menu != null) _menu.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
