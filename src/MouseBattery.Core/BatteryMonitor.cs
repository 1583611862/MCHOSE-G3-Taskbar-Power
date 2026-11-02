using System;
using System.Collections.Generic;
using System.Threading;

namespace G3Power.Core
{
    public sealed class BatterySnapshot
    {
        public bool HasPercent;
        public int Percent;
        public bool IsConnected;
        public DateTime LastUpdateUtc;
        public string DeviceName;
        public string DeviceId;
        public string DevicePath;
        public string LastFrameHex;
        public string LastError;

        public BatterySnapshot Clone()
        {
            BatterySnapshot copy = new BatterySnapshot();
            copy.HasPercent = HasPercent;
            copy.Percent = Percent;
            copy.IsConnected = IsConnected;
            copy.LastUpdateUtc = LastUpdateUtc;
            copy.DeviceName = DeviceName;
            copy.DeviceId = DeviceId;
            copy.DevicePath = DevicePath;
            copy.LastFrameHex = LastFrameHex;
            copy.LastError = LastError;
            return copy;
        }
    }

    public sealed class BatteryMonitor : IDisposable
    {
        private readonly object _sync = new object();
        private readonly ManualResetEvent _stopEvent = new ManualResetEvent(false);

        private Thread _thread;
        private volatile bool _running;
        private volatile bool _reconnectRequested;
        private bool _disposed;

        private HidTransport _transport;
        private HidDeviceInfo _device;

        private bool _hasPercent;
        private int _percent;
        private bool _isConnected;
        private DateTime _lastUpdateUtc;
        private string _lastFrameHex;
        private string _lastError;

        public ushort VendorId { get; set; }
        public ushort ProductId { get; set; }
        public ushort UsagePage { get; set; }
        public ushort Usage { get; set; }
        public bool BcdDecode { get; set; }

        public event Action<byte[], int> FrameReceived;

        public BatteryMonitor()
        {
            VendorId = MchoseG3Protocol.DefaultVendorId;
            ProductId = MchoseG3Protocol.DefaultProductId;
            UsagePage = MchoseG3Protocol.UsagePage;
            Usage = MchoseG3Protocol.Usage;
        }

        public void Start()
        {
            if (_running) return;

            _running = true;
            _stopEvent.Reset();

            _thread = new Thread(Loop);
            _thread.IsBackground = true;
            _thread.Priority = ThreadPriority.BelowNormal;
            _thread.Name = "G3Power.BatteryMonitor";
            _thread.Start();
        }

        public void Stop()
        {
            if (!_running) return;

            _running = false;
            _stopEvent.Set();

            Thread thread = _thread;
            if (thread != null)
            {
                try { thread.Join(3000); }
                catch (Exception) { }
            }

            _thread = null;
            CloseTransport();
        }

        public void RequestReconnect()
        {
            _reconnectRequested = true;
        }

        public BatterySnapshot GetSnapshot()
        {
            lock (_sync)
            {
                BatterySnapshot snapshot = new BatterySnapshot();
                snapshot.HasPercent = _hasPercent;
                snapshot.Percent = _percent;
                snapshot.IsConnected = _isConnected;
                snapshot.LastUpdateUtc = _lastUpdateUtc;
                snapshot.LastFrameHex = _lastFrameHex;
                snapshot.LastError = _lastError;
                snapshot.DeviceName = _device != null ? _device.DisplayName : null;
                snapshot.DeviceId = _device != null ? _device.Id : null;
                snapshot.DevicePath = _device != null ? _device.Path : null;
                return snapshot;
            }
        }

        private void Loop()
        {
            int[] backoff = new int[] { 2000, 5000, 10000, 30000 };
            int attempt = 0;

            while (_running)
            {
                if (_reconnectRequested)
                {
                    _reconnectRequested = false;
                    CloseTransport();
                    SetDisconnected("手动重连");
                    attempt = 0;
                }

                if (_transport == null)
                {
                    HidDeviceInfo info = FindDevice();
                    if (info == null)
                    {
                        SetDisconnected("未找到设备");
                        Sleep(5000);
                        continue;
                    }

                    int openError;
                    HidTransport transport = HidTransport.Open(info.Path, out openError);
                    if (transport == null)
                    {
                        SetDisconnected("打开设备失败 (错误 " + openError + ")");
                        Sleep(backoff[Math.Min(attempt, backoff.Length - 1)]);
                        attempt++;
                        continue;
                    }

                    _transport = transport;
                    _device = info;
                    attempt = 0;
                    SetConnected();
                }

                byte[] buffer = new byte[MchoseG3Protocol.InputReportLength];
                bool timedOut;
                int error;
                int read = _transport.Read(buffer, 1000, out timedOut, out error);

                if (timedOut) continue;

                if (read <= 0)
                {
                    CloseTransport();
                    SetDisconnected("连接中断 (错误 " + error + ")");
                    Sleep(backoff[Math.Min(attempt, backoff.Length - 1)]);
                    attempt++;
                    continue;
                }

                HandleFrame(buffer, read);
            }

            CloseTransport();
        }

        private void HandleFrame(byte[] frame, int length)
        {
            string hex = MchoseG3Protocol.ToHex(frame, length);

            G3Event value;
            if (!MchoseG3Protocol.TryParse(frame, length, out value))
            {
                lock (_sync) { _lastFrameHex = hex; }
                RaiseFrame(frame, length);
                return;
            }

            lock (_sync)
            {
                _lastFrameHex = hex;
                _isConnected = true;
                _lastError = null;

                if (value.Type == G3EventType.Battery)
                {
                    _percent = DecodePercent(value.Value);
                    _hasPercent = true;
                    _lastUpdateUtc = DateTime.UtcNow;
                }
                else if (value.Type == G3EventType.DeviceState)
                {
                    _isConnected = value.Value == 1;
                }
            }

            RaiseFrame(frame, length);
        }

        private void RaiseFrame(byte[] frame, int length)
        {
            Action<byte[], int> handler = FrameReceived;
            if (handler == null) return;

            try { handler(frame, length); }
            catch (Exception) { }
        }

        private HidDeviceInfo FindDevice()
        {
            List<HidDeviceInfo> devices = HidDeviceEnumerator.Find(VendorId, ProductId, UsagePage, Usage);
            if (devices.Count == 0) return null;

            if (_device != null)
            {
                for (int i = 0; i < devices.Count; i++)
                {
                    if (string.Equals(devices[i].Path, _device.Path, StringComparison.OrdinalIgnoreCase))
                        return devices[i];
                }
            }

            return devices[0];
        }

        private int DecodePercent(byte raw)
        {
            if (!BcdDecode) return MchoseG3Protocol.ClampPercent(raw);

            int high = (raw >> 4) & 0x0F;
            int low = raw & 0x0F;
            if (high > 9 || low > 9) return MchoseG3Protocol.ClampPercent(raw);

            return MchoseG3Protocol.ClampPercent(high * 10 + low);
        }

        private void SetConnected()
        {
            lock (_sync)
            {
                _isConnected = true;
                _lastError = null;
            }
        }

        private void SetDisconnected(string reason)
        {
            lock (_sync)
            {
                _isConnected = false;
                _lastError = reason;
            }
        }

        private void Sleep(int milliseconds)
        {
            if (!_running) return;
            _stopEvent.WaitOne(milliseconds);
        }

        private void CloseTransport()
        {
            HidTransport transport = _transport;
            _transport = null;

            if (transport != null)
            {
                try { transport.Dispose(); }
                catch (Exception) { }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Stop();
            _stopEvent.Close();
        }
    }
}
