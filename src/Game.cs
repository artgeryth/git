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

    /// <summary>自检用：上一局的归属 —— "iwin" 她赢 / "ilose" 你赢 / "draw" 平</summary>
    public static string LastOutcome = "";
    /// <summary>自检用：上一局她是不是耍赖了</summary>
    public static bool LastWasCheat = false;

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
        if (kind == "rps") return Rps(app, t, out reply);
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

    /// <summary>她耍赖的概率：小概率偷偷换成能赢你的那一手，然后嘴硬不承认</summary>
    public const double CheatChance = 0.08;

    static bool Rps(App app, string t, out string reply)
    {
        reply = null;
        int mine = -1;
        if (Has(t, "石头") || Has(t, "rock")) mine = 0;
        else if (Has(t, "剪刀") || Has(t, "scissors")) mine = 1;
        else if (Has(t, "布") || Has(t, "paper")) mine = 2;

        if (mine < 0) { reply = Lang.T("game.rpsWhat"); return true; }

        int hers = rng.Next(3);
        bool cheated = rng.NextDouble() < CheatChance;
        if (cheated) hers = (mine + 2) % 3;         // 换成刚好赢你的那一手

        int diff = (mine - hers + 3) % 3;           // 0=平  1=她赢  2=你赢
        string mv = Lang.T("game.move" + hers);
        string result;
        if (diff == 0) result = PickLine(app, "game_rps_draw", Lang.T("game.rpsDraw"));
        else if (diff == 2) result = PickLine(app, "game_rps_ilose", Lang.T("game.rpsWin"));
        else result = PickLine(app, cheated ? "game_rps_cheat" : "game_rps_iwin", Lang.T("game.rpsLose"));

        // 记分：存在 state.json 里，跨次启动也记得，她会对这个比分有执念
        int me = 0, you = 0, dr = 0;
        if (app != null)
        {
            me = app.store.GetInt("rpsMe", 0);
            you = app.store.GetInt("rpsYou", 0);
            dr = app.store.GetInt("rpsDraw", 0);
        }
        if (diff == 0) dr++; else if (diff == 2) you++; else me++;
        if (app != null)
        {
            app.store.Set("rpsMe", me);
            app.store.Set("rpsYou", you);
            app.store.Set("rpsDraw", dr);
            app.SaveSoon();
        }

        LastOutcome = diff == 0 ? "draw" : (diff == 2 ? "ilose" : "iwin");
        LastWasCheat = cheated;

        reply = Lang.F("game.rpsBoth", mv, result) + "  " + Lang.F("game.rpsScore", me, you);
        if (cheated && app != null) app.Log("猜拳耍赖了一次（当前 我" + me + " : 你" + you + "）");
        return true;
    }

    /// <summary>优先从台词库取（用户可编辑），取不到就用代码里的兜底文案</summary>
    static string PickLine(App app, string cat, string fallback)
    {
        try
        {
            if (app != null && app.lines != null && app.pet != null && app.lines.Has(cat))
            {
                string s = app.lines.Pick(cat, app.Vars(), app.pet.Level);
                if (!string.IsNullOrEmpty(s)) return s;
            }
        }
        catch { }
        return fallback;
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
