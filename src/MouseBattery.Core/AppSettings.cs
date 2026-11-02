using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace G3Power.Core
{
    public enum IconStyle
    {
        Number = 0,
        Battery = 1
    }

    public sealed class AppSettings
    {
        public IconStyle Style;
        public int StaleSeconds;
        public ushort VendorId;
        public ushort ProductId;
        public bool BcdDecode;

        public AppSettings()
        {
            Style = IconStyle.Number;
            StaleSeconds = 180;
            VendorId = MchoseG3Protocol.DefaultVendorId;
            ProductId = MchoseG3Protocol.DefaultProductId;
            BcdDecode = false;
        }

        public static string DirectoryPath
        {
            get
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(root, "G3Power");
            }
        }

        public static string FilePath
        {
            get { return Path.Combine(DirectoryPath, "settings.ini"); }
        }

        public static AppSettings Load()
        {
            AppSettings settings = new AppSettings();

            try
            {
                if (!File.Exists(FilePath)) return settings;

                string[] lines = File.ReadAllLines(FilePath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int separator = line.IndexOf('=');
                    if (separator <= 0) continue;

                    string key = line.Substring(0, separator).Trim();
                    string value = line.Substring(separator + 1).Trim();

                    if (key == "IconStyle")
                    {
                        int parsed;
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                            settings.Style = parsed == 1 ? IconStyle.Battery : IconStyle.Number;
                    }
                    else if (key == "StaleSeconds")
                    {
                        int parsed;
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) && parsed >= 30)
                            settings.StaleSeconds = parsed;
                    }
                    else if (key == "VendorId")
                    {
                        ushort parsed;
                        if (TryParseHex(value, out parsed)) settings.VendorId = parsed;
                    }
                    else if (key == "ProductId")
                    {
                        ushort parsed;
                        if (TryParseHex(value, out parsed)) settings.ProductId = parsed;
                    }
                    else if (key == "BcdDecode")
                    {
                        settings.BcdDecode = value == "1";
                    }
                }
            }
            catch (Exception)
            {
            }

            return settings;
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(DirectoryPath)) Directory.CreateDirectory(DirectoryPath);

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("IconStyle=" + ((int)Style).ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("StaleSeconds=" + StaleSeconds.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("VendorId=0x" + VendorId.ToString("X4"));
                builder.AppendLine("ProductId=0x" + ProductId.ToString("X4"));
                builder.AppendLine("BcdDecode=" + (BcdDecode ? "1" : "0"));
                File.WriteAllText(FilePath, builder.ToString(), Encoding.UTF8);
            }
            catch (Exception)
            {
            }
        }

        private static bool TryParseHex(string value, out ushort result)
        {
            result = 0;
            if (string.IsNullOrEmpty(value)) return false;

            string text = value.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text.Substring(2);

            return ushort.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);
        }
    }
}
