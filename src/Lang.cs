// 界面文案表：所有给用户看的字都在这里，中英各一份
//   Lang.Code = "zh" / "en"（存进 state.json 的 lang 字段，设置里可切）
//   用 Lang.T("key") 取当前语言；带占位符的用 Lang.F("key", 参数…)
using System;
using System.Collections.Generic;

static class Lang
{
    public static string Code = "zh";
    public static bool IsEn { get { return Code == "en"; } }

    static readonly Dictionary<string, string[]> Tbl = new Dictionary<string, string[]>();

    static void A(string key, string zh, string en) { Tbl[key] = new string[] { zh, en }; }

    public static string T(string key)
    {
        string[] v;
        if (Tbl.TryGetValue(key, out v)) return v[IsEn ? 1 : 0];
        return key;
    }

    public static string F(string key, params object[] args)
    {
        try { return string.Format(T(key), args); }
        catch { return T(key); }
    }

    static Lang()
    {
        /* ---------- 悬停菜单 ---------- */
        A("menu.pat", "摸摸头", "Pat head");
        A("menu.feed", "投喂蛋糕", "Feed cake");
        A("menu.chat", "和我聊天", "Chat with me");
        A("menu.topic", "找个话题", "Start a topic");
        A("menu.action", "做个动作", "Do a trick");
        A("menu.walk", "走两步", "Take a walk");
        A("menu.roamOn", "自动走动：开", "Auto roam: on");
        A("menu.roamOff", "自动走动：关", "Auto roam: off");
        A("menu.poseFmt", "换个姿势：{0}", "Pose: {0}");
        A("menu.settings", "设置", "Settings");
        A("menu.hide", "让我歇会儿", "Take a break");
        A("pose.front", "正面", "front");
        A("pose.side", "侧面", "side");
        A("pose.back", "背面", "back");

        /* ---------- 托盘 ---------- */
        A("tray.recall", "叫她出来", "Call her back");
        A("tray.hide", "让她歇会儿", "Let her rest");
        A("tray.chat", "和我聊天", "Chat with me");
        A("tray.topic", "找个话题", "Start a topic");
        A("tray.settings", "设置", "Settings");
        A("tray.quit", "退出", "Quit");
        A("tray.tip", "桌面宠物", "Desktop pet");
        A("balloon.title", "{0} 去休息了", "{0} went to rest");
        A("balloon.text", "想找我就在托盘图标上点一下（或者双击）。", "Click the tray icon to call me back.");

        /* ---------- 设置窗 ---------- */
        A("set.titleFmt", "{0} · 设置", "{0} · Settings");
        A("set.name", "她的名字", "Her name");
        A("set.chatMode", "聊天方式", "Chat mode");
        A("set.local", "本地词库（离线，不用联网、不花钱）", "Local library (offline, free)");
        A("set.ai", "AI 对话（真的聊天，要填 Key、要联网）", "AI chat (real conversation, needs a key + internet)");
        A("set.endpoint", "接口地址", "API endpoint");
        A("set.model", "模型", "Model");
        A("set.key", "API Key（只存在你本机，加密保存）", "API Key (stored on this PC, encrypted)");
        A("set.test", "测试连接", "Test connection");
        A("set.extra", "性格补充（可选，AI 模式下会遵守）", "Extra personality (optional, used in AI mode)");
        A("set.size", "桌面上她的大小", "Her size");
        A("set.sizeFmt", "{0}%  ·  她大约 {1} 像素高（屏幕高度的 {2}%）", "{0}%  ·  about {1} px tall ({2}% of your screen)");
        A("set.lang", "语言 / Language", "Language / 语言");
        A("set.roam", "让她自己在桌面上溜达", "Let her wander around the desktop");
        A("set.autostart", "开机自动启动", "Start with Windows");
        A("set.shortcut", "在桌面创建快捷方式", "Create desktop shortcut");
        A("set.save", "保存", "Save");
        A("set.cancel", "取消", "Cancel");
        A("set.infoFmt", "好感度 {0} · {1} · 记忆存在 data 目录", "Affection {0} · {1} · saved in the data folder");
        A("set.shortcutOk", "快捷方式已放到桌面", "Shortcut created on your desktop");
        A("set.shortcutFail", "创建快捷方式失败了", "Failed to create the shortcut");
        A("set.noKey", "先填 API Key 再测试。", "Fill in an API key first.");
        A("set.testing", "正在连接……（最多等 50 秒）", "Connecting… (up to 50 seconds)");
        A("set.aiNoKey", "选了 AI 聊天，但 Key 还没填哦——先填上我再变聪明。", "You picked AI chat, but there's no API key yet — add one and I'll get smarter.");
        A("set.badKeyLine", "设置里还没填 API Key，我先用本地词库陪你聊——想让我变聪明就去设置里填上。", "No API key yet, so I'll keep you company with my local library — add one in Settings and I'll get smarter.");

        /* ---------- 聊天窗 ---------- */
        A("chat.titleFmt", "{0} · 聊天", "{0} · Chat");
        A("chat.send", "发送", "Send");
        A("chat.empty", "跟我说点什么吧，随便聊～", "Say something — anything goes!");
        A("chat.thinkingFmt", "{0} 正在想{1}", "{0} is thinking{1}");
        A("chat.modeLocal", "本地词库（离线）", "Local library (offline)");
        A("chat.modeAiFmt", "AI 对话 · {0}", "AI chat · {0}");
        A("chat.modeAiNoKey", "AI 对话（还没填 API Key，暂时用本地词库）", "AI chat (no key yet — using the local library)");
        A("chat.noReply", "……嗯？你说什么？", "…huh? What did you say?");
        A("chat.aiErrorFallback", "（她好像走神了，再说一次？）", "(She zoned out for a second — say that again?)");

        /* ---------- 首次运行 / 提示 ---------- */
        A("first.shortcutTitle", "要我在桌面放个快捷方式吗？", "Put a shortcut on your desktop?");
        A("first.shortcutText",
            "点「是」我就在你桌面放一个，以后双击桌面图标就能叫我出来。\n点「否」也没关系——程序照常用，设置里随时能补一个。",
            "Click Yes and I'll drop a shortcut on your desktop, so you can call me out anytime.\nNo is fine too — I still work, and you can add one later in Settings.");
        A("first.yes", "放一个", "Yes, please");
        A("first.no", "不用了", "No thanks");
        A("fallbackNotice",
            "（我现在用的是示例形象——想换成你自己的角色，把三视图放进 content\\sheet\\ 再跑一次 tools\\make-sprites.exe）",
            "(I'm using the sample look right now — drop your own character sheet into content\\sheet\\ and run tools\\make-sprites.exe to change me.)");
        A("lang.switched", "好啦，我改说英文了～", "Okay, I'll speak English from now on!");
        A("lang.switchedZh", "好啦，我改说中文了～", "好，我改回中文啦～");
        A("app.startFailed", "启动失败：", "Failed to start: ");
        A("app.startFailedTitle", "桌面宠物", "Desktop pet");
        A("app.noLines", "我的台词本好像读不出来了……你看看 content 下的台词文件？", "I can't read my line files… could you check the content folder?");
    }
}
