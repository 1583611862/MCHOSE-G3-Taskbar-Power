using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace G3Power.Core
{
    public static class HidDeviceEnumerator
    {
        public static List<HidDeviceInfo> EnumerateAll()
        {
            List<HidDeviceInfo> result = new List<HidDeviceInfo>();
            Guid hidGuid = new Guid("4d1e55b2-f16f-11cf-88cb-001111000030");

            IntPtr deviceInfoSet = NativeMethods.SetupDiGetClassDevs(
                ref hidGuid, IntPtr.Zero, IntPtr.Zero,
                NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_DEVICEINTERFACE);

            if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == new IntPtr(-1)) return result;

            try
            {
                int index = 0;
                while (true)
                {
                    NativeMethods.SP_DEVICE_INTERFACE_DATA interfaceData = new NativeMethods.SP_DEVICE_INTERFACE_DATA();
                    interfaceData.cbSize = Marshal.SizeOf(typeof(NativeMethods.SP_DEVICE_INTERFACE_DATA));

                    if (!NativeMethods.SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, index, ref interfaceData))
                        break;

                    index++;
                    string path = GetDevicePath(deviceInfoSet, ref interfaceData);
                    if (path == null) continue;

                    HidDeviceInfo info = Probe(path);
                    if (info != null) result.Add(info);
                }
            }
            finally
            {
                NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }

            return result;
        }

        public static List<HidDeviceInfo> FindMchose(ushort vendorId, ushort productId)
        {
            return Find(vendorId, productId, MchoseG3Protocol.UsagePage, MchoseG3Protocol.Usage);
        }

        public static List<HidDeviceInfo> Find(ushort vendorId, ushort productId, ushort usagePage, ushort usage)
        {
            List<HidDeviceInfo> all = EnumerateAll();
            List<HidDeviceInfo> matched = new List<HidDeviceInfo>();

            for (int i = 0; i < all.Count; i++)
            {
                HidDeviceInfo info = all[i];
                if (info.VendorId != vendorId) continue;
                if (info.ProductId != productId) continue;
                if (info.UsagePage != usagePage) continue;
                if (info.Usage != usage) continue;
                if (info.InputReportLength < MchoseG3Protocol.InputReportLength) continue;
                matched.Add(info);
            }

            return matched;
        }

        private static string GetDevicePath(IntPtr deviceInfoSet, ref NativeMethods.SP_DEVICE_INTERFACE_DATA interfaceData)
        {
            int requiredSize = 0;
            NativeMethods.SetupDiGetDeviceInterfaceDetail(
                deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out requiredSize, IntPtr.Zero);

            if (requiredSize <= 0) return null;

            IntPtr buffer = Marshal.AllocHGlobal(requiredSize);
            try
            {
                Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);

                int written = 0;
                if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(
                        deviceInfoSet, ref interfaceData, buffer, requiredSize, out written, IntPtr.Zero))
                    return null;

                IntPtr stringPointer = new IntPtr(buffer.ToInt64() + 4);
                return Marshal.PtrToStringUni(stringPointer);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static HidDeviceInfo Probe(string path)
        {
            SafeFileHandle handle;
            try
            {
                handle = NativeMethods.CreateFile(path, 0,
                    NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                    IntPtr.Zero, NativeMethods.OPEN_EXISTING, 0, IntPtr.Zero);
            }
            catch (Exception)
            {
                return null;
            }

            if (handle == null || handle.IsInvalid)
            {
                if (handle != null) handle.Dispose();
                return null;
            }

            try
            {
                NativeMethods.HIDD_ATTRIBUTES attributes = new NativeMethods.HIDD_ATTRIBUTES();
                attributes.Size = Marshal.SizeOf(typeof(NativeMethods.HIDD_ATTRIBUTES));
                if (!NativeMethods.HidD_GetAttributes(handle, ref attributes)) return null;

                HidDeviceInfo info = new HidDeviceInfo();
                info.Path = path;
                info.VendorId = attributes.VendorID;
                info.ProductId = attributes.ProductID;
                info.VersionNumber = attributes.VersionNumber;
                info.ProductName = ReadProductName(handle);

                IntPtr preparsed = IntPtr.Zero;
                if (NativeMethods.HidD_GetPreparsedData(handle, out preparsed) && preparsed != IntPtr.Zero)
                {
                    try
                    {
                        NativeMethods.HIDP_CAPS caps = new NativeMethods.HIDP_CAPS();
                        caps.Reserved = new ushort[17];
                        if (NativeMethods.HidP_GetCaps(preparsed, ref caps) == NativeMethods.HIDP_STATUS_SUCCESS)
                        {
                            info.UsagePage = caps.UsagePage;
                            info.Usage = caps.Usage;
                            info.InputReportLength = caps.InputReportByteLength;
                            info.OutputReportLength = caps.OutputReportByteLength;
                            info.FeatureReportLength = caps.FeatureReportByteLength;
                        }
                    }
                    finally
                    {
                        NativeMethods.HidD_FreePreparsedData(preparsed);
                    }
                }

                return info;
            }
            finally
            {
                handle.Dispose();
            }
        }

        private static string ReadProductName(SafeFileHandle handle)
        {
            try
            {
                byte[] buffer = new byte[254];
                if (!NativeMethods.HidD_GetProductString(handle, buffer, buffer.Length)) return string.Empty;

                string value = Encoding.Unicode.GetString(buffer);
                int terminator = value.IndexOf('\0');
                if (terminator >= 0) value = value.Substring(0, terminator);
                return value.Trim();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
