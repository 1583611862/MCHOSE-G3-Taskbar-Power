using System;
using System.Text;

namespace G3Power.Core
{
    public enum G3EventType
    {
        DeviceState = 2,
        Dpi = 3,
        Reserved = 4,
        Battery = 5,
        Ack = 6
    }

    public struct G3Event
    {
        public G3EventType Type;
        public byte Value;
    }

    public static class MchoseG3Protocol
    {
        public const byte ReportId = 0x09;
        public const byte EventMarker = 0x0A;

        public const ushort UsagePage = 0xFF02;
        public const ushort Usage = 0x0002;

        public const int InputReportLength = 8;
        public const int FeatureReportLength = 520;

        public const ushort DefaultVendorId = 0x258A;
        public const ushort DefaultProductId = 0x0138;

        public static bool TryParse(byte[] frame, int length, out G3Event result)
        {
            result = new G3Event();

            if (frame == null) return false;
            if (length < InputReportLength) return false;
            if (frame[0] != ReportId) return false;
            if (frame[1] != EventMarker) return false;

            byte type = frame[2];
            if (type < 2 || type > 6) return false;

            result.Type = (G3EventType)type;
            result.Value = frame[3];
            return true;
        }

        public static int ClampPercent(int value)
        {
            if (value < 0) return 0;
            if (value > 100) return 100;
            return value;
        }

        public static string ToHex(byte[] data, int length)
        {
            if (data == null) return string.Empty;
            int count = length;
            if (count > data.Length) count = data.Length;

            StringBuilder builder = new StringBuilder(count * 3);
            for (int i = 0; i < count; i++)
            {
                if (i > 0) builder.Append(' ');
                builder.Append(data[i].ToString("X2"));
            }
            return builder.ToString();
        }

        public static string Describe(G3Event value)
        {
            switch (value.Type)
            {
                case G3EventType.DeviceState:
                    return value.Value == 1 ? "设备接入" : "设备断开";
                case G3EventType.Dpi:
                    return "DPI 变更 (0x" + value.Value.ToString("X2") + ")";
                case G3EventType.Battery:
                    return "电量 " + ClampPercent(value.Value) + "%";
                case G3EventType.Ack:
                    return "命令回执 (" + (value.Value != 0 ? "成功" : "失败") + ")";
                default:
                    return "保留事件 (0x" + value.Value.ToString("X2") + ")";
            }
        }
    }
}
