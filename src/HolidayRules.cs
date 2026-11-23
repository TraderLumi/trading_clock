using System;
using System.Collections.Generic;
using System.IO;

namespace TradingClock
{
    public enum Market { Asia = 0, Eu = 1, Us = 2 }

    /// <summary>Quiet = 市场仍开但清淡（降级处理）；Closed = 该市场当天真的不开</summary>
    public enum HolidayKind { Quiet = 0, Closed = 1 }

    public class HolidayInfo
    {
        public DateTime Date;
        public Market M;
        public string Name;
        public HolidayKind Kind;
    }

    /// <summary>
    /// 节假日规则。
    /// 美国、欧洲全部按规则算（第几个星期几 / 复活节算法），不存日期表。
    /// 中国农历节日无法用公历推导，用内置表 + 可编辑的 holidays.txt 补。
    /// </summary>
    public static class HolidayRules
    {
        static readonly Dictionary<int, Dictionary<Market, Dictionary<DateTime, HolidayInfo>>> Cache =
            new Dictionary<int, Dictionary<Market, Dictionary<DateTime, HolidayInfo>>>();

        static Dictionary<Market, Dictionary<DateTime, HolidayInfo>> _user;
        static bool _userLoaded;

        // ---------------- 查询 ----------------

        public static HolidayInfo Find(DateTime localDate, Market m)
        {
            DateTime d = localDate.Date;
            Dictionary<Market, Dictionary<DateTime, HolidayInfo>> year = Year(d.Year);
            Dictionary<DateTime, HolidayInfo> map;
            if (year.TryGetValue(m, out map))
            {
                HolidayInfo h;
                if (map.TryGetValue(d, out h)) return h;
            }
            EnsureUser();
            if (_user != null && _user.TryGetValue(m, out map))
            {
                HolidayInfo h;
                if (map.TryGetValue(d, out h)) return h;
            }
            return null;
        }

        public static bool IsClosed(DateTime localDate, Market m)
        {
            HolidayInfo h = Find(localDate, m);
            return h != null && h.Kind == HolidayKind.Closed;
        }

        public static bool IsQuiet(DateTime localDate, Market m)
        {
            HolidayInfo h = Find(localDate, m);
            return h != null && h.Kind == HolidayKind.Quiet;
        }

        // ---------------- 用户自定义 ----------------

        /// <summary>
        /// 读取 exe 同目录的 holidays.txt，一行一条：yyyy-MM-dd, CN|EU|US, 名称[, CLOSED]
        /// 中国节假日每年 11 月国务院发通知后加几行即可，不用重新编译。
        /// </summary>
        public static void LoadUserFile(string path)
        {
            _userLoaded = true;
            _user = null;
            try
            {
                if (!File.Exists(path)) return;
                Dictionary<Market, Dictionary<DateTime, HolidayInfo>> map =
                    new Dictionary<Market, Dictionary<DateTime, HolidayInfo>>();
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                    string[] parts = line.Split(',');
                    if (parts.Length < 3) continue;
                    DateTime d;
                    if (!DateTime.TryParse(parts[0].Trim(), out d)) continue;
                    string mk = parts[1].Trim().ToUpperInvariant();
                    Market m;
                    if (mk == "CN" || mk == "ASIA") m = Market.Asia;
                    else if (mk == "EU") m = Market.Eu;
                    else if (mk == "US") m = Market.Us;
                    else continue;
                    HolidayInfo h = new HolidayInfo();
                    h.Date = d.Date;
                    h.M = m;
                    h.Name = parts[2].Trim();
                    h.Kind = HolidayKind.Quiet;
                    if (parts.Length >= 4 && parts[3].Trim().ToUpperInvariant() == "CLOSED")
                        h.Kind = HolidayKind.Closed;

                    Dictionary<DateTime, HolidayInfo> inner;
                    if (!map.TryGetValue(m, out inner))
                    {
                        inner = new Dictionary<DateTime, HolidayInfo>();
                        map[m] = inner;
                    }
                    inner[h.Date] = h;
                }
                _user = map;
            }
            catch { }
        }

        static void EnsureUser()
        {
            if (_userLoaded) return;
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            LoadUserFile(Path.Combine(dir, "holidays.txt"));
        }

        // ---------------- 按年生成 ----------------

        static Dictionary<Market, Dictionary<DateTime, HolidayInfo>> Build(int year)
        {
            Dictionary<Market, Dictionary<DateTime, HolidayInfo>> m =
                new Dictionary<Market, Dictionary<DateTime, HolidayInfo>>();
            m[Market.Us] = new Dictionary<DateTime, HolidayInfo>();
            m[Market.Eu] = new Dictionary<DateTime, HolidayInfo>();
            m[Market.Asia] = new Dictionary<DateTime, HolidayInfo>();

            DateTime easter = Easter(year);

            // --- 美国：联邦假日（券商/银行休假，外汇变清淡） ---
            Add(m, Market.Us, Observed(new DateTime(year, 1, 1)), "元旦", HolidayKind.Closed);
            Add(m, Market.Us, NthWeekday(year, 1, DayOfWeek.Monday, 3), "马丁路德金日", HolidayKind.Quiet);
            Add(m, Market.Us, NthWeekday(year, 2, DayOfWeek.Monday, 3), "总统日", HolidayKind.Quiet);
            Add(m, Market.Us, easter.AddDays(-2), "耶稣受难日", HolidayKind.Closed);
            Add(m, Market.Us, LastWeekday(year, 5, DayOfWeek.Monday), "阵亡将士纪念日", HolidayKind.Quiet);
            Add(m, Market.Us, Observed(new DateTime(year, 6, 19)), "六月节", HolidayKind.Quiet);
            Add(m, Market.Us, Observed(new DateTime(year, 7, 4)), "独立日", HolidayKind.Quiet);
            Add(m, Market.Us, NthWeekday(year, 9, DayOfWeek.Monday, 1), "劳动节", HolidayKind.Quiet);
            Add(m, Market.Us, NthWeekday(year, 11, DayOfWeek.Thursday, 4), "感恩节", HolidayKind.Quiet);
            Add(m, Market.Us, Observed(new DateTime(year, 12, 25)), "圣诞节", HolidayKind.Closed);

            // --- 欧洲（伦敦）银行假日 ---
            Add(m, Market.Eu, Observed(new DateTime(year, 1, 1)), "元旦", HolidayKind.Closed);
            Add(m, Market.Eu, easter.AddDays(-2), "耶稣受难日", HolidayKind.Closed);
            Add(m, Market.Eu, easter.AddDays(1), "复活节星期一", HolidayKind.Quiet);
            Add(m, Market.Eu, NthWeekday(year, 5, DayOfWeek.Monday, 1), "五月初银行假日", HolidayKind.Quiet);
            Add(m, Market.Eu, LastWeekday(year, 5, DayOfWeek.Monday), "春季银行假日", HolidayKind.Quiet);
            Add(m, Market.Eu, LastWeekday(year, 8, DayOfWeek.Monday), "夏季银行假日", HolidayKind.Quiet);
            Add(m, Market.Eu, Observed(new DateTime(year, 12, 25)), "圣诞节", HolidayKind.Closed);
            Add(m, Market.Eu, Observed(new DateTime(year, 12, 26)), "节礼日", HolidayKind.Closed);

            // --- 中国：内置表（2027 为国务院 2026-11-05 通知原文，2028 待官方通知） ---
            AddCn(m, CnTable, year);

            return m;
        }

        static void Add(Dictionary<Market, Dictionary<DateTime, HolidayInfo>> m,
                        Market mk, DateTime d, string name, HolidayKind kind)
        {
            HolidayInfo h = new HolidayInfo();
            h.Date = d.Date;
            h.M = mk;
            h.Name = name;
            h.Kind = kind;
            m[mk][h.Date] = h;
        }

        static void AddCn(Dictionary<Market, Dictionary<DateTime, HolidayInfo>> m, string[] table, int year)
        {
            for (int i = 0; i < table.Length; i++)
            {
                string[] parts = table[i].Split('|');
                if (parts.Length < 2) continue;
                DateTime d;
                if (!DateTime.TryParse(parts[0], out d)) continue;
                if (d.Year != year) continue;
                HolidayInfo h = new HolidayInfo();
                h.Date = d.Date;
                h.M = Market.Asia;
                h.Name = parts[1];
                h.Kind = HolidayKind.Quiet;   // 亚盘是东京/香港，中国放假只是清淡，不是停市
                m[Market.Asia][h.Date] = h;
            }
        }

        // 2027：国办发明电〔2026〕10 号
        // 2028：仅固定日期的节日，农历节日待国务院通知后补到 holidays.txt
        static readonly string[] CnTable = new string[]
        {
            "2027-01-01|元旦", "2027-01-02|元旦", "2027-01-03|元旦",
            "2027-02-05|春节", "2027-02-06|春节", "2027-02-07|春节", "2027-02-08|春节",
            "2027-02-09|春节", "2027-02-10|春节", "2027-02-11|春节", "2027-02-12|春节", "2027-02-13|春节",
            "2027-04-03|清明节", "2027-04-04|清明节", "2027-04-05|清明节",
            "2027-05-01|劳动节", "2027-05-02|劳动节", "2027-05-03|劳动节", "2027-05-04|劳动节", "2027-05-05|劳动节",
            "2027-06-09|端午节",
            "2027-09-15|中秋节",
            "2027-10-01|国庆节", "2027-10-02|国庆节", "2027-10-03|国庆节", "2027-10-04|国庆节",
            "2027-10-05|国庆节", "2027-10-06|国庆节", "2027-10-07|国庆节",

            "2028-01-01|元旦",
            "2028-04-04|清明节",
            "2028-05-01|劳动节",
            "2028-10-01|国庆节", "2028-10-02|国庆节", "2028-10-03|国庆节", "2028-10-04|国庆节",
            "2028-10-05|国庆节", "2028-10-06|国庆节", "2028-10-07|国庆节"
        };

        // ---------------- 日期工具 ----------------

        public static DateTime Easter(int year)
        {
            int a = year % 19, b = year / 100, c = year % 100;
            int d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
            int h = (19 * a + b - d - g + 15) % 30;
            int i = c / 4, k = c % 4;
            int l = (32 + 2 * e + 2 * i - h - k) % 7;
            int mm = (a + 11 * h + 22 * l) / 451;
            int month = (h + l - 7 * mm + 114) / 31;
            int day = ((h + l - 7 * mm + 114) % 31) + 1;
            return new DateTime(year, month, day);
        }

        static DateTime NthWeekday(int year, int month, DayOfWeek dow, int n)
        {
            DateTime first = new DateTime(year, month, 1);
            int off = ((int)dow - (int)first.DayOfWeek + 7) % 7;
            return first.AddDays(off + 7 * (n - 1));
        }

        static DateTime LastWeekday(int year, int month, DayOfWeek dow)
        {
            DateTime last = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
            int off = ((int)last.DayOfWeek - (int)dow + 7) % 7;
            return last.AddDays(-off);
        }

        /// <summary>落在周六的假日提前到周五，周日的推到周一</summary>
        static DateTime Observed(DateTime d)
        {
            if (d.DayOfWeek == DayOfWeek.Saturday) return d.AddDays(-1);
            if (d.DayOfWeek == DayOfWeek.Sunday) return d.AddDays(1);
            return d;
        }

        static Dictionary<Market, Dictionary<DateTime, HolidayInfo>> Year(int year)
        {
            Dictionary<Market, Dictionary<DateTime, HolidayInfo>> m;
            if (Cache.TryGetValue(year, out m)) return m;
            m = Build(year);
            Cache[year] = m;
            return m;
        }
    }
}
