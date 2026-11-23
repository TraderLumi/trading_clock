using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using TradingClock;

/// <summary>
/// 离屏渲染工具：把若干固定时刻的小窗画面渲染成 PNG，用于核对界面。
/// 不打开任何窗口，所以在没有桌面交互的环境里也能跑。
/// 用法：preview.exe 输出目录 [倍数]
/// </summary>
static class Preview
{
    [STAThread]
    static int Main(string[] args)
    {
        string dir = args.Length > 0 ? args[0] : "preview";
        float scale = 2f;
        if (args.Length > 1) float.TryParse(args[1], out scale);
        Directory.CreateDirectory(dir);

        // { 名称, 北京时间(NOW=当前), 视图(day/week), 偏移 }
        string[][] cases = new string[][]
        {
            new string[] { "1-同步夏令-主攻",  "2026-06-15 21:30", "day",  "0" },
            new string[] { "2-同步冬令-主攻",  "2026-12-15 21:30", "day",  "0" },
            new string[] { "3-春季错位-轻仓",  "2026-03-17 21:00", "day",  "0" },
            new string[] { "4-秋季错位-轻仓",  "2026-10-28 22:00", "day",  "0" },
            new string[] { "5-亚盘-布局",      "2026-06-15 10:00", "day",  "0" },
            new string[] { "6-欧盘-试单",      "2026-06-15 16:00", "day",  "0" },
            new string[] { "7-错位期禁开单",   "2026-10-28 17:00", "day",  "0" },
            new string[] { "8-周六休市",       "2026-06-20 12:00", "day",  "0" },
            new string[] { "9-切换日",         "2026-10-25 08:00", "day",  "0" },
            new string[] { "10-美盘独走",      "2026-06-15 23:30", "day",  "0" },
            new string[] { "11-感恩节清淡",    "2026-11-26 22:00", "day",  "0" },
            new string[] { "12-圣诞全休",      "2026-12-25 22:00", "day",  "0" },
            new string[] { "13-国内假期",      "2027-10-01 10:00", "day",  "0" },
            new string[] { "14-周视图-本周",   "NOW",              "week", "0" },
            new string[] { "15-周视图-下周",   "NOW",              "week", "1" },
            new string[] { "16-明天-无现在标记","NOW",             "day",  "1" },
            new string[] { "17-实时",          "NOW",              "day",  "0" }
        };

        for (int i = 0; i < cases.Length; i++)
        {
            string name = cases[i][0];
            string when = cases[i][1];
            bool week = cases[i][2] == "week";
            int shift = int.Parse(cases[i][3]);

            DateTime utc;
            if (when == "NOW") utc = DateTime.UtcNow;
            else utc = DateTime.ParseExact(when, "yyyy-MM-dd HH:mm", null).AddHours(-8);

            Snapshot snap = TimeEngine.Compute(utc, week, week ? 0 : shift, week ? shift : 0);

            int w = (int)Math.Round(WidgetRenderer.W * scale);
            int h = (int)Math.Round(WidgetRenderer.H * scale);

            using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(0, 0, 0, 0));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath clip = WidgetRenderer.RoundedPath(
                    new RectangleF(0, 0, w, h), 12f * scale))
                {
                    g.SetClip(clip);
                    WidgetRenderer.Render(g, snap, scale);
                }
                bmp.Save(Path.Combine(dir, name + ".png"), ImageFormat.Png);
            }

            Console.WriteLine(name + "  <- " + when + "  [" +
                              (week ? "周" : "日") + " " + snap.ViewDate.ToString("MM-dd") +
                              " / " + snap.ModeName + " / " + snap.LevelTitle +
                              " / now=" + (snap.ShowNow ? "on" : "off") + "]");
        }

        Console.WriteLine("done -> " + Path.GetFullPath(dir));
        return 0;
    }
}
