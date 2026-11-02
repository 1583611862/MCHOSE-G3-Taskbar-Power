using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace G3Power.Core
{
    public sealed class BatteryCacheEntry
    {
        public bool HasPercent;
        public int Percent;
        public DateTime Utc;
        public string DeviceName;
    }

    public static class BatteryStateStore
    {
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
            get { return Path.Combine(DirectoryPath, "state.txt"); }
        }

        public static BatteryCacheEntry Load()
        {
            BatteryCacheEntry entry = new BatteryCacheEntry();
            try
            {
                if (!File.Exists(FilePath)) return entry;

                string[] lines = File.ReadAllLines(FilePath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int separator = line.IndexOf('=');
                    if (separator <= 0) continue;

                    string key = line.Substring(0, separator).Trim();
                    string value = line.Substring(separator + 1).Trim();

                    if (key == "percent")
                    {
                        int parsed;
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                        {
                            entry.Percent = parsed;
                            entry.HasPercent = true;
                        }
                    }
                    else if (key == "utc")
                    {
                        DateTime parsed;
                        if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out parsed))
                            entry.Utc = parsed;
                    }
                    else if (key == "name")
                    {
                        entry.DeviceName = value;
                    }
                }
            }
            catch (Exception)
            {
            }

            return entry;
        }

        public static void Save(int percent, DateTime utc, string deviceName)
        {
            try
            {
                if (!Directory.Exists(DirectoryPath)) Directory.CreateDirectory(DirectoryPath);

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("percent=" + percent.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("utc=" + utc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
                builder.AppendLine("name=" + (deviceName == null ? string.Empty : deviceName));
                File.WriteAllText(FilePath, builder.ToString(), Encoding.UTF8);
            }
            catch (Exception)
            {
            }
        }
    }
}
