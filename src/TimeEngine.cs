using System;
using System.Collections.Generic;

namespace TradingClock
{
    // 交易许可五档
    public enum Level
    {
        Main = 0,    // 主攻
        Trial = 1,   // 试单
        Layout = 2,  // 布局
        Light = 3,   // 轻仓
        NoTrade = 4, // 禁止开单
        Closed = 5   // 休市
    }

    // 周视图格子状态
    public enum CellState
    {
        None = 0,    // 不能交易
        Asia = 1,
        Eu = 2,
        Us = 3,
        Overlap = 4  // 欧美重叠
    }

    public class NodeInfo
    {
        public string Label;
        public DateTime Time;
    }

    /// <summary>时间轴上的一段，相对当日 00:00 的分钟数，可能为负（跨零点）</summary>
    public class Seg
    {
        public int Start;
        public int End;
        public bool Dim;   // 节假日清淡
    }

    class DayRows
    {
        public List<Seg>[] Rows;
        public List<Seg> Overlap;
    }

    public class Snapshot
    {
        public DateTime BeijingNow;
        public SessionMode Mode;
        public string ModeName;
        public string PeriodName;
        public bool IsSwitchDay;
        public string SwitchNote;

        public bool ClosedNow;
        public string CloseReason;
        public DateTime NextOpen;

        public SessionWindows Win;
        public string ActiveWindow;
        public Level Level;
        public string LevelTitle;
        public string LevelDetail;
        public string HolidayTag;

        public List<NodeInfo> Nodes;
        public string NextSwitchLabel;
        public DateTime NextSwitchTime;
        public TimeSpan NextSwitchIn;

        // 视图
        public bool WeekView;
        public DateTime ViewDate;        // 日视图=那一天，周视图=那一周的周一
        public bool ViewIsToday;         // 视图范围是否包含“现在”
        public bool ShowNow;             // 是否画现在标记
        public bool Preview;             // 是否在看非今日（此时不显示实时状态）
        public double ViewHours;         // 该日/该周可交易小时数

        public List<Seg>[] Rows;         // 仅日视图
        public List<Seg> Overlap;        // 仅日视图
        public int[,] WeekGrid;          // [7,24]，仅周视图
    }

    /// <summary>
    /// 判定层：只吃 UTC，吐状态。不碰 UI，方便控制台测试。
    /// 核心原则：时间轴画的是「这一天真实能交易的时间」，不是每天重复的固定表。
    /// </summary>
    public static class TimeEngine
    {
        static readonly TimeZoneInfo Cn;
        static readonly TimeZoneInfo Lon;
        static readonly TimeZoneInfo Ny;

        static readonly Dictionary<string, DayRows> DayCache = new Dictionary<string, DayRows>();
        static readonly Dictionary<string, int[,]> WeekCache = new Dictionary<string, int[,]>();

        static TimeEngine()
        {
            Cn = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
            Lon = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
            Ny = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }

        // ---------------- 市场是否开市 ----------------

        public static bool IsWeekend(DateTime utc)
        {
            DateTime ny = TimeZoneInfo.ConvertTime(utc, Ny);
            return (ny.DayOfWeek == DayOfWeek.Friday && ny.Hour >= 17)
                || ny.DayOfWeek == DayOfWeek.Saturday
                || (ny.DayOfWeek == DayOfWeek.Sunday && ny.Hour < 17);
        }

        public static bool UsOpen(DateTime utc)
        {
            if (IsWeekend(utc)) return false;
            DateTime ny = TimeZoneInfo.ConvertTime(utc, Ny);
            return !HolidayRules.IsClosed(ny.Date, Market.Us);
        }

        public static bool EuOpen(DateTime utc)
        {
            if (IsWeekend(utc)) return false;
            DateTime lon = TimeZoneInfo.ConvertTime(utc, Lon);
            return !HolidayRules.IsClosed(lon.Date, Market.Eu);
        }

        public static bool AsiaOpen(DateTime utc)
        {
            return !IsWeekend(utc);
        }

        static bool UsQuiet(DateTime utc)
        {
            if (IsWeekend(utc)) return false;
            DateTime ny = TimeZoneInfo.ConvertTime(utc, Ny);
            return HolidayRules.IsQuiet(ny.Date, Market.Us);
        }

        static bool EuQuiet(DateTime utc)
        {
            if (IsWeekend(utc)) return false;
            DateTime lon = TimeZoneInfo.ConvertTime(utc, Lon);
            return HolidayRules.IsQuiet(lon.Date, Market.Eu);
        }

        static bool AsiaQuiet(DateTime utc)
        {
            if (IsWeekend(utc)) return false;
            DateTime bj = TimeZoneInfo.ConvertTime(utc, Cn);
            return HolidayRules.IsQuiet(bj.Date, Market.Asia);
        }

        /// <summary>把“北京某天某时”换算成 UTC</summary>
        static DateTime BjToUtc(DateTime bjLocal)
        {
            return DateTime.SpecifyKind(bjLocal.AddHours(-8), DateTimeKind.Utc);
        }

        // ---------------- 主入口 ----------------

        public static Snapshot Compute(DateTime utc)
        {
            return Compute(utc, false, 0, 0);
        }

        /// <param name="weekView">true=周视图</param>
        /// <param name="dayShift">日视图偏移天数（+1 明天）</param>
        /// <param name="weekShift">周视图偏移周数（+1 下周）</param>
        public static Snapshot Compute(DateTime utc, bool weekView, int dayShift, int weekShift)
        {
            if (utc.Kind != DateTimeKind.Utc) utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

            Snapshot s = new Snapshot();
            s.BeijingNow = TimeZoneInfo.ConvertTime(utc, Cn);

            bool euDst = Lon.IsDaylightSavingTime(utc);
            bool usDst = Ny.IsDaylightSavingTime(utc);
            s.Mode = SessionRules.ModeFor(euDst, usDst);
            s.ModeName = SessionRules.ModeName(s.Mode);
            s.Win = SessionRules.Windows(s.Mode);
            s.PeriodName = PeriodNameFor(s.BeijingNow);
            FillSwitchInfo(s);

            bool usO = UsOpen(utc);
            bool euO = EuOpen(utc);
            bool asiaO = AsiaOpen(utc);
            bool weekend = IsWeekend(utc);

            s.ClosedNow = !usO && !euO;
            s.HolidayTag = BuildHolidayTag(utc, weekend);
            if (s.ClosedNow)
            {
                s.CloseReason = weekend ? "周末休市" : BuildCloseReason(utc);
                s.NextOpen = FindNextOpen(utc);
            }

            int minutes = s.BeijingNow.Hour * 60 + s.BeijingNow.Minute;
            bool inEuWin = InSpan(minutes, s.Win.EuStart, s.Win.EuEnd);
            bool inUsWin = InSpan(minutes, s.Win.UsStart, s.Win.UsEnd);
            bool inOverlapWin = InSpan(minutes, s.Win.UsStart, s.Win.EuEnd);
            bool inAsiaWin = InSpan(minutes, SessionRules.AsiaStart, SessionRules.AsiaEnd);

            bool inEu = euO && inEuWin;
            bool inUs = usO && inUsWin;
            bool inOverlap = inEu && inUs && inOverlapWin;

            if (s.ClosedNow) s.ActiveWindow = "休市";
            else if (inOverlap) s.ActiveWindow = "欧美重叠";
            else if (inEu) s.ActiveWindow = "欧盘";
            else if (inUs) s.ActiveWindow = "美盘";
            else if (asiaO && inAsiaWin) s.ActiveWindow = "亚盘";
            else s.ActiveWindow = "空窗";

            ResolveLevel(s, utc, inEu, inUs, inOverlap, asiaO && inAsiaWin);

            // ---- 视图 ----
            DateTime today = s.BeijingNow.Date;
            s.WeekView = weekView;

            if (weekView)
            {
                DateTime monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
                s.ViewDate = monday.AddDays(7 * weekShift);
                s.ViewIsToday = today >= s.ViewDate && today < s.ViewDate.AddDays(7);
                s.WeekGrid = BuildWeek(s.ViewDate, s.Win);
                s.ViewHours = GridHours(s.WeekGrid);
            }
            else
            {
                s.ViewDate = today.AddDays(dayShift);
                s.ViewIsToday = s.ViewDate == today;
                DayRows dr = BuildDayRows(s.ViewDate, s.Win);
                s.Rows = dr.Rows;
                s.Overlap = dr.Overlap;
                s.ViewHours = SegHours(dr.Rows);
            }
            s.ShowNow = s.ViewIsToday;
            s.Preview = !s.ViewIsToday;

            s.Nodes = BuildNodes(s.BeijingNow, s.Win);
            return s;
        }

        static bool InSpan(int m, int start, int end)
        {
            if (end <= 1440) return m >= start && m < end;
            return m >= start || m < (end - 1440);
        }

        // ---------------- 一天的分段 ----------------

        static DayRows BuildDayRows(DateTime dayStart, SessionWindows w)
        {
            string key = dayStart.Ticks.ToString() + "|" + ((int)ModeOf(w)).ToString();
            DayRows cached;
            if (DayCache.TryGetValue(key, out cached)) return cached;

            DayRows dr = new DayRows();
            List<Seg>[] rows = new List<Seg>[3];
            rows[0] = BuildSegs(dayStart, SessionRules.AsiaStart, SessionRules.AsiaEnd, AsiaOpen, AsiaQuiet);
            rows[1] = BuildSegs(dayStart, w.EuStart, w.EuEnd, EuOpen, EuQuiet);

            // 美盘跨零点：今天这一段 + 昨天那一段的尾巴
            List<Seg> usRow = new List<Seg>();
            usRow.AddRange(BuildSegs(dayStart, w.UsStart - 1440, w.UsEnd - 1440, UsOpen, UsQuiet));
            usRow.AddRange(BuildSegs(dayStart, w.UsStart, w.UsEnd, UsOpen, UsQuiet));
            rows[2] = usRow;

            dr.Rows = rows;
            dr.Overlap = BuildSegs(dayStart, w.UsStart, w.EuEnd,
                delegate(DateTime t) { return EuOpen(t) && UsOpen(t); },
                delegate(DateTime t) { return EuQuiet(t) || UsQuiet(t); });

            if (DayCache.Count > 24) DayCache.Clear();
            DayCache[key] = dr;
            return dr;
        }

        static SessionMode ModeOf(SessionWindows w)
        {
            if (w.EuStart == 900) return SessionMode.BothSummer;
            if (w.UsStart == 1260) return SessionMode.BothWinter;
            return SessionMode.Mixed;
        }

        static List<Seg> BuildSegs(DateTime dayStart, int from, int to,
                                   Func<DateTime, bool> open, Func<DateTime, bool> dim)
        {
            List<Seg> res = new List<Seg>();
            bool inRun = false;
            int runStart = 0;
            bool runDim = false;

            for (int m = from; m < to; m++)
            {
                DateTime t = dayStart.AddMinutes(m);
                bool o = open(t);
                bool d = o && dim != null && dim(t);

                if (o)
                {
                    if (!inRun) { inRun = true; runStart = m; runDim = d; }
                    else if (d != runDim) { res.Add(Mk(runStart, m, runDim)); runStart = m; runDim = d; }
                }
                else if (inRun)
                {
                    res.Add(Mk(runStart, m, runDim));
                    inRun = false;
                }
            }
            if (inRun) res.Add(Mk(runStart, to, runDim));
            return res;
        }

        static Seg Mk(int a, int b, bool dim)
        {
            Seg s = new Seg();
            s.Start = a;
            s.End = b;
            s.Dim = dim;
            return s;
        }

        /// <summary>当天有行情的总时长（三条轨道取并集，避免亚欧重叠那一小时被算两次）</summary>
        static double SegHours(List<Seg>[] rows)
        {
            bool[] mark = new bool[1440];
            for (int i = 0; i < rows.Length; i++)
            {
                for (int j = 0; j < rows[i].Count; j++)
                {
                    int a = Math.Max(rows[i][j].Start, 0);
                    int b = Math.Min(rows[i][j].End, 1440);
                    for (int m = a; m < b; m++) mark[m] = true;
                }
            }
            int n = 0;
            for (int i = 0; i < 1440; i++) if (mark[i]) n++;
            return n / 60.0;
        }

        // ---------------- 一周的格子 ----------------

        static int[,] BuildWeek(DateTime weekStart, SessionWindows w)
        {
            string key = weekStart.Ticks.ToString() + "|" + ((int)ModeOf(w)).ToString();
            int[,] cached;
            if (WeekCache.TryGetValue(key, out cached)) return cached;

            int[,] grid = new int[7, 24];
            for (int d = 0; d < 7; d++)
            {
                DateTime day = weekStart.AddDays(d);
                for (int h = 0; h < 24; h++)
                {
                    grid[d, h] = (int)CellAt(day.AddHours(h), w);
                }
            }

            if (WeekCache.Count > 8) WeekCache.Clear();
            WeekCache[key] = grid;
            return grid;
        }

        static CellState CellAt(DateTime bjLocal, SessionWindows w)
        {
            DateTime utc = BjToUtc(bjLocal);
            bool usO = UsOpen(utc);
            bool euO = EuOpen(utc);
            bool asiaO = AsiaOpen(utc);

            int m = bjLocal.Hour * 60 + bjLocal.Minute;
            bool inEu = euO && InSpan(m, w.EuStart, w.EuEnd);
            bool inUs = usO && InSpan(m, w.UsStart, w.UsEnd);
            bool inAsia = asiaO && InSpan(m, SessionRules.AsiaStart, SessionRules.AsiaEnd);

            if (inEu && inUs && InSpan(m, w.UsStart, w.EuEnd)) return CellState.Overlap;
            if (inEu) return CellState.Eu;
            if (inUs) return CellState.Us;
            if (inAsia) return CellState.Asia;
            return CellState.None;
        }

        static double GridHours(int[,] grid)
        {
            int n = 0;
            for (int d = 0; d < 7; d++)
                for (int h = 0; h < 24; h++)
                    if (grid[d, h] != 0) n++;
            return n;
        }

        // ---------------- 假期标注 ----------------

        static string BuildHolidayTag(DateTime utc, bool weekend)
        {
            if (weekend) return "";
            List<string> names = new List<string>();
            DateTime bj = TimeZoneInfo.ConvertTime(utc, Cn);
            DateTime ny = TimeZoneInfo.ConvertTime(utc, Ny);
            DateTime lon = TimeZoneInfo.ConvertTime(utc, Lon);

            HolidayInfo a = HolidayRules.Find(bj.Date, Market.Asia);
            if (a != null && !names.Contains(a.Name)) names.Add(a.Name);
            HolidayInfo e = HolidayRules.Find(lon.Date, Market.Eu);
            if (e != null && !names.Contains(e.Name)) names.Add(e.Name);
            HolidayInfo u = HolidayRules.Find(ny.Date, Market.Us);
            if (u != null && !names.Contains(u.Name)) names.Add(u.Name);

            string r = "";
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) r += "、";
                r += names[i];
            }
            return r;
        }

        static string BuildCloseReason(DateTime utc)
        {
            DateTime ny = TimeZoneInfo.ConvertTime(utc, Ny);
            HolidayInfo u = HolidayRules.Find(ny.Date, Market.Us);
            DateTime lon = TimeZoneInfo.ConvertTime(utc, Lon);
            HolidayInfo e = HolidayRules.Find(lon.Date, Market.Eu);
            if (u != null && e != null && u.Name == e.Name) return u.Name;
            if (u != null) return u.Name;
            if (e != null) return e.Name;
            return "休市";
        }

        static DateTime FindNextOpen(DateTime utc)
        {
            DateTime t = utc.AddMinutes(1);
            for (int i = 0; i < 60 * 24 * 5; i++)
            {
                if (UsOpen(t) || EuOpen(t)) return TimeZoneInfo.ConvertTime(t, Cn);
                t = t.AddMinutes(1);
            }
            return TimeZoneInfo.ConvertTime(t, Cn);
        }

        // ---------------- 许可等级 ----------------

        static void ResolveLevel(Snapshot s, DateTime utc, bool inEu, bool inUs, bool inOverlap, bool inAsia)
        {
            if (s.ClosedNow)
            {
                s.Level = Level.Closed;
                s.LevelTitle = "休市";
                s.LevelDetail = s.CloseReason;
                return;
            }
            if (s.Mode == SessionMode.Mixed)
            {
                if (inOverlap)
                {
                    s.Level = Level.Light;
                    s.LevelTitle = "轻仓/观望";
                    s.LevelDetail = "错位期重叠 · 假突破多";
                }
                else
                {
                    s.Level = Level.NoTrade;
                    s.LevelTitle = "禁止开单";
                    s.LevelDetail = "错位期 · 流动性断层";
                }
                ApplyQuiet(s, utc, inEu, inUs, inAsia);
                return;
            }

            if (inOverlap)
            {
                s.Level = Level.Main;
                s.LevelTitle = "主攻窗口";
                s.LevelDetail = "重叠期 · 机构订单集中";
            }
            else if (inEu)
            {
                s.Level = Level.Trial;
                s.LevelTitle = "试单窗口";
                s.LevelDetail = "欧盘独走 · 吃亚盘流动性";
            }
            else if (inAsia)
            {
                s.Level = Level.Layout;
                s.LevelTitle = "布局窗口";
                s.LevelDetail = "亚盘 · 画结构等埋伏";
            }
            else if (inUs)
            {
                s.Level = Level.Light;
                s.LevelTitle = "轻仓";
                s.LevelDetail = "美盘独走 · 尾盘不新开";
            }
            else
            {
                s.Level = Level.Light;
                s.LevelTitle = "空窗";
                s.LevelDetail = "等下一个开盘";
            }
            ApplyQuiet(s, utc, inEu, inUs, inAsia);
        }

        static void ApplyQuiet(Snapshot s, DateTime utc, bool inEu, bool inUs, bool inAsia)
        {
            if (s.Level != Level.Main && s.Level != Level.Trial && s.Level != Level.Layout) return;

            string name = null;
            if (inEu && EuQuiet(utc)) name = QuietName(utc, Market.Eu);
            else if (inUs && UsQuiet(utc)) name = QuietName(utc, Market.Us);
            else if (inAsia && AsiaQuiet(utc)) name = QuietName(utc, Market.Asia);
            if (name == null) return;

            s.Level = Level.Light;
            s.LevelTitle = "轻仓";
            s.LevelDetail = name + " · 流动性差";
        }

        static string QuietName(DateTime utc, Market m)
        {
            if (m == Market.Asia)
            {
                DateTime bj = TimeZoneInfo.ConvertTime(utc, Cn);
                HolidayInfo h = HolidayRules.Find(bj.Date, Market.Asia);
                return h != null ? "国内假期" : null;
            }
            if (m == Market.Eu)
            {
                DateTime lon = TimeZoneInfo.ConvertTime(utc, Lon);
                HolidayInfo h = HolidayRules.Find(lon.Date, Market.Eu);
                return h != null ? h.Name : null;
            }
            DateTime ny = TimeZoneInfo.ConvertTime(utc, Ny);
            HolidayInfo u = HolidayRules.Find(ny.Date, Market.Us);
            return u != null ? u.Name : null;
        }

        // ---------------- 切换信息 ----------------

        static string PeriodNameFor(DateTime bj)
        {
            int y = bj.Year;
            DateTime usS = SessionRules.UsDstStartBj(y);
            DateTime euS = SessionRules.EuDstStartBj(y);
            DateTime euE = SessionRules.EuDstEndBj(y);
            DateTime usE = SessionRules.UsDstEndBj(y);

            if (bj < usS) return "同步冬令期";
            if (bj < euS) return "春季错位期";
            if (bj < euE) return "同步夏令期";
            if (bj < usE) return "秋季错位期";
            return "同步冬令期";
        }

        static void FillSwitchInfo(Snapshot s)
        {
            DateTime bj = s.BeijingNow;
            for (int y = bj.Year - 1; y <= bj.Year + 1; y++)
            {
                SwitchPoint[] pts = SessionRules.Switches(y);
                for (int i = 0; i < pts.Length; i++)
                {
                    if (pts[i].Beijing.Date == bj.Date)
                    {
                        s.IsSwitchDay = true;
                        s.SwitchNote = "今日 " + pts[i].Beijing.ToString("HH:mm") + " " + pts[i].Label;
                    }
                }
            }

            SwitchPoint best = null;
            for (int y = bj.Year - 1; y <= bj.Year + 2; y++)
            {
                SwitchPoint[] pts = SessionRules.Switches(y);
                for (int i = 0; i < pts.Length; i++)
                {
                    if (pts[i].Beijing > bj && (best == null || pts[i].Beijing < best.Beijing)) best = pts[i];
                }
            }
            if (best != null)
            {
                s.NextSwitchLabel = best.Label;
                s.NextSwitchTime = best.Beijing;
                s.NextSwitchIn = best.Beijing - bj;
            }
        }

        // ---------------- 节点 ----------------

        static List<NodeInfo> BuildNodes(DateTime now, SessionWindows w)
        {
            List<NodeInfo> raw = new List<NodeInfo>();
            for (int d = -1; d <= 2; d++)
            {
                DateTime day = now.Date.AddDays(d);
                raw.Add(Pick(day.AddMinutes(SessionRules.AsiaStart), "亚盘开盘"));
                raw.Add(Pick(day.AddMinutes(w.EuStart), "欧盘开盘"));
                raw.Add(Pick(day.AddMinutes(w.UsStart), "重叠开始"));
                raw.Add(Pick(day.AddMinutes(w.EuEnd), "欧盘收盘"));
                raw.Add(Pick(day.AddMinutes(w.UsEnd), "美盘收盘"));
            }

            List<NodeInfo> list = new List<NodeInfo>();
            for (int i = 0; i < raw.Count; i++)
            {
                NodeInfo n = raw[i];
                if (n.Time <= now) continue;
                DateTime utc = BjToUtc(n.Time);
                bool ok;
                if (n.Label == "亚盘开盘") ok = AsiaOpen(utc);
                else if (n.Label == "欧盘开盘" || n.Label == "欧盘收盘") ok = EuOpen(utc);
                else if (n.Label == "美盘收盘") ok = UsOpen(utc);
                else ok = EuOpen(utc) && UsOpen(utc);
                if (ok) list.Add(n);
            }
            list.Sort(delegate(NodeInfo a, NodeInfo b) { return a.Time.CompareTo(b.Time); });
            return list;
        }

        static NodeInfo Pick(DateTime t, string label)
        {
            NodeInfo n = new NodeInfo();
            n.Time = t;
            n.Label = label;
            return n;
        }

        // ---------------- 文案 ----------------

        public static string Human(TimeSpan t)
        {
            if (t.TotalSeconds < 0) t = TimeSpan.Zero;
            if (t.TotalDays >= 1)
            {
                if (t.Hours == 0) return ((int)t.TotalDays) + "天";
                return ((int)t.TotalDays) + "天" + t.Hours + "小时";
            }
            if (t.TotalHours >= 1)
            {
                if (t.Minutes == 0) return t.Hours + "小时";
                return t.Hours + "小时" + t.Minutes + "分";
            }
            if (t.TotalMinutes >= 1)
            {
                if (t.Seconds == 0) return t.Minutes + "分";
                return t.Minutes + "分" + t.Seconds + "秒";
            }
            return t.Seconds + "秒";
        }

        public static string Hours(double h)
        {
            if (Math.Abs(h - Math.Round(h)) < 0.05) return ((int)Math.Round(h)).ToString();
            return h.ToString("0.0");
        }

        public static string WeekdayCn(DateTime d)
        {
            string[] names = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
            return names[(int)d.DayOfWeek];
        }
    }
}
