using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace TradingClock
{
    static class Program
    {
        const string WindowTitle = "TradingClockWidget";

        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        static string LogPath()
        {
            try
            {
                string d = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TradingClock");
                Directory.CreateDirectory(d);
                return Path.Combine(d, "error.log");
            }
            catch { return null; }
        }

        static void Log(string msg)
        {
            try
            {
                string p = LogPath();
                if (p != null) File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine);
            }
            catch { }
        }

        [STAThread]
        static void Main()
        {
            Log("---- start pid=" + System.Diagnostics.Process.GetCurrentProcess().Id);
            try
            {
                try { SetProcessDPIAware(); Log("dpi ok"); }
                catch (Exception ex) { Log("dpi fail: " + ex.Message); }

                IntPtr existing = FindWindow(null, WindowTitle);
                Log("findwindow=" + existing);
                if (existing != IntPtr.Zero)
                {
                    ShowWindow(existing, 5);
                    SetForegroundWindow(existing);
                    return;
                }

                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
                {
                    Log("ThreadException: " + e.Exception);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
                {
                    Log("Unhandled: " + e.ExceptionObject);
                };

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Log("creating form");
                WidgetForm f = new WidgetForm(WindowTitle);
                Log("form created, running");
                Application.Run(f);
                Log("exited run");
            }
            catch (Exception ex)
            {
                Log("FATAL: " + ex);
            }
        }
    }
}
