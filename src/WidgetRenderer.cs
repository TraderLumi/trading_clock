using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TradingClock
{
    /// <summary>
    /// 纯绘制层：给一个 Graphics 和一个快照，画出整块小窗。
    /// 不依赖窗口，所以可以用离屏渲染出图核对。
    /// </summary>
    public static class WidgetRenderer
    {
        public const int W = 380;
        public const int H = 242;

        // 交互热区（逻辑坐标，WidgetForm 复用）
        public static readonly RectangleF BtnDay = new RectangleF(300f, 6f, 32f, 18f);
        public static readonly RectangleF BtnWeek = new RectangleF(334f, 6f, 32f, 18f);
        public static readonly RectangleF BtnPrev = new RectangleF(272f, 26f, 22f, 18f);
        public static readonly RectangleF BtnToday = new RectangleF(296f, 26f, 44f, 18f);
        public static readonly RectangleF BtnNext = new RectangleF(342f, 26f, 22f, 18f);

        public static readonly Color C_Bg = Color.FromArgb(22, 24, 29);
        public static readonly Color C_Border = Color.FromArgb(44, 48, 58);
        public static readonly Color C_Text = Color.FromArgb(243, 244, 246);
        public static readonly Color C_Sub = Color.FromArgb(158, 165, 178);
        public static readonly Color C_Muted = Color.FromArgb(110, 117, 131);
        public static readonly Color C_Track = Color.FromArgb(30, 33, 41);
        public static readonly Color C_Grid = Color.FromArgb(40, 44, 54);
        public static readonly Color C_Asia = Color.FromArgb(59, 130, 246);
        public static readonly Color C_Eu = Color.FromArgb(139, 92, 246);
        public static readonly Color C_Us = Color.FromArgb(6, 182, 212);
        public static readonly Color C_Amber = Color.FromArgb(245, 158, 11);
        public static readonly Color C_Green = Color.FromArgb(34, 197, 94);
        public static readonly Color C_WeekTrack = Color.FromArgb(38, 42, 52);

        static readonly Font FTime;
        static readonly Font FMode;
        static readonly Font FChip;
        static readonly Font FRow;
        static readonly Font FSmall;
        static readonly Font FTiny;
        static readonly Font FUnit;
        static readonly Font FBtn;
        static readonly Font FWeekDay;

        static WidgetRenderer()
        {
            FTime = MakeFont(new string[] { "Consolas", "Courier New" }, 38f, FontStyle.Bold);
            FMode = MakeFont(new string[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" }, 15f, FontStyle.Bold);
            FChip = MakeFont(new string[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" }, 13f, FontStyle.Bold);
            FRow = MakeFont(new string[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" }, 11f, FontStyle.Bold);
            FSmall = MakeFont(new string[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" }, 11f, FontStyle.Regular);
            FTiny = MakeFont(new string[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" }, 10f, FontStyle.Regular);
            FUnit = MakeFont(new string[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" }, 11f, FontStyle.Regular);
            FBtn = MakeFont(new string[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" }, 10f, FontStyle.Regular);
            FWeekDay = MakeFont(new string[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" }, 9f, FontStyle.Bold);
        }

        static Font MakeFont(string[] families, float px, FontStyle style)
        {
            for (int i = 0; i < families.Length; i++)
            {
                Font f = new Font(families[i], px, style, GraphicsUnit.Pixel);
                if (f.Name == families[i]) return f;
                f.Dispose();
            }
            return new Font(FontFamily.GenericSansSerif, px, style, GraphicsUnit.Pixel);
        }

        public static Color LevelColor(Level l)
        {
            switch (l)
            {
                case Level.Main: return C_Green;
                case Level.Trial: return Color.FromArgb(234, 179, 8);
                case Level.Layout: return Color.FromArgb(96, 165, 250);
                case Level.Light: return Color.FromArgb(249, 115, 22);
                case Level.NoTrade: return Color.FromArgb(239, 68, 68);
                default: return Color.FromArgb(120, 126, 138);
            }
        }

        public static void Render(Graphics g, Snapshot s, float scale)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.ScaleTransform(scale, scale);

            Color accent = s.Preview ? Color.FromArgb(120, 126, 138) : LevelColor(s.Level);

            using (SolidBrush bg = new SolidBrush(C_Bg)) g.FillRectangle(bg, 0, 0, W, H);

            using (LinearGradientBrush lg = new LinearGradientBrush(
                new Rectangle(0, 0, W, 104),
                Color.FromArgb(34, accent), Color.FromArgb(0, accent),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(lg, 0, 0, W, 104);
            }

            bool switchDay = s.IsSwitchDay;
            using (GraphicsPath fp = RoundedPath(new RectangleF(0.5f, 0.5f, W - 1f, H - 1f), 12f))
            using (Pen pen = new Pen(switchDay ? Color.FromArgb(200, C_Amber) : C_Border, switchDay ? 1.8f : 1f))
            {
                g.DrawPath(pen, fp);
            }

            DrawClock(g, s);
            DrawControls(g, s);
            DrawModeLine(g, s);
            DrawChip(g, s, accent);

            if (s.WeekView) DrawWeek(g, s);
            else DrawTimeline(g, s);

            DrawFooter(g, s);
        }

        // ---------------- 头部 ----------------

        static void DrawClock(Graphics g, Snapshot s)
        {
            using (SolidBrush b = new SolidBrush(C_Text))
                g.DrawString(s.BeijingNow.ToString("HH:mm:ss"), FTime, b, 10f, -2f);

            string dateText;
            if (s.WeekView)
            {
                dateText = s.ViewDate.ToString("MM-dd") + "~" + s.ViewDate.AddDays(6).ToString("MM-dd");
                if (s.ViewIsToday) dateText = s.ViewDate.ToString("MM-dd") + "~" + s.ViewDate.AddDays(6).ToString("MM-dd");
            }
            else
            {
                dateText = s.ViewDate.ToString("MM-dd") + " " + TimeEngine.WeekdayCn(s.ViewDate);
            }

            Color dc = s.ViewIsToday ? C_Sub : C_Amber;
            using (SolidBrush b = new SolidBrush(dc))
                g.DrawString(dateText, FTiny, b, 194f, 33f);
        }

        static void DrawControls(Graphics g, Snapshot s)
        {
            DrawToggle(g, BtnDay, "日", !s.WeekView);
            DrawToggle(g, BtnWeek, "周", s.WeekView);
            DrawBtn(g, BtnPrev, "‹");
            DrawBtn(g, BtnToday, s.WeekView ? "本周" : "今天");
            DrawBtn(g, BtnNext, "›");
        }

        static void DrawToggle(Graphics g, RectangleF r, string text, bool active)
        {
            using (GraphicsPath p = RoundedPath(r, 4f))
            {
                using (SolidBrush fill = new SolidBrush(active ? Color.FromArgb(58, 62, 74) : Color.FromArgb(30, 33, 41)))
                    g.FillPath(fill, p);
                using (Pen pen = new Pen(active ? Color.FromArgb(90, 94, 106) : C_Border, 1f))
                    g.DrawPath(pen, p);
            }
            SizeF sz = g.MeasureString(text, FBtn);
            using (SolidBrush tb = new SolidBrush(active ? C_Text : C_Muted))
                g.DrawString(text, FBtn, tb, r.X + (r.Width - sz.Width) / 2f, r.Y + (r.Height - sz.Height) / 2f);
        }

        static void DrawBtn(Graphics g, RectangleF r, string text)
        {
            using (GraphicsPath p = RoundedPath(r, 4f))
            {
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(30, 33, 41))) g.FillPath(fill, p);
                using (Pen pen = new Pen(C_Border, 1f)) g.DrawPath(pen, p);
            }
            SizeF sz = g.MeasureString(text, FBtn);
            using (SolidBrush tb = new SolidBrush(C_Sub))
                g.DrawString(text, FBtn, tb, r.X + (r.Width - sz.Width) / 2f, r.Y + (r.Height - sz.Height) / 2f);
        }

        static void DrawModeLine(Graphics g, Snapshot s)
        {
            string modeLine;
            if (s.Preview)
                modeLine = s.ModeName + " · " + (s.WeekView ? "该周时间表" : TimeEngine.WeekdayCn(s.ViewDate));
            else
                modeLine = s.ModeName + " · " + s.ActiveWindow;
            using (SolidBrush b = new SolidBrush(C_Text))
                g.DrawString(modeLine, FMode, b, 14f, 48f);

            if (s.HolidayTag != null && s.HolidayTag.Length > 0)
            {
                SizeF sz = g.MeasureString(s.HolidayTag, FUnit);
                using (SolidBrush hb = new SolidBrush(C_Amber))
                    g.DrawString(s.HolidayTag, FUnit, hb, W - 14f - sz.Width, 53f);
            }
        }

        static void DrawChip(Graphics g, Snapshot s, Color accent)
        {
            string title = s.Preview ? "预览" : s.LevelTitle;
            SizeF sz = g.MeasureString(title, FChip);
            float h = 24f;
            float w = sz.Width + 30f;
            float x = 14f;
            float y = 76f;

            using (GraphicsPath p = RoundedPath(new RectangleF(x, y, w, h), h / 2f))
            {
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(30, accent))) g.FillPath(fill, p);
                using (Pen pen = new Pen(Color.FromArgb(110, accent), 1f)) g.DrawPath(pen, p);
            }
            using (SolidBrush dot = new SolidBrush(accent))
                g.FillEllipse(dot, x + 10f, y + h / 2f - 3.5f, 7f, 7f);
            using (SolidBrush tb = new SolidBrush(accent))
                g.DrawString(title, FChip, tb, x + 22f, y + 4f);

            using (SolidBrush rb = new SolidBrush(C_Sub))
                g.DrawString(s.Preview ? "仅时间表 · 非实时" : WindowText(s), FSmall, rb, x + w + 10f, y + 5f);
        }

        static string WindowText(Snapshot s)
        {
            if (s.ClosedNow) return s.CloseReason;
            SessionWindows w = s.Win;
            if (s.ActiveWindow == "欧美重叠") return TimeText(w.UsStart) + "–" + TimeText(w.EuEnd) + " · " + s.LevelDetail;
            if (s.ActiveWindow == "欧盘") return TimeText(w.EuStart) + "–" + TimeText(w.EuEnd) + " · " + s.LevelDetail;
            if (s.ActiveWindow == "美盘") return TimeText(w.UsStart) + "–" + TimeText(w.UsEnd) + " · " + s.LevelDetail;
            if (s.ActiveWindow == "亚盘") return "08:00–17:00 · " + s.LevelDetail;
            return s.LevelDetail;
        }

        static string TimeText(int minutes)
        {
            int m = minutes % 1440;
            return (m / 60).ToString("00") + ":" + (m % 60).ToString("00");
        }

        // ---------------- 日视图 ----------------

        static void DrawTimeline(Graphics g, Snapshot s)
        {
            int barX = 40;
            int barW = W - 14 - barX;
            float perMinute = barW / 1440f;

            int[] rowY = new int[] { 116, 136, 156 };
            Color[] rowColor = new Color[] { C_Asia, C_Eu, C_Us };
            string[] rowLabel = new string[] { "亚", "欧", "美" };

            using (Pen gp = new Pen(C_Grid, 1f))
            {
                for (int h = 0; h <= 24; h += 3)
                {
                    float x = barX + h * 60 * perMinute;
                    g.DrawLine(gp, x, 112f, x, 166f);
                }
            }

            for (int i = 0; i < 3; i++)
            {
                float y = rowY[i];
                RectangleF track = new RectangleF(barX, y, barW, 10f);

                using (GraphicsPath clip = RoundedPath(track, 5f))
                {
                    GraphicsState st = g.Save();
                    g.SetClip(clip, CombineMode.Replace);
                    using (SolidBrush tb = new SolidBrush(C_Track)) g.FillRectangle(tb, track);
                    DrawSegs(g, perMinute, barX, y, 10f, s.Rows[i], rowColor[i]);
                    g.Restore(st);
                }

                using (SolidBrush lb = new SolidBrush(C_Sub))
                    g.DrawString(rowLabel[i], FRow, lb, 16f, y - 1f);
            }

            Color oc = s.Mode == SessionMode.Mixed
                ? Color.FromArgb(250, 204, 21)
                : C_Green;
            for (int i = 1; i <= 2; i++)
            {
                float y = rowY[i] + 2f;
                RectangleF track = new RectangleF(barX, y, barW, 6f);
                using (GraphicsPath clip = RoundedPath(track, 3f))
                {
                    GraphicsState st = g.Save();
                    g.SetClip(clip, CombineMode.Replace);
                    DrawSegs(g, perMinute, barX, y, 6f, s.Overlap, oc);
                    g.Restore(st);
                }
            }

            if (s.ShowNow)
            {
                int mins = s.BeijingNow.Hour * 60 + s.BeijingNow.Minute;
                float nowX = barX + mins * perMinute;
                using (Pen np = new Pen(Color.FromArgb(235, 255, 255, 255), 1.4f))
                    g.DrawLine(np, nowX, 112f, nowX, 166f);
                using (SolidBrush nb = new SolidBrush(Color.White))
                {
                    PointF[] tri = new PointF[]
                    {
                        new PointF(nowX - 4f, 108f),
                        new PointF(nowX + 4f, 108f),
                        new PointF(nowX, 114f)
                    };
                    g.FillPolygon(nb, tri);
                }
            }

            DrawHourLabels(g, barX, barW, perMinute, 168f);
        }

        static void DrawHourLabels(Graphics g, int barX, int barW, float perMinute, float y)
        {
            int[] hours = new int[] { 0, 6, 12, 18, 24 };
            using (SolidBrush hb = new SolidBrush(C_Muted))
            {
                for (int i = 0; i < hours.Length; i++)
                {
                    string t = hours[i].ToString("00");
                    SizeF sz = g.MeasureString(t, FTiny);
                    float x = barX + hours[i] * 60 * perMinute - sz.Width / 2f;
                    if (i == 0) x = barX;
                    if (i == hours.Length - 1) x = barX + barW - sz.Width;
                    g.DrawString(t, FTiny, hb, x, y);
                }
            }
        }

        static void DrawSegs(Graphics g, float perMinute, int barX, float y, float h,
                             List<Seg> segs, Color color)
        {
            if (segs == null) return;
            for (int i = 0; i < segs.Count; i++)
            {
                Seg sg = segs[i];
                int cs = Math.Max(sg.Start, 0);
                int ce = Math.Min(sg.End, 1440);
                if (ce <= cs) continue;

                Color c = sg.Dim ? Color.FromArgb(105, color) : color;
                using (SolidBrush sb = new SolidBrush(c))
                {
                    float x0 = barX + cs * perMinute;
                    float x1 = barX + ce * perMinute;
                    g.FillRectangle(sb, x0, y, x1 - x0, h);
                }
            }
        }

        // ---------------- 周视图 ----------------

        static void DrawWeek(Graphics g, Snapshot s)
        {
            int x0 = 40;
            int x1 = W - 14;
            float cw = (x1 - x0) / 24f;
            float top = 98f;
            float bottom = 172f;
            float rh = (bottom - top) / 7f;
            string[] labels = { "一", "二", "三", "四", "五", "六", "日" };

            using (Pen gp = new Pen(C_Grid, 1f))
            {
                for (int h = 0; h <= 24; h += 3)
                {
                    float x = x0 + h * cw;
                    g.DrawLine(gp, x, top - 1f, x, bottom);
                }
            }

            for (int d = 0; d < 7; d++)
            {
                float y = top + d * rh;
                RectangleF track = new RectangleF(x0, y + 1f, x1 - x0, rh - 2f);

                using (GraphicsPath clip = RoundedPath(track, 2.5f))
                {
                    GraphicsState st = g.Save();
                    g.SetClip(clip, CombineMode.Replace);
                    using (SolidBrush tb = new SolidBrush(C_WeekTrack)) g.FillRectangle(tb, track);

                    for (int h = 0; h < 24; h++)
                    {
                        int cell = s.WeekGrid[d, h];
                        if (cell == 0) continue;
                        using (SolidBrush cb = new SolidBrush(CellColor((CellState)cell, s.Mode)))
                            g.FillRectangle(cb, x0 + h * cw, y + 1f, cw - 0.5f, rh - 2f);
                    }
                    g.Restore(st);
                }

                bool isToday = s.ViewDate.AddDays(d) == s.BeijingNow.Date;
                SizeF ls = g.MeasureString(labels[d], FWeekDay);
                using (SolidBrush lb = new SolidBrush(isToday ? C_Text : C_Muted))
                    g.DrawString(labels[d], FWeekDay, lb, 22f - ls.Width / 2f, y + rh / 2f - ls.Height / 2f);
            }

            if (s.ShowNow)
            {
                float nowX = x0 + (s.BeijingNow.Hour + s.BeijingNow.Minute / 60f) * cw;
                using (Pen np = new Pen(Color.FromArgb(235, 255, 255, 255), 1.4f))
                    g.DrawLine(np, nowX, top - 1f, nowX, bottom);
            }

            DrawHourLabels(g, x0, x1 - x0, cw, 174f);
        }

        static Color CellColor(CellState st, SessionMode mode)
        {
            switch (st)
            {
                case CellState.Asia: return Color.FromArgb(150, C_Asia);
                case CellState.Eu: return Color.FromArgb(190, C_Eu);
                case CellState.Us: return Color.FromArgb(190, C_Us);
                case CellState.Overlap:
                    return mode == SessionMode.Mixed ? Color.FromArgb(215, 250, 204, 21) : C_Green;
                default: return Color.Transparent;
            }
        }

        // ---------------- 底部 ----------------

        static void DrawFooter(Graphics g, Snapshot s)
        {
            string line1;
            if (s.WeekView)
            {
                line1 = (s.ViewIsToday ? "本周" : "该周") + "可交易 " +
                        TimeEngine.Hours(s.ViewHours) + " 小时";
            }
            else if (!s.ViewIsToday)
            {
                line1 = "该日可交易 " + TimeEngine.Hours(s.ViewHours) + " 小时";
            }
            else if (s.ClosedNow)
            {
                line1 = "开盘 " + TimeEngine.WeekdayCn(s.NextOpen) + " " +
                        s.NextOpen.ToString("HH:mm") + " · 还有 " +
                        TimeEngine.Human(s.NextOpen - s.BeijingNow);
            }
            else if (s.Nodes != null && s.Nodes.Count > 0)
            {
                NodeInfo n = s.Nodes[0];
                line1 = "下一节点 " + n.Label + " " + n.Time.ToString("HH:mm") +
                        " · 还有 " + TimeEngine.Human(n.Time - s.BeijingNow);
            }
            else
            {
                line1 = "今日无更多节点";
            }

            using (SolidBrush b = new SolidBrush(C_Sub))
                g.DrawString(line1, FSmall, b, 14f, 190f);

            if (s.WeekView) DrawLegend(g, 252f, 191f);

            string line2 = "";
            if (s.NextSwitchTime != default(DateTime))
            {
                if (s.NextSwitchTime.Date == s.BeijingNow.Date)
                {
                    line2 = "今日切换 " + s.NextSwitchLabel + " " +
                            s.NextSwitchTime.ToString("HH:mm") + " · 还有 " +
                            TimeEngine.Human(s.NextSwitchIn);
                }
                else if (s.IsSwitchDay)
                {
                    line2 = "今日已切换 · " + s.SwitchNote;
                }
                else
                {
                    line2 = "换季 " + s.NextSwitchLabel + " " +
                            s.NextSwitchTime.ToString("yyyy-MM-dd HH:mm") + " · " +
                            TimeEngine.Human(s.NextSwitchIn);
                }
            }

            Color c2 = s.IsSwitchDay ? C_Amber : C_Muted;
            using (SolidBrush b2 = new SolidBrush(c2))
                g.DrawString(line2, FTiny, b2, 14f, 208f);
        }

        /// <summary>周视图的小图例：蓝亚 紫欧 青美 绿重叠</summary>
        static void DrawLegend(Graphics g, float x, float y)
        {
            string[] names = { "亚", "欧", "美", "重叠" };
            Color[] cols = { C_Asia, C_Eu, C_Us, C_Green };
            float cx = x;
            using (SolidBrush tb = new SolidBrush(C_Muted))
            {
                for (int i = 0; i < names.Length; i++)
                {
                    using (SolidBrush dot = new SolidBrush(cols[i]))
                        g.FillRectangle(dot, cx, y + 4.5f, 5f, 5f);
                    g.DrawString(names[i], FTiny, tb, cx + 8f, y);
                    SizeF sz = g.MeasureString(names[i], FTiny);
                    cx += 8f + sz.Width + 6f;
                }
            }
        }

        public static GraphicsPath RoundedPath(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = radius * 2f;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            if (d <= 0.1f)
            {
                p.AddRectangle(r);
                p.CloseFigure();
                return p;
            }
            p.AddArc(r.X, r.Y, d, d, 180f, 90f);
            p.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
            p.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
            p.CloseFigure();
            return p;
        }
    }
}
