using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace G3Power.Core
{
    public sealed class HidTransport : IDisposable
    {
        private SafeFileHandle _handle;
        private IntPtr _readEvent;
        private bool _disposed;
        private int _lastError;

        public string DevicePath { get; private set; }
        public int LastError { get { return _lastError; } }
        public bool IsOpen { get { return !_disposed && _handle != null && !_handle.IsInvalid; } }

        private HidTransport()
        {
        }

        public static HidTransport Open(string path, out int error)
        {
            error = 0;

            SafeFileHandle handle = NativeMethods.CreateFile(
                path,
                NativeMethods.GENERIC_READ,
                NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                IntPtr.Zero,
                NativeMethods.OPEN_EXISTING,
                NativeMethods.FILE_FLAG_OVERLAPPED,
                IntPtr.Zero);

            if (handle == null || handle.IsInvalid)
            {
                if (handle != null) handle.Dispose();

                handle = NativeMethods.CreateFile(
                    path,
                    NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
                    NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    NativeMethods.OPEN_EXISTING,
                    NativeMethods.FILE_FLAG_OVERLAPPED,
                    IntPtr.Zero);

                if (handle == null || handle.IsInvalid)
                {
                    error = Marshal.GetLastWin32Error();
                    if (handle != null) handle.Dispose();
                    return null;
                }
            }

            IntPtr readEvent = NativeMethods.CreateEvent(IntPtr.Zero, true, false, null);
            if (readEvent == IntPtr.Zero)
            {
                error = Marshal.GetLastWin32Error();
                handle.Dispose();
                return null;
            }

            HidTransport transport = new HidTransport();
            transport._handle = handle;
            transport._readEvent = readEvent;
            transport.DevicePath = path;
            return transport;
        }

        public int Read(byte[] buffer, int timeoutMs, out bool timedOut, out int error)
        {
            timedOut = false;
            error = 0;

            if (!IsOpen)
            {
                error = NativeMethods.ERROR_INVALID_HANDLE;
                return -1;
            }

            NativeMethods.OVERLAPPED overlapped = new NativeMethods.OVERLAPPED();
            overlapped.hEvent = _readEvent;
            NativeMethods.ResetEvent(_readEvent);

            int bytesRead = 0;
            bool ok = NativeMethods.ReadFile(_handle, buffer, buffer.Length, out bytesRead, ref overlapped);

            if (ok) return bytesRead;

            int lastError = Marshal.GetLastWin32Error();
            if (lastError != NativeMethods.ERROR_IO_PENDING)
            {
                _lastError = lastError;
                error = lastError;
                return -1;
            }

            int waitResult = NativeMethods.WaitForSingleObject(_readEvent, timeoutMs);
            if (waitResult == NativeMethods.WAIT_OBJECT_0)
            {
                int transferred = 0;
                if (!NativeMethods.GetOverlappedResult(_handle, ref overlapped, out transferred, false))
                {
                    int resultError = Marshal.GetLastWin32Error();
                    _lastError = resultError;
                    error = resultError;
                    return -1;
                }

                return transferred;
            }

            if (waitResult == NativeMethods.WAIT_TIMEOUT)
            {
                NativeMethods.CancelIoEx(_handle, ref overlapped);

                int drained = 0;
                NativeMethods.GetOverlappedResult(_handle, ref overlapped, out drained, true);

                timedOut = true;
                return 0;
            }

            _lastError = Marshal.GetLastWin32Error();
            error = _lastError;
            return -1;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_handle != null)
            {
                try { _handle.Dispose(); }
                catch (Exception) { }
                _handle = null;
            }

            if (_readEvent != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(_readEvent);
                _readEvent = IntPtr.Zero;
            }
        }
    }
}
