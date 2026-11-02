using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace G3Power.Tray
{
    internal static class Program
    {
        private const string MutexName = "G3Power.SingleInstance";

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew) return;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += OnThreadException;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

                using (TrayContext context = new TrayContext())
                {
                    Application.Run(context);
                }
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            WriteError(e.Exception);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            WriteError(e.ExceptionObject as Exception);
        }

        private static void WriteError(Exception exception)
        {
            if (exception == null) return;

            try
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "G3Power");
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

                StringBuilder builder = new StringBuilder();
                builder.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                builder.AppendLine(exception.ToString());
                builder.AppendLine();
                File.AppendAllText(Path.Combine(directory, "error.log"), builder.ToString(), Encoding.UTF8);
            }
            catch (Exception)
            {
            }
        }
    }
}
