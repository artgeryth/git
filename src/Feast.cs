// 节日彩蛋：到了特定日子，她开场白之后会补一句特别的。
//
//   · 公历节日（元旦 / 情人节 / 儿童节 / 国庆 / 万圣夜 / 圣诞 / 跨年夜）：
//     日期是固定的，一定准
//   · 农历节日（春节 / 中秋）：没法从公历推算，用下面这张对照表
//     ⚠ 表只列到 2030 年；到期后在数组里补新行即可（一行一年）
//   · 生日：由用户自己告诉她 —— 聊天里说「我的生日是 3 月 15 日」
using System;
using System.Text.RegularExpressions;

static class Feast
{
    /// <summary>春节对照表 { 年, 月, 日 }（农历，公历日期）</summary>
    static readonly int[][] SpringFestival = new int[][]
    {
        new int[] { 2025, 1, 29 }, new int[] { 2026, 2, 17 }, new int[] { 2027, 2, 6 },
        new int[] { 2028, 1, 26 }, new int[] { 2029, 2, 13 }, new int[] { 2030, 2, 3 }
    };

    /// <summary>中秋对照表 { 年, 月, 日 }（农历，公历日期）</summary>
    static readonly int[][] MidAutumn = new int[][]
    {
        new int[] { 2025, 10, 6 }, new int[] { 2026, 9, 25 }, new int[] { 2027, 9, 15 },
        new int[] { 2028, 10, 3 }, new int[] { 2029, 9, 22 }, new int[] { 2030, 9, 12 }
    };

    /// <summary>今天是什么日子？没有就返回 null。返回的分类名直接对应台词库</summary>
    public static string Key(App app)
    {
        return KeyFor(app, DateTime.Now);
    }

    /// <summary>自检用：可以指定日期来验证</summary>
    public static string KeyFor(App app, DateTime d)
    {
        if (IsBirthday(app, d)) return "feast_birthday";
        if (InTable(SpringFestival, d)) return "feast_spring";
        if (InTable(MidAutumn, d)) return "feast_midautumn";
        if (d.Month == 1 && d.Day == 1) return "feast_newyear";
        if (d.Month == 12 && d.Day == 31) return "feast_newyeareve";
        if (d.Month == 2 && d.Day == 14) return "feast_valentine";
        if (d.Month == 6 && d.Day == 1) return "feast_children";
        if (d.Month == 10 && d.Day == 1) return "feast_national";
        if (d.Month == 10 && d.Day == 31) return "feast_halloween";
        if (d.Month == 12 && d.Day == 25) return "feast_christmas";
        return null;
    }

    static bool InTable(int[][] table, DateTime d)
    {
        foreach (int[] row in table)
            if (row[0] == d.Year && row[1] == d.Month && row[2] == d.Day) return true;
        return false;
    }

    /* ---------------- 生日 ---------------- */

    static bool IsBirthday(App app, DateTime d)
    {
        string b = app.store.GetString("birthday", "");
        if (string.IsNullOrEmpty(b) || b.Length < 5) return false;   // 存的是 "MM-dd"
        int m, day;
        if (!int.TryParse(b.Substring(0, 2), out m)) return false;
        if (!int.TryParse(b.Substring(3, 2), out day)) return false;
        return d.Month == m && d.Day == day;
    }

    public static string BirthdayText(App app)
    {
        string b = app.store.GetString("birthday", "");
        if (string.IsNullOrEmpty(b) || b.Length < 5) return "";
        return b.Substring(0, 2) + "-" + b.Substring(3, 2);
    }

    /// <summary>认「我的生日是 3 月 15 日」「生日 03-15」「birthday: 3/15」</summary>
    public static bool TryCommand(App app, string input, out string reply)
    {
        reply = null;
        if (string.IsNullOrEmpty(input)) return false;
        string t = input.Trim();
        bool mentions = t.IndexOf("生日", StringComparison.Ordinal) >= 0 ||
                        t.IndexOf("birthday", StringComparison.OrdinalIgnoreCase) >= 0;
        if (!mentions) return false;

        if (t.IndexOf("忘", StringComparison.Ordinal) >= 0 || t.IndexOf("清除", StringComparison.Ordinal) >= 0)
        {
            app.store.Set("birthday", "");
            app.store.Save();
            reply = Lang.T("feast.birthdayCleared");
            return true;
        }

        Match mm = Regex.Match(t, @"(\d{1,2})\s*[月/\-\.]\s*(\d{1,2})");
        if (!mm.Success) return false;

        int m = int.Parse(mm.Groups[1].Value);
        int d = int.Parse(mm.Groups[2].Value);
        if (m < 1 || m > 12 || d < 1 || d > 31) { reply = Lang.T("feast.birthdayBad"); return true; }

        string val = m.ToString("00") + "-" + d.ToString("00");
        app.store.Set("birthday", val);
        app.store.Save();
        app.Log("记住生日：" + val);
        reply = Lang.F("feast.birthdaySaved", m, d);
        return true;
    }
}
