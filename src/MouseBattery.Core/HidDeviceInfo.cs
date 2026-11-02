using System;

namespace G3Power.Core
{
    public sealed class HidDeviceInfo
    {
        public string Path;
        public ushort VendorId;
        public ushort ProductId;
        public ushort VersionNumber;
        public ushort UsagePage;
        public ushort Usage;
        public int InputReportLength;
        public int OutputReportLength;
        public int FeatureReportLength;
        public string ProductName;

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(ProductName)) return ProductName;
                return string.Format("HID {0:X4}:{1:X4}", VendorId, ProductId);
            }
        }

        public string Id
        {
            get { return string.Format("{0:X4}:{1:X4}", VendorId, ProductId); }
        }
    }
}
