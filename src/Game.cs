// 小游戏：猜拳 + 猜数字
//
// 在聊天窗里玩，不另开窗口 —— 聊天本来就是个输入框，打字最自然。
//   「猜拳」  → 她说"石头剪刀布，你先出"，你回「石头/剪刀/布」就开局
//   「猜数字」→ 她想一个 1..100 的数，你猜，她说大了/小了
//   「不玩了」→ 结束
//
// ChatWindow.Send 会把每句话先交给这里；没在玩、也不是开局口令就返回 false，
// 原样交回给记忆 / 生日 / 词库 / AI 那条正常链路。
using System;
using System.Text.RegularExpressions;

static class Game
{
    static readonly Random rng = new Random();

    static string kind = "";        // "" / "rps" / "num"
    static int secret = 0;
    static int tries = 0;

    public static bool Active { get { return kind.Length > 0; } }

    public static string Kind { get { return kind; } }

    /// <summary>自检用：直接看当前是不是在玩、玩哪一种</summary>
    public static string StateForTest()
    {
        if (kind == "rps") return "rps";
        if (kind == "num") return "num";
        return "idle";
    }

    public static void ResetForTest() { kind = ""; secret = 0; tries = 0; }

    public static bool TryHandle(App app, string input, out string reply)
    {
        reply = null;
        if (input == null) return false;
        string t = input.Trim();
        if (t.Length == 0) return false;

        /* 结束 */
        if (Active && (Has(t, "不玩") || Has(t, "不猜") || Has(t, "退出游戏") ||
                       Has(t, "quit") || Has(t, "stop")))
        {
            kind = ""; secret = 0; tries = 0;
            reply = Lang.T("game.quit");
            return true;
        }

        /* 开局：猜拳 */
        if (Has(t, "猜拳") || Has(t, "石头剪刀布") || Has(t, "rock paper") ||
            Has(t, "rock-paper") || Has(t, "rps"))
        {
            kind = "rps";
            reply = Lang.T("game.rpsStart");
            return true;
        }

        /* 开局：猜数字 */
        if (Has(t, "猜数字") || Has(t, "猜个数") || Has(t, "猜一个数") ||
            Has(t, "guess a number") || Has(t, "guess the number"))
        {
            kind = "num";
            secret = rng.Next(1, 101);
            tries = 0;
            reply = Lang.T("game.numStart");
            return true;
        }

        /* 对局中 */
        if (kind == "rps") return Rps(t, out reply);
        if (kind == "num") return Num(app, t, out reply);

        return false;
    }

    /// <summary>菜单入口用：不经过文字匹配，直接开一局，返回开场白</summary>
    public static string Start(bool rps)
    {
        if (rps)
        {
            kind = "rps";
            return Lang.T("game.rpsStart");
        }
        kind = "num";
        secret = rng.Next(1, 101);
        tries = 0;
        return Lang.T("game.numStart");
    }

    /* ---------------- 猜拳 ---------------- */

    static bool Rps(string t, out string reply)
    {
        reply = null;
        int mine = -1;
        if (Has(t, "石头") || Has(t, "rock")) mine = 0;
        else if (Has(t, "剪刀") || Has(t, "scissors")) mine = 1;
        else if (Has(t, "布") || Has(t, "paper")) mine = 2;

        if (mine < 0) { reply = Lang.T("game.rpsWhat"); return true; }

        int hers = rng.Next(3);
        string mv = Lang.T("game.move" + hers);
        string result;
        if (mine == hers) result = Lang.T("game.rpsDraw");
        else if ((mine - hers + 3) % 3 == 2) result = Lang.T("game.rpsWin");    // 石头>剪刀>布>石头
        else result = Lang.T("game.rpsLose");

        reply = Lang.F("game.rpsBoth", mv, result);
        return true;
    }

    /* ---------------- 猜数字 ---------------- */

    static bool Num(App app, string t, out string reply)
    {
        reply = null;
        // 别把「记住：3 月 15 日」这类当猜测
        if (Has(t, "记住") || Has(t, "生日") || Has(t, "remember")) return false;

        Match m = Regex.Match(t, @"\d+");
        if (!m.Success) { reply = Lang.T("game.numBad"); return true; }

        int g;
        if (!int.TryParse(m.Value, out g) || g < 1 || g > 100)
        {
            reply = Lang.T("game.numBad");
            return true;
        }

        tries++;
        if (g == secret)
        {
            int used = tries;
            kind = ""; secret = 0; tries = 0;
            reply = Lang.F("game.numWin", used);
        }
        else if (g < secret) reply = Lang.T("game.numLow");
        else reply = Lang.T("game.numHigh");
        return true;
    }

    static bool Has(string hay, string needle)
    {
        return hay.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
