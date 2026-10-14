using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace PedalKeys
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // One copy at a time. A second launch (e.g. from the Startup entry plus a manual
            // start) just exits quietly.
            bool createdNew;
            using (Mutex single = new Mutex(true, @"Local\PedalKeys.SingleInstance", out createdNew))
            {
                if (!createdNew) return;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                string logPath = null;
                if (Array.IndexOf(args, "--log") >= 0)
                {
                    string dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PedalKeys");
                    Directory.CreateDirectory(dir);
                    logPath = Path.Combine(dir, "reports.log");
                }

                Application.Run(new TrayApp(logPath));
            }
        }
    }
}
