using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using G3Power.Core;
using Microsoft.Win32.SafeHandles;

namespace G3Power.Probe
{
    internal static class Probe
    {
        private const uint GENERIC_READ = 0x80000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode,
            IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetFeature(SafeFileHandle device, byte[] buffer, int bufferLength);

        private static int Main(string[] args)
        {
            int seconds = 60;
            bool listOnly = false;
            bool featureOnly = false;

            for (int i = 0; i < args.Length; i++)
            {
                string argument = args[i];
                if (argument == "--list") listOnly = true;
                else if (argument == "--feature") featureOnly = true;
                else if (argument == "--seconds" && i + 1 < args.Length)
                {
                    seconds = int.Parse(args[i + 1], CultureInfo.InvariantCulture);
                    i++;
                }
                else if (argument == "--help" || argument == "-h")
                {
                    PrintUsage();
                    return 0;
                }
            }

            if (listOnly)
            {
                PrintAll();
                return 0;
            }

            List<HidDeviceInfo> targets = HidDeviceEnumerator.FindMchose(
                MchoseG3Protocol.DefaultVendorId, MchoseG3Protocol.DefaultProductId);

            Console.WriteLine("目标设备 {0:X4}:{1:X4}  UsagePage 0xFF02 / Usage 0x0002",
                MchoseG3Protocol.DefaultVendorId, MchoseG3Protocol.DefaultProductId);

            if (targets.Count == 0)
            {
                Console.WriteLine("未找到目标集合。使用 --list 查看全部 HID 设备。");
                return 2;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                HidDeviceInfo info = targets[i];
                Console.WriteLine("  [{0}] {1}", i, info.Path);
                Console.WriteLine("      IN={0} OUT={1} FEAT={2} 版本=0x{3:X4}",
                    info.InputReportLength, info.OutputReportLength, info.FeatureReportLength, info.VersionNumber);
            }

            if (featureOnly)
            {
                DumpFeature(targets[0]);
                return 0;
            }

            Console.WriteLine();
            Console.WriteLine("开始监听 {0} 秒（Ctrl+C 退出）...", seconds);
            Console.WriteLine();

            BatteryMonitor monitor = new BatteryMonitor();
            monitor.FrameReceived += OnFrame;
            monitor.Start();

            DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
            while (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(200);
            }

            monitor.Stop();
            BatterySnapshot snapshot = monitor.GetSnapshot();
            monitor.Dispose();

            Console.WriteLine();
            Console.WriteLine("监听结束。最近电量：{0}，连接状态：{1}",
                snapshot.HasPercent ? snapshot.Percent + "%" : "未知",
                snapshot.IsConnected ? "已连接" : "未连接");

            return snapshot.HasPercent ? 0 : 3;
        }

        private static void OnFrame(byte[] frame, int length)
        {
            string hex = MchoseG3Protocol.ToHex(frame, length);

            G3Event value;
            string description;
            if (MchoseG3Protocol.TryParse(frame, length, out value))
                description = MchoseG3Protocol.Describe(value);
            else
                description = "非事件帧";

            Console.WriteLine("{0}  [{1}]  {2}", DateTime.Now.ToString("HH:mm:ss.fff"), hex, description);
        }

        private static void DumpFeature(HidDeviceInfo info)
        {
            SafeFileHandle handle = CreateFile(info.Path, GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);

            if (handle == null || handle.IsInvalid)
            {
                Console.WriteLine("打开设备失败，错误 {0}", Marshal.GetLastWin32Error());
                return;
            }

            try
            {
                byte[] buffer = new byte[MchoseG3Protocol.FeatureReportLength];
                buffer[0] = MchoseG3Protocol.ReportId;

                if (!HidD_GetFeature(handle, buffer, buffer.Length))
                {
                    Console.WriteLine("读取特征报告失败，错误 {0}", Marshal.GetLastWin32Error());
                    return;
                }

                Console.WriteLine();
                Console.WriteLine("特征报告（前 32 字节）：");
                Console.WriteLine("  " + MchoseG3Protocol.ToHex(buffer, 32));

                Console.WriteLine("非零字节：");
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i] == 0) continue;
                    Console.WriteLine("  [{0}] = 0x{1:X2}", i, buffer[i]);
                }
            }
            finally
            {
                handle.Dispose();
            }
        }

        private static void PrintAll()
        {
            List<HidDeviceInfo> devices = HidDeviceEnumerator.EnumerateAll();
            Console.WriteLine("共 {0} 个 HID 集合", devices.Count);

            for (int i = 0; i < devices.Count; i++)
            {
                HidDeviceInfo info = devices[i];
                Console.WriteLine();
                Console.WriteLine("[{0}] {1:X4}:{2:X4}  \"{3}\"", i, info.VendorId, info.ProductId, info.DisplayName);
                Console.WriteLine("    UP=0x{0:X4} U=0x{1:X4}  IN={2} OUT={3} FEAT={4}",
                    info.UsagePage, info.Usage, info.InputReportLength, info.OutputReportLength, info.FeatureReportLength);
                Console.WriteLine("    " + info.Path);
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("probe.exe [--list] [--feature] [--seconds N]");
            Console.WriteLine("  --list      列出全部 HID 集合");
            Console.WriteLine("  --feature   读取一次 520 字节特征报告");
            Console.WriteLine("  --seconds N 监听秒数，默认 60");
        }
    }
}
