using System;
using System.IO;
using Microsoft.Win32;

namespace TradingClock
{
    /// <summary>
    /// 配置：%APPDATA%\TradingClock\config.ini
    /// 只有 5 个字段，用 ini 而不是 json，省掉一个 JSON 库依赖。
    /// </summary>
    public class Config
    {
        public int X;
        public int Y;
        public bool HasPos;
        public bool AlwaysOnTop = true;
        public int Opacity = 96;

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "TradingClock";

        static string DirPath()
        {
            string d = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TradingClock");
            Directory.CreateDirectory(d);
            return d;
        }

        static string FilePath()
        {
            return Path.Combine(DirPath(), "config.ini");
        }

        public static Config Load()
        {
            Config c = new Config();
            try
            {
                string path = FilePath();
                if (!File.Exists(path)) return c;
                string[] lines = File.ReadAllLines(path);
                foreach (string raw in lines)
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();
                    if (k == "x") { int n; if (int.TryParse(v, out n)) { c.X = n; c.HasPos = true; } }
                    else if (k == "y") { int n; if (int.TryParse(v, out n)) { c.Y = n; c.HasPos = true; } }
                    else if (k == "topmost") c.AlwaysOnTop = v == "1";
                    else if (k == "opacity") { int n; if (int.TryParse(v, out n) && n >= 40 && n <= 100) c.Opacity = n; }
                }
            }
            catch { }
            return c;
        }

        public void Save()
        {
            try
            {
                string[] lines = new string[]
                {
                    "# TradingClock 配置",
                    "x=" + X,
                    "y=" + Y,
                    "topmost=" + (AlwaysOnTop ? "1" : "0"),
                    "opacity=" + Opacity
                };
                File.WriteAllLines(FilePath(), lines, new System.Text.UTF8Encoding(false));
            }
            catch { }
        }

        public static bool IsAutostartEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null) return false;
                    return k.GetValue(RunName) != null;
                }
            }
            catch { return false; }
        }

        public static void SetAutostart(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (on)
                    {
                        string exe = System.Reflection.Assembly.GetEntryAssembly().Location;
                        k.SetValue(RunName, "\"" + exe + "\"");
                    }
                    else
                    {
                        if (k.GetValue(RunName) != null) k.DeleteValue(RunName, false);
                    }
                }
            }
            catch { }
        }
    }
}
