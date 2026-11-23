using System;
using System.Collections.Generic;
using TradingClock;

// 控制台断言程序：不依赖任何测试框架，csc 直接编译运行
static class EngineTests
{
    static int _pass = 0;
    static int _fail = 0;

    static void Check(string name, bool ok, string detail)
    {
        if (ok) { _pass++; return; }
        _fail++;
        Console.WriteLine("  FAIL  " + name + (detail == null ? "" : "  -> " + detail));
    }

    static void Eq(string name, object expected, object actual)
    {
        bool ok = Equals(expected, actual);
        Check(name, ok, ok ? null : "期望 " + expected + " / 实际 " + actual);
    }

    // 北京时间 -> UTC
    static DateTime Bj(string s)
    {
        DateTime local = DateTime.ParseExact(s, "yyyy-MM-dd HH:mm", null);
        return local.AddHours(-8);
    }

    static Snapshot At(string bjTime) { return TimeEngine.Compute(Bj(bjTime)); }

    [STAThread]
    static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("[1] 规则推导：切换日期逐年不同（证明没有写死）");
        Eq("2024 美入夏令", new DateTime(2024, 3, 10), SessionRules.UsDstStartBj(2024).Date);
        Eq("2025 美入夏令", new DateTime(2025, 3, 9), SessionRules.UsDstStartBj(2025).Date);
        Eq("2026 美入夏令", new DateTime(2026, 3, 8), SessionRules.UsDstStartBj(2026).Date);
        Eq("2026 欧入夏令", new DateTime(2026, 3, 29), SessionRules.EuDstStartBj(2026).Date);
        Eq("2026 欧入冬令", new DateTime(2026, 10, 25), SessionRules.EuDstEndBj(2026).Date);
        Eq("2026 美入冬令", new DateTime(2026, 11, 1), SessionRules.UsDstEndBj(2026).Date);
        Eq("2027 美入夏令", new DateTime(2027, 3, 14), SessionRules.UsDstStartBj(2027).Date);
        Eq("2027 欧入夏令", new DateTime(2027, 3, 28), SessionRules.EuDstStartBj(2027).Date);
        Eq("2027 欧入冬令", new DateTime(2027, 10, 31), SessionRules.EuDstEndBj(2027).Date);
        Eq("2027 美入冬令", new DateTime(2027, 11, 7), SessionRules.UsDstEndBj(2027).Date);
        Eq("2028 美入夏令", new DateTime(2028, 3, 12), SessionRules.UsDstStartBj(2028).Date);
        Eq("2030 欧入夏令", new DateTime(2030, 3, 31), SessionRules.EuDstStartBj(2030).Date);

        Console.WriteLine("[2] 切换时刻换算（北京 09:00 / 15:00 / 14:00）");
        Eq("欧入夏令 09:00", "09:00", SessionRules.EuDstStartBj(2026).ToString("HH:mm"));
        Eq("欧入冬令 09:00", "09:00", SessionRules.EuDstEndBj(2026).ToString("HH:mm"));
        Eq("美入夏令 15:00", "15:00", SessionRules.UsDstStartBj(2026).ToString("HH:mm"));
        Eq("美入冬令 14:00", "14:00", SessionRules.UsDstEndBj(2026).ToString("HH:mm"));

        Console.WriteLine("[3] 错位期长度（春季 14 或 21 天；秋季恒为 7 天）");
        int[] springYears = { 2024, 2025, 2026, 2027, 2028, 2029, 2030 };
        int[] springGap = { 21, 21, 21, 14, 14, 14, 21 };
        for (int i = 0; i < springYears.Length; i++)
        {
            int y = springYears[i];
            int gap = (SessionRules.EuDstStartBj(y).Date - SessionRules.UsDstStartBj(y).Date).Days;
            Eq(y + " 春季错位天数", springGap[i], gap);
        }
        // 欧盟最后一个周日与美国第一个周日总是相隔 7 天
        for (int y = 2024; y <= 2035; y++)
        {
            int gap = (SessionRules.UsDstEndBj(y).Date - SessionRules.EuDstEndBj(y).Date).Days;
            Eq(y + " 秋季错位天数", 7, gap);
        }

        Console.WriteLine("[4] 三配置窗口");
        Check("A 双夏令 重叠 20:00–23:00",
            SessionRules.Windows(SessionMode.BothSummer).UsStart == 1200 &&
            SessionRules.Windows(SessionMode.BothSummer).EuEnd == 1380, null);
        Check("B 双冬令 重叠 21:00–00:00",
            SessionRules.Windows(SessionMode.BothWinter).UsStart == 1260 &&
            SessionRules.Windows(SessionMode.BothWinter).EuEnd == 1440, null);
        Check("C 错位   重叠 20:00–00:00",
            SessionRules.Windows(SessionMode.Mixed).UsStart == 1200 &&
            SessionRules.Windows(SessionMode.Mixed).EuEnd == 1440, null);

        Console.WriteLine("[5] 盘面判定");
        Eq("2026-01-20 12:00 同步冬令", "同步冬令", At("2026-01-20 12:00").ModeName);
        Eq("2026-03-15 21:00 春季错位", "错位期", At("2026-03-15 21:00").ModeName);
        Eq("2026-06-15 21:00 同步夏令", "同步夏令", At("2026-06-15 21:00").ModeName);
        Eq("2026-10-28 22:00 秋季错位", "错位期", At("2026-10-28 22:00").ModeName);
        Eq("2026-12-15 22:00 同步冬令", "同步冬令", At("2026-12-15 22:00").ModeName);

        Console.WriteLine("[6] 关键：2027 也必须判对（写死 2026 日期会在这里失败）");
        Eq("2027-03-10 12:00 仍是同步冬令", "同步冬令", At("2027-03-10 12:00").ModeName);
        Eq("2027-03-14 15:30 已进错位", "错位期", At("2027-03-14 15:30").ModeName);
        Eq("2027-03-20 21:00 错位", "错位期", At("2027-03-20 21:00").ModeName);
        Eq("2027-03-28 09:30 回到同步夏令", "同步夏令", At("2027-03-28 09:30").ModeName);
        Eq("2027-10-31 08:30 仍是同步夏令", "同步夏令", At("2027-10-31 08:30").ModeName);
        Eq("2027-10-31 09:30 进秋季错位", "错位期", At("2027-10-31 09:30").ModeName);
        Eq("2027-11-07 14:30 回到同步冬令", "同步冬令", At("2027-11-07 14:30").ModeName);

        Console.WriteLine("[7] 切换前后一分钟");
        Eq("2026-10-25 08:59 同步夏令", "同步夏令", At("2026-10-25 08:59").ModeName);
        Eq("2026-10-25 09:00 错位", "错位期", At("2026-10-25 09:00").ModeName);
        Eq("2026-11-01 13:59 错位", "错位期", At("2026-11-01 13:59").ModeName);
        Eq("2026-11-01 14:00 同步冬令", "同步冬令", At("2026-11-01 14:00").ModeName);

        Console.WriteLine("[8] 许可等级");
        Eq("2026-06-15 21:00 主攻", Level.Main, At("2026-06-15 21:00").Level);
        Eq("2026-06-15 16:00 试单", Level.Trial, At("2026-06-15 16:00").Level);
        Eq("2026-06-15 10:00 布局", Level.Layout, At("2026-06-15 10:00").Level);
        Eq("2026-06-15 23:30 轻仓(美盘独走)", Level.Light, At("2026-06-15 23:30").Level);
        Eq("2026-10-28 22:00 轻仓(错位重叠)", Level.Light, At("2026-10-28 22:00").Level);
        Eq("2026-10-28 17:00 禁开单", Level.NoTrade, At("2026-10-28 17:00").Level);
        Eq("2026-06-20 12:00 周六休市", Level.Closed, At("2026-06-20 12:00").Level);

        Console.WriteLine("[9] 周末与周一开盘");
        Snapshot sat = At("2026-06-20 12:00");
        Check("周六判定休市", sat.ClosedNow, null);
        Eq("周一开盘 05:00（夏令）", "05:00", sat.NextOpen.ToString("HH:mm"));
        Snapshot satW = At("2026-12-12 12:00");
        Eq("周一开盘 06:00（冬令）", "06:00", satW.NextOpen.ToString("HH:mm"));
        Check("周一 09:00 已开市", !At("2026-06-22 09:00").ClosedNow, null);

        Console.WriteLine("[10] 倒计时与节点");
        Snapshot s1 = At("2026-06-15 22:30");
        Check("有下一个节点", s1.Nodes.Count > 0, null);
        Eq("22:30 下一节点是欧盘收盘 23:00", "欧盘收盘", s1.Nodes[0].Label);
        Check("剩余 30 分钟", Math.Abs((s1.Nodes[0].Time - s1.BeijingNow).TotalMinutes - 30) < 0.01, null);
        Eq("下一个节点之后是美盘收盘", "美盘收盘", s1.Nodes[1].Label);

        Console.WriteLine("[11] 下一次切换");
        Snapshot t1 = At("2026-10-23 12:00");
        Eq("10-23 下一次切换是欧盟入冬令", "欧盟入冬令", t1.NextSwitchLabel);
        Eq("距今 1 天 21 小时", "1天21小时", TimeEngine.Human(t1.NextSwitchIn));
        Snapshot t2 = At("2026-12-01 12:00");
        Eq("12-01 下一次是美国入夏令", "美国入夏令", t2.NextSwitchLabel);
        Eq("跨年也正确", new DateTime(2027, 3, 14), t2.NextSwitchTime.Date);

        Console.WriteLine("[12] 切换日识别");
        Check("10-25 是切换日", At("2026-10-25 12:00").IsSwitchDay, null);
        Check("10-24 不是切换日", !At("2026-10-24 12:00").IsSwitchDay, null);
        Check("切换日早晚都标记", At("2026-10-25 08:00").IsSwitchDay && At("2026-10-25 23:00").IsSwitchDay, null);

        Console.WriteLine("[13] 周末时间轴：白天全空，只剩真实可交易的尾盘");
        Snapshot satNoon = At("2026-06-20 12:00");   // 周六中午
        Check("周六 亚盘空", satNoon.Rows[0].Count == 0, "段数=" + satNoon.Rows[0].Count);
        Check("周六 欧盘空", satNoon.Rows[1].Count == 0, "段数=" + satNoon.Rows[1].Count);
        Check("周六 美盘只剩 00:00–05:00 的周五尾盘", OnlyTail(satNoon.Rows[2], 300), CountStr(satNoon.Rows[2]));
        Check("周六 重叠空", satNoon.Overlap.Count == 0, null);

        Snapshot sunNoon = At("2026-06-21 12:00");   // 周日
        Check("周日 三行全空",
            sunNoon.Rows[0].Count == 0 && sunNoon.Rows[1].Count == 0 && sunNoon.Rows[2].Count == 0,
            CountStr(sunNoon.Rows[0]) + "/" + CountStr(sunNoon.Rows[1]) + "/" + CountStr(sunNoon.Rows[2]));

        Console.WriteLine("[14] 周六凌晨仍算周五尾盘，周一凌晨仍是空的");
        Snapshot satEarly = At("2026-06-20 02:00");  // 周六 02:00 = 纽约周五 13:00
        Check("周六 02:00 仍在交易", !satEarly.ClosedNow, "应为开市");
        Check("周六 02:00 美盘行有段", satEarly.Rows[2].Count > 0, null);
        Snapshot monEarly = At("2026-06-22 02:00");  // 周一 02:00，市场还没开
        Check("周一 02:00 休市", monEarly.ClosedNow, null);
        Check("周一 02:00 美盘行没有凌晨段", !HasEarlyPart(monEarly.Rows[2], 300),
            CountStr(monEarly.Rows[2]));
        Check("周一 02:00 美盘行仍有今晚 20:00 那段", HasLatePart(monEarly.Rows[2], 1200),
            CountStr(monEarly.Rows[2]));

        Console.WriteLine("[15] 节假日：美国");
        HolidayInfo thx = HolidayRules.Find(new DateTime(2026, 11, 26), Market.Us);
        Check("2026-11-26 是感恩节", thx != null && thx.Name == "感恩节", thx == null ? "null" : thx.Name);
        Check("2026-11-25 不是假日", HolidayRules.Find(new DateTime(2026, 11, 25), Market.Us) == null, null);
        Snapshot tg = At("2026-11-26 22:00");        // 感恩节当晚
        Check("感恩节降级为轻仓", tg.Level == Level.Light, "实际=" + tg.Level);
        Check("感恩节标注出现", tg.HolidayTag != null && tg.HolidayTag.Contains("感恩节"), tg.HolidayTag);
        Check("感恩节 美盘晚间段变暗", EveningDim(tg.Rows[2]), CountStr(tg.Rows[2]));
        Check("感恩节 凌晨那段是周三正常行情，不该变暗", !AllDim(tg.Rows[2]), CountStr(tg.Rows[2]));
        Check("感恩节 重叠条也变暗", AllDim(tg.Overlap), CountStr(tg.Overlap));
        Check("感恩节 欧盘行不变暗", !AllDim(tg.Rows[1]), CountStr(tg.Rows[1]));

        Console.WriteLine("[16] 节假日：全休（元旦 / 耶稣受难日）");
        Snapshot nyd = At("2027-01-01 21:00");
        Check("元旦全场休市", nyd.ClosedNow, null);
        Check("元旦 当晚美盘无新段（只剩前一天尾盘）",
            nyd.Rows[2].Count == 1 && nyd.Rows[2][0].End <= 360, CountStr(nyd.Rows[2]));
        Snapshot gf = At("2027-03-26 17:00");        // 2027 复活节 3/28，受难日 3/26
        Check("受难日全场休市", gf.ClosedNow, null);
        Eq("受难日原因", "耶稣受难日", gf.CloseReason);

        Console.WriteLine("[17] 节假日：国内假日只让亚盘变清淡");
        Snapshot cn = At("2027-10-01 10:00");        // 国庆
        Check("国庆 亚盘行有段但全为清淡", cn.Rows[0].Count > 0 && AllDim(cn.Rows[0]), null);
        Check("国庆 欧盘行正常", cn.Rows[1].Count > 0 && !AllDim(cn.Rows[1]), null);
        Check("国庆 不强制休市", !cn.ClosedNow, null);
        Check("国庆 标注出现", cn.HolidayTag != null && cn.HolidayTag.Contains("国庆"), cn.HolidayTag);
        Snapshot cny = At("2027-02-08 10:00");       // 春节
        Check("春节 亚盘清淡", cny.Rows[0].Count > 0 && AllDim(cny.Rows[0]), null);

        Console.WriteLine("[18] 休市日不把该市场的节点算进倒计时");
        Snapshot nx = At("2026-12-25 08:00");        // 圣诞（周五，全休）
        bool hasOverlapNode = false;
        for (int i = 0; i < nx.Nodes.Count; i++)
            if (nx.Nodes[i].Label == "重叠开始" && nx.Nodes[i].Time.Date == new DateTime(2026, 12, 25))
                hasOverlapNode = true;
        Check("圣诞当天不再提示今晚重叠开始", !hasOverlapNode, null);
        bool hasEuOpen = false;
        for (int i = 0; i < nx.Nodes.Count; i++)
            if (nx.Nodes[i].Label == "欧盘开盘" && nx.Nodes[i].Time.Date == new DateTime(2026, 12, 25))
                hasEuOpen = true;
        Check("圣诞当天不再提示欧盘开盘", !hasEuOpen, null);

        Console.WriteLine("[19] 日/周视图与翻页");
        DateTime utc0 = Bj("2026-11-22 21:00");
        Snapshot vDay0 = TimeEngine.Compute(utc0, false, 0, 0);
        Check("今日日视图显示现在标记", vDay0.ShowNow, null);
        Eq("今日视图日期", new DateTime(2026, 11, 22), vDay0.ViewDate);

        Snapshot vDay1 = TimeEngine.Compute(utc0, false, 1, 0);
        Check("明天的日视图不显示现在标记", !vDay1.ShowNow, null);
        Eq("明天 = 11-23", new DateTime(2026, 11, 23), vDay1.ViewDate);
        Check("明天的时间轴照常画", vDay1.Rows[0].Count > 0 && vDay1.Rows[1].Count > 0, null);
        Eq("明天可交易小时数（三轨并集）", 16.0, vDay1.ViewHours);

        Snapshot vDay_1 = TimeEngine.Compute(utc0, false, -1, 0);
        Check("昨天的日视图也不显示现在标记", !vDay_1.ShowNow, null);
        Eq("昨天 = 11-21", new DateTime(2026, 11, 21), vDay_1.ViewDate);

        Snapshot vWeek0 = TimeEngine.Compute(utc0, true, 0, 0);
        Eq("本周从周一开始", new DateTime(2026, 11, 16), vWeek0.ViewDate);
        Check("本周显示现在标记", vWeek0.ShowNow, null);
        Eq("周视图 7 行 24 列", 7, vWeek0.WeekGrid.GetLength(0));
        Eq("周视图列数", 24, vWeek0.WeekGrid.GetLength(1));

        Snapshot vWeek1 = TimeEngine.Compute(utc0, true, 0, 1);
        Eq("下周从 11-23 开始", new DateTime(2026, 11, 23), vWeek1.ViewDate);
        Check("下周不显示现在标记", !vWeek1.ShowNow, null);
        Check("本周有现在标记，下周没有", vWeek0.ShowNow && !vWeek1.ShowNow, null);

        Console.WriteLine("[20] 周视图格子内容");
        // 2026-11-16 是周一，同步冬令：亚 08-17、欧 16-24、重叠 21-24
        Eq("周一 08:00 亚盘", (int)CellState.Asia, vWeek0.WeekGrid[0, 8]);
        Eq("周一 17:00 欧盘仍在", (int)CellState.Eu, vWeek0.WeekGrid[0, 17]);
        Eq("周一 16:00 欧盘", (int)CellState.Eu, vWeek0.WeekGrid[0, 16]);
        Eq("周一 21:00 重叠", (int)CellState.Overlap, vWeek0.WeekGrid[0, 21]);
        Eq("周一 01:00 还没开盘", (int)CellState.None, vWeek0.WeekGrid[0, 1]);
        Eq("周六 12:00 空", (int)CellState.None, vWeek0.WeekGrid[5, 12]);
        Eq("周日 12:00 空", (int)CellState.None, vWeek0.WeekGrid[6, 12]);
        Eq("周六 02:00 仍是周五尾盘", (int)CellState.Us, vWeek0.WeekGrid[5, 2]);

        Console.WriteLine("[21] 翻页后休市判断仍按真实现在算");
        Snapshot vFuture = TimeEngine.Compute(utc0, false, 3, 0);
        Check("明天之后仍按现在判休市", vFuture.ClosedNow == vDay0.ClosedNow, null);

        Console.WriteLine();
        Console.WriteLine("========== " + _pass + " passed / " + _fail + " failed ==========");
        return _fail == 0 ? 0 : 1;
    }

    static string CountStr(System.Collections.Generic.List<Seg> segs)
    {
        string r = "";
        for (int i = 0; i < segs.Count; i++)
            r += "[" + segs[i].Start + "," + segs[i].End + (segs[i].Dim ? ",dim" : "") + "]";
        return r.Length == 0 ? "空" : r;
    }

    static bool AllDim(System.Collections.Generic.List<Seg> segs)
    {
        for (int i = 0; i < segs.Count; i++) if (!segs[i].Dim) return false;
        return segs.Count > 0;
    }

    /// <summary>只有一段，且整段落在 [0, tailEnd) 内</summary>
    static bool OnlyTail(System.Collections.Generic.List<Seg> segs, int tailEnd)
    {
        return segs.Count == 1 && segs[0].End <= tailEnd;
    }

    /// <summary>是否存在覆盖凌晨 [0, edge) 的段</summary>
    static bool HasEarlyPart(System.Collections.Generic.List<Seg> segs, int edge)
    {
        for (int i = 0; i < segs.Count; i++)
            if (segs[i].Start < edge && segs[i].End > 0) return true;
        return false;
    }

    /// <summary>是否存在覆盖傍晚 [start, 1440) 的段</summary>
    static bool HasLatePart(System.Collections.Generic.List<Seg> segs, int start)
    {
        for (int i = 0; i < segs.Count; i++)
            if (segs[i].Start <= start && segs[i].End > start) return true;
        return false;
    }

    /// <summary>晚间（>= 20:00）的段是否都变暗了</summary>
    static bool EveningDim(System.Collections.Generic.List<Seg> segs)
    {
        bool any = false;
        for (int i = 0; i < segs.Count; i++)
        {
            if (segs[i].Start < 1200) continue;
            any = true;
            if (!segs[i].Dim) return false;
        }
        return any;
    }
}
