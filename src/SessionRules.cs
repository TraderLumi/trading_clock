using System;

namespace TradingClock
{
    // 全年只有 3 种盘面配置
    public enum SessionMode
    {
        BothSummer = 0, // A 双夏令
        BothWinter = 1, // B 双冬令
        Mixed = 2       // C 错位（欧冬令 + 美夏令），春秋两段都用它
    }

    public enum SwitchKind { UsStart = 0, UsEnd = 1, EuStart = 2, EuEnd = 3 }

    // 一次切换：北京时间、名称、切换后盘面
    public class SwitchPoint
    {
        public SwitchKind Kind;
        public DateTime Beijing;
        public string Label;
        public string Effect;
    }

    // 对应盘面下三条交易的起止（北京时间，单位：当天 0 点起的分钟数，可 > 1440）
    public struct SessionWindows
    {
        public int EuStart;
        public int EuEnd;
        public int UsStart;
        public int UsEnd;
    }

    /// <summary>
    /// 规则层：只负责“第几个星期几”的推导，不存任何年份的日期表。
    /// 规则来源：
    ///   美国  15 U.S.C. 260a —— 3 月第 2 个周日 02:00 EST 起，11 月第 1 个周日 02:00 EDT 止
    ///   欧盟  2000/84/EC     —— 3 月最后一个周日 01:00 UTC 起，10 月最后一个周日 01:00 UTC 止
    /// 若将来任一方的立法变更，只需改本文件顶部的四个函数。
    /// </summary>
    public static class SessionRules
    {
        // 亚盘全年固定，中国无夏令时
        public const int AsiaStart = 8 * 60;   // 08:00
        public const int AsiaEnd = 17 * 60;    // 17:00

        /// <summary>某年某月的第 n 个周日（n 从 1 开始）</summary>
        public static DateTime NthSunday(int year, int month, int n)
        {
            DateTime first = new DateTime(year, month, 1);
            int off = ((int)DayOfWeek.Sunday - (int)first.DayOfWeek + 7) % 7;
            return first.AddDays(off + 7 * (n - 1));
        }

        /// <summary>某年某月的最后一个周日</summary>
        public static DateTime LastSunday(int year, int month)
        {
            DateTime last = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
            int off = ((int)last.DayOfWeek - (int)DayOfWeek.Sunday + 7) % 7;
            return last.AddDays(-off);
        }

        // ---- 四个切换时刻，全部换算成北京时间 ----
        // 美国 02:00 本地切换：春季 EST(UTC-5) 02:00 = 07:00 UTC = 15:00 北京
        //                      秋季 EDT(UTC-4) 02:00 = 06:00 UTC = 14:00 北京
        // 欧盟 01:00 UTC 切换  = 09:00 北京
        public static DateTime UsDstStartBj(int year) { return NthSunday(year, 3, 2).AddHours(15); }
        public static DateTime UsDstEndBj(int year) { return NthSunday(year, 11, 1).AddHours(14); }
        public static DateTime EuDstStartBj(int year) { return LastSunday(year, 3).AddHours(9); }
        public static DateTime EuDstEndBj(int year) { return LastSunday(year, 10).AddHours(9); }

        /// <summary>某年的四个切换点，按时间升序</summary>
        public static SwitchPoint[] Switches(int year)
        {
            SwitchPoint[] list = new SwitchPoint[4];

            list[0] = new SwitchPoint();
            list[0].Kind = SwitchKind.UsStart;
            list[0].Beijing = UsDstStartBj(year);
            list[0].Label = "美国入夏令";
            list[0].Effect = "美盘 20:00–05:00，进入春季错位期";

            list[1] = new SwitchPoint();
            list[1].Kind = SwitchKind.EuStart;
            list[1].Beijing = EuDstStartBj(year);
            list[1].Label = "欧盟入夏令";
            list[1].Effect = "欧盘 15:00–23:00，两盘同步";

            list[2] = new SwitchPoint();
            list[2].Kind = SwitchKind.EuEnd;
            list[2].Beijing = EuDstEndBj(year);
            list[2].Label = "欧盟入冬令";
            list[2].Effect = "欧盘 16:00–00:00，进入秋季错位期";

            list[3] = new SwitchPoint();
            list[3].Kind = SwitchKind.UsEnd;
            list[3].Beijing = UsDstEndBj(year);
            list[3].Label = "美国入冬令";
            list[3].Effect = "美盘 21:00–06:00，两盘同步";

            Array.Sort(list, delegate(SwitchPoint a, SwitchPoint b) { return a.Beijing.CompareTo(b.Beijing); });
            return list;
        }

        /// <summary>由两个夏令时布尔量得到盘面配置</summary>
        public static SessionMode ModeFor(bool euDst, bool usDst)
        {
            if (euDst && usDst) return SessionMode.BothSummer;
            if (!euDst && !usDst) return SessionMode.BothWinter;
            return SessionMode.Mixed;
        }

        public static SessionWindows Windows(SessionMode mode)
        {
            SessionWindows w = new SessionWindows();
            if (mode == SessionMode.BothSummer)
            {
                w.EuStart = 15 * 60; w.EuEnd = 23 * 60;   // 15:00–23:00
                w.UsStart = 20 * 60; w.UsEnd = 29 * 60;   // 20:00–次日05:00
            }
            else if (mode == SessionMode.BothWinter)
            {
                w.EuStart = 16 * 60; w.EuEnd = 24 * 60;   // 16:00–00:00
                w.UsStart = 21 * 60; w.UsEnd = 30 * 60;   // 21:00–次日06:00
            }
            else
            {
                w.EuStart = 16 * 60; w.EuEnd = 24 * 60;   // 16:00–00:00（欧冬令）
                w.UsStart = 20 * 60; w.UsEnd = 29 * 60;   // 20:00–次日05:00（美夏令）
            }
            return w;
        }

        public static string ModeName(SessionMode m)
        {
            if (m == SessionMode.BothSummer) return "同步夏令";
            if (m == SessionMode.BothWinter) return "同步冬令";
            return "错位期";
        }

        /// <summary>重叠窗口：从美盘开盘到欧盘收盘</summary>
        public static int OverlapStart(SessionWindows w) { return w.UsStart; }
        public static int OverlapEnd(SessionWindows w) { return w.EuEnd; }
    }
}
