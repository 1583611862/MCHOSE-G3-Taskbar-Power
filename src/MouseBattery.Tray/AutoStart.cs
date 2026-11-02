using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace G3Power.Tray
{
    internal static class AutoStart
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "G3Power";

        public static string ExecutablePath
        {
            get
            {
                string path = Application.ExecutablePath;
                if (!string.IsNullOrEmpty(path)) return path;
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "G3Power.exe");
            }
        }

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return false;
                    string value = key.GetValue(ValueName) as string;
                    return !string.IsNullOrEmpty(value);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (key == null) return;

                    if (enabled)
                        key.SetValue(ValueName, "\"" + ExecutablePath + "\"", RegistryValueKind.String);
                    else
                        key.DeleteValue(ValueName, false);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
