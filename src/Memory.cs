// 记忆系统：她记住关于你的事，以后聊天自然地带出来。
//
// 怎么"学到"（两种，都不额外花 API 额度）：
//   1. 你直接说「记住：我在备考」—— 两种模式都认，立刻记住
//   2. AI 模式：在系统提示词里要求模型在学到稳定偏好时，
//      在回答最后单独加一行 @@记：<事实>@@；这里解析出来并抹掉那一行
//
// 怎么"用上"：注入 AI 的系统提示词。本地词库模式也会认
//   「你记得什么」这类问句，把记忆念出来。
using System;
using System.Collections.Generic;

static class Memory
{
    public const string Key = "memories";
    public const int MaxItems = 30;

    /// <summary>AI 回答里的记忆标记（中英文冒号都认）</summary>
    const string TagHead = "@@记";
    const string TagTail = "@@";

    /* ---------------- 读写 ---------------- */

    public static List<string> All(App app)
    {
        var list = new List<string>();
        if (app == null || app.store == null) return list;
        var arr = Json.Arr(GetRaw(app.store));
        if (arr == null) return list;
        foreach (object o in arr)
        {
            string s = Json.Str(o, null);
            if (!string.IsNullOrEmpty(s)) list.Add(s);
        }
        return list;
    }

    static object GetRaw(Store store)
    {
        // Store 没有暴露原始对象，用一次 Set/Get 往返借道：
        // 存进去的是 List<object>，Json.Arr 能直接吃
        return store.Raw(Key);
    }

    static void Save(App app, List<string> items)
    {
        var boxed = new List<object>();
        foreach (string s in items) boxed.Add(s);
        app.store.Set(Key, boxed);
        app.store.Save();
    }

    /* ---------------- 学 ---------------- */

    /// <summary>记住一条；返回 false 表示已经有了一样的</summary>
    public static bool Remember(App app, string fact)
    {
        if (app == null || string.IsNullOrEmpty(fact)) return false;
        string f = Clean(fact);
        if (f.Length == 0) return false;
        if (f.Length > 60) f = f.Substring(0, 60).TrimEnd();

        List<string> items = All(app);
        foreach (string s in items)
            if (string.Equals(s, f, StringComparison.OrdinalIgnoreCase)) return false;

        items.Add(f);
        while (items.Count > MaxItems) items.RemoveAt(0);   // 旧的先忘
        Save(app, items);
        app.Log("记住：" + f);
        return true;
    }

    public static void Clear(App app)
    {
        if (app == null) return;
        app.store.Set(Key, new List<object>());
        app.store.Save();
        app.Log("记忆已清空");
    }

    static string Clean(string s)
    {
        string t = (s ?? "").Trim();
        t = t.Trim('：', ':', '。', '，', ',', '.', ' ', '「', '」', '"', '\'');
        return t.Trim();
    }

    /// <summary>从模型回答里摘出 @@记：…@@，返回抹掉标记后的正文</summary>
    public static string ExtractTag(App app, string reply)
    {
        if (string.IsNullOrEmpty(reply)) return reply;
        string text = reply;
        int guard = 0;
        while (guard++ < 3)
        {
            int a = text.IndexOf(TagHead, StringComparison.Ordinal);
            if (a < 0) break;
            int b = text.IndexOf(TagTail, a + TagHead.Length, StringComparison.Ordinal);
            if (b < 0)
            {
                // 只有前半截（模型没写完），整行丢掉
                text = text.Substring(0, a).TrimEnd();
                break;
            }
            string inner = text.Substring(a + TagHead.Length, b - a - TagHead.Length);
            inner = inner.TrimStart('：', ':').Trim();
            if (inner.Length > 0) Remember(app, inner);
            text = (text.Substring(0, a) + text.Substring(b + TagTail.Length));
        }
        return text.Trim();
    }

    /* ---------------- 用 ---------------- */

    /// <summary>给系统提示词追加：已知记忆 + 学习规则</summary>
    public static string PromptBlock(App app)
    {
        var items = All(app);
        var sb = new System.Text.StringBuilder();
        if (items.Count > 0)
        {
            sb.Append(Lang.IsEn
                ? "\nThings you remember about the user (bring them up naturally, don't force it):\n"
                : "\n你记得关于用户的这些事（聊天时可以自然地带出来，但别硬塞）：\n");
            foreach (string s in items) sb.Append("- ").Append(s).Append("\n");
        }
        sb.Append(Lang.T("mem.learnRule"));
        return sb.ToString();
    }

    /* ---------------- 聊天里的指令 ---------------- */

    /// <summary>
    /// 认「记住：…」「你记得什么」「忘掉全部记忆」这几种说法。
    /// 命中返回 true 并把回复写进 reply（这时就不用再走本地词库/AI 了）。
    /// </summary>
    public static bool TryCommand(App app, string input, out string reply)
    {
        reply = null;
        if (string.IsNullOrEmpty(input)) return false;
        string t = input.Trim();

        if (t.StartsWith("记住") || t.StartsWith("记一下") ||
            t.StartsWith("remember", StringComparison.OrdinalIgnoreCase))
        {
            int i = 0;
            while (i < t.Length && !IsSep(t[i])) i++;
            string fact = (i < t.Length) ? t.Substring(i + 1).Trim() : "";
            if (fact.Length == 0)
            {
                reply = Lang.T("mem.needFact");
                return true;
            }
            bool fresh = Remember(app, fact);
            reply = fresh ? Lang.F("mem.saved", fact) : Lang.T("mem.already");
            return true;
        }

        if (t.IndexOf("记得什么", StringComparison.Ordinal) >= 0 ||
            t.IndexOf("记得啥", StringComparison.Ordinal) >= 0 ||
            t.IndexOf("你记得我", StringComparison.Ordinal) >= 0 ||
            t.IndexOf("what do you remember", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var items = All(app);
            if (items.Count == 0) { reply = Lang.T("mem.empty"); return true; }
            var sb = new System.Text.StringBuilder();
            sb.Append(Lang.F("mem.listHead", items.Count));
            foreach (string s in items) sb.Append("\n· ").Append(s);
            reply = sb.ToString();
            return true;
        }

        if (t.IndexOf("忘掉全部记忆", StringComparison.Ordinal) >= 0 ||
            t.IndexOf("清空记忆", StringComparison.Ordinal) >= 0 ||
            t.IndexOf("forget everything", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            Clear(app);
            reply = Lang.T("mem.cleared");
            return true;
        }

        return false;
    }

    /// <summary>分隔符：记住[：: ，,]xxx 都认</summary>
    static bool IsSep(char c)
    {
        return c == '：' || c == ':' || c == '，' || c == ',' || c == ' ' || c == '\t';
    }
}
