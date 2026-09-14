// AI 互动反馈：接上 API 之后，互动不再只说词库里那几句固定的，
// 而是把「刚刚发生了什么」发给模型，让它现场说一句。
//
//   · 只对「值得开口」的互动生效 —— 走动、换姿势、开关自动走动这类
//     机械动作会跳过，否则会变成刷屏 + 刷 API
//   · 全局冷却 + 同一时刻只允许一个请求在飞
//   · 本地台词仍然先说（保证点下去立刻有反应），模型那句是异步追加的
//   · 失败/超时静默放弃，不影响体验
using System;
using System.Collections.Generic;
using System.Windows.Forms;

static class AiReaction
{
    /// <summary>两次 AI 互动之间至少隔这么久（毫秒）</summary>
    public const int CooldownMs = 15000;

    static DateTime lastAt = DateTime.MinValue;
    static bool inFlight = false;

    static readonly Dictionary<string, string[]> Events = new Dictionary<string, string[]>();

    static void E(string cat, string zh, string en) { Events[cat] = new string[] { zh, en }; }

    static AiReaction()
    {
        /* 摸 / 点 */
        E("praise_pat",     "用户刚摸了你的头",                       "The user just patted your head.");
        E("click",          "用户刚点了你一下",                       "The user just poked you.");
        E("click_many",     "用户在 1.2 秒里连点了你好几下",           "The user poked you 4+ times in a row, really fast.");
        E("notice_cursor",  "用户的鼠标刚凑到你跟前",                  "The user's mouse cursor just came close to you.");

        /* 投喂 */
        E("feed",           "用户刚喂了你一块草莓蛋糕",                "The user just fed you a slice of strawberry cake.");
        E("feed_full",      "你今天已经吃满三块蛋糕了，用户还想再喂",   "You already had 3 slices of cake today, and the user offered another one.");

        /* 动作 */
        E("action_hop",     "你刚蹦了一下",                          "You just hopped.");
        E("action_spin",    "你刚原地转了个圈",                       "You just spun around.");
        E("action_wave",    "你刚朝用户挥了挥手",                     "You just waved at the user.");
        E("action_dance",   "你刚跳了一小段舞",                       "You just did a little dance.");
        E("action_stretch", "你刚伸了个懒腰",                         "You just stretched.");
        E("action_lookback","你刚回头看了一眼",                       "You just looked back over your shoulder.");
        E("action_sit",     "你刚坐了下来",                          "You just sat down.");

        /* 挂机 */
        E("idle_3min",      "用户已经 3 分钟没理你了",                 "The user hasn't interacted with you for 3 minutes.");
        E("idle_8min",      "用户已经 8 分钟没理你了，你有点犯困",      "The user has ignored you for 8 minutes and you're getting sleepy.");
        E("idle_12min",     "用户已经 12 分钟没理你了，你困得不行",     "The user has ignored you for 12 minutes and you're about to nod off.");
        E("wake_up",        "用户碰了键鼠，你刚醒过来",                "The user touched the keyboard and you just woke up.");
        E("sleep",          "你刚睡着了",                            "You just fell asleep.");
        E("bored",          "你自己待着，有点无聊",                    "You're by yourself and a bit bored.");

        /* 关系 / 场合 */
        E("level_up",       "你和用户的关系升级了",                    "Your friendship level with the user just went up.");
        E("topic",          "用户让你找个话题聊聊",                    "The user asked you to come up with a topic.");
        E("boot",           "你刚被启动，出现在用户的桌面上",           "You just started up and appeared on the user's desktop.");
        E("welcome_back",   "用户好几天没开电脑，今天刚回来",           "The user hasn't opened the PC in days and just came back.");
        E("first_run",      "这是用户第一次运行你",                    "This is the very first time the user runs you.");
        E("hide",           "用户让你歇会儿，你躲起来了",               "The user told you to take a break, so you hid away.");
        E("recall",         "用户刚把你叫回来",                       "The user just called you back.");
    }

    /// <summary>这个分类值不值得让模型现场说一句</summary>
    public static bool WorthAi(string cat)
    {
        return !string.IsNullOrEmpty(cat) && Events.ContainsKey(cat);
    }

    /// <summary>自检用：列出所有会走 AI 的分类</summary>
    public static List<string> CoveredCategories()
    {
        var list = new List<string>(Events.Keys);
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    static string Describe(string cat)
    {
        string[] v;
        if (!Events.TryGetValue(cat, out v)) return null;
        return Lang.IsEn ? v[1] : v[0];
    }

    public static void Ask(App app, string cat, string mood)
    {
        if (app == null || app.pet == null) return;
        if (inFlight) return;
        if (!WorthAi(cat)) return;
        if ((DateTime.Now - lastAt).TotalMilliseconds < CooldownMs) return;
        if (!app.AiReactionsOn) return;      // 这一项会解密 API Key，放最后判

        string ev = Describe(cat);
        if (string.IsNullOrEmpty(ev)) return;

        string key = app.store.GetSecret("apiKey");
        if (string.IsNullOrEmpty(key)) return;

        inFlight = true;
        lastAt = DateTime.Now;

        string sys = app.lines.SystemPrompt(app.PetName, app.lines.LevelName(app.pet.Intimacy),
                                            app.store.GetString("personaExtra", ""), app.pet.Intimacy)
                     + "\n" + Lang.T("ai.reactionRule");

        var msgs = new List<KeyValuePair<string, string>>();
        msgs.Add(new KeyValuePair<string, string>("user", Lang.F("ai.reactionAsk", ev)));

        app.Log("AI 互动反馈请求 · " + cat);
        AiChat.Ask(app.store.GetString("apiBase", AiChat.DefaultBase), key,
            app.store.GetString("apiModel", AiChat.DefaultModel), sys, msgs,
            delegate (string reply)
            {
                inFlight = false;
                string line = Clamp(reply);
                if (string.IsNullOrEmpty(line)) return;
                app.Log("AI 互动反馈回复 · " + cat + " · " + line);
                SayOnUiThread(app, line, mood);
            },
            delegate (string err)
            {
                inFlight = false;
                app.Log("AI 互动反馈失败 · " + cat + " · " + err);
                // 静默放弃：本地台词已经说过了
            });
    }

    /// <summary>回调在后台线程上，碰到窗口必须回到 UI 线程</summary>
    static void SayOnUiThread(App app, string text, string mood)
    {
        try
        {
            PetWindow pet = app.pet;
            if (pet == null || pet.IsDisposed) return;
            if (pet.InvokeRequired) pet.BeginInvoke((MethodInvoker)delegate { pet.Say(text, mood); });
            else pet.Say(text, mood);
        }
        catch { }
    }

    /// <summary>模型有时会一口气说好几句，这里只留第一句，并且限长</summary>
    static string Clamp(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        string s = text.Replace("\r", "").Trim();
        int nl = s.IndexOf('\n');
        if (nl > 0) s = s.Substring(0, nl).Trim();
        s = s.Trim('"', '\'', '“', '”', '「', '」', '『', '』', ' ');
        int max = Lang.IsEn ? 90 : 42;
        if (s.Length > max) s = s.Substring(0, max).TrimEnd() + "…";
        return s;
    }
}
