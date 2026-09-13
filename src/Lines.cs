// 台词库 + 本地聊天引擎
//   content\lines.json 结构：
//   {
//     "defaultName": "娅娅",
//     "persona": "……（AI 模式下的系统提示词素材）",
//     "levels": [ {"lv":1,"name":"刚认识","need":0}, ... ],
//     "categories": { "boot": ["台词", {"text":"更熟才说","minLv":3}], ... },
//     "chat_keywords": { "chat_greet": ["你好","嗨", ...], ... },
//     "chat_fallback": ["chat_unknown","chat_ask_back"]
//   }
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

class Lines
{
    public string DefaultName = "娅娅";
    public string Persona = "";
    public string LoadError = "";

    public List<Level> Levels = new List<Level>();
    public Dictionary<string, List<object>> Cats = new Dictionary<string, List<object>>();
    public List<Keyword> Keywords = new List<Keyword>();
    public List<string> ChatFallback = new List<string>();
    public List<string> DefaultFallback = new List<string>();

    public class Level { public int Lv; public string Name; public int Need; }
    public class Keyword { public string Word; public string Cat; }

    Dictionary<string, List<string>> recent = new Dictionary<string, List<string>>();
    static readonly Random rng = new Random();

    /* ---------------- 载入 ---------------- */

    public static Lines Load(string path)
    {
        var L = new Lines();
        try
        {
            if (!File.Exists(path))
            {
                L.LoadError = "找不到台词库：" + path;
                return L;
            }
            var root = Json.Obj(Json.Parse(File.ReadAllText(path, Encoding.UTF8)));
            if (root == null) { L.LoadError = "台词库格式不对（顶层不是对象）"; return L; }

            L.DefaultName = Json.Str(Json.Get(root, "defaultName"), "娅娅");
            L.Persona = Json.Str(Json.Get(root, "persona"), "");

            var lvArr = Json.Arr(Json.Get(root, "levels"));
            if (lvArr != null)
            {
                foreach (object o in lvArr)
                {
                    var d = Json.Obj(o);
                    if (d == null) continue;
                    var lv = new Level();
                    lv.Lv = Json.Int(Json.Get(d, "lv"), L.Levels.Count + 1);
                    lv.Name = Json.Str(Json.Get(d, "name"), "等级" + lv.Lv);
                    lv.Need = Json.Int(Json.Get(d, "need"), 0);
                    L.Levels.Add(lv);
                }
            }
            if (L.Levels.Count == 0)
            {
                L.Levels.Add(new Level { Lv = 1, Name = "刚认识", Need = 0 });
            }

            var cats = Json.Obj(Json.Get(root, "categories"));
            if (cats != null)
            {
                foreach (KeyValuePair<string, object> kv in cats)
                {
                    var arr = Json.Arr(kv.Value);
                    if (arr != null) L.Cats[kv.Key] = arr;
                }
            }

            var kw = Json.Obj(Json.Get(root, "chat_keywords"));
            if (kw != null)
            {
                foreach (KeyValuePair<string, object> kv in kw)
                {
                    var arr = Json.Arr(kv.Value);
                    if (arr == null) continue;
                    foreach (object o in arr)
                    {
                        string w = Json.Str(o, null);
                        if (string.IsNullOrEmpty(w)) continue;
                        L.Keywords.Add(new Keyword { Word = w.Trim().ToLowerInvariant(), Cat = kv.Key });
                    }
                }
                // 长词优先，避免「晚安」被「晚上好」这种短配抢走
                L.Keywords.Sort(delegate (Keyword a, Keyword b) { return b.Word.Length - a.Word.Length; });
            }

            var fb = Json.Arr(Json.Get(root, "chat_fallback"));
            if (fb != null) foreach (object o in fb) { string s = Json.Str(o, null); if (s != null) L.ChatFallback.Add(s); }
            if (L.ChatFallback.Count == 0) { L.ChatFallback.Add("chat_unknown"); L.ChatFallback.Add("chat_ask_back"); }

            L.DefaultFallback.Add("chat_unknown");
        }
        catch (Exception ex)
        {
            L.LoadError = ex.Message;
        }
        return L;
    }

    /* ---------------- 等级 ---------------- */

    public int LevelOf(int intimacy)
    {
        int lv = 1;
        for (int i = 0; i < Levels.Count; i++)
            if (intimacy >= Levels[i].Need) lv = Levels[i].Lv;
        return lv;
    }

    public string LevelName(int intimacy)
    {
        string name = Levels[0].Name;
        for (int i = 0; i < Levels.Count; i++)
            if (intimacy >= Levels[i].Need) name = Levels[i].Name;
        return name;
    }

    public int NextLevelNeed(int intimacy)
    {
        for (int i = 0; i < Levels.Count; i++)
            if (Levels[i].Need > intimacy) return Levels[i].Need;
        return -1;
    }

    public bool Has(string cat) { return Cats.ContainsKey(cat) && Cats[cat].Count > 0; }

    /* ---------------- 取一句台词 ---------------- */

    public string Pick(string cat, Dictionary<string, string> vars, int lv)
    {
        if (string.IsNullOrEmpty(cat)) return null;
        List<object> list;
        if (!Cats.TryGetValue(cat, out list) || list.Count == 0) return null;

        var pool = new List<string>();
        var loose = new List<string>();
        foreach (object o in list)
        {
            string t = null; int minLv = 0;
            if (o is string) t = (string)o;
            else
            {
                var d = Json.Obj(o);
                if (d != null)
                {
                    t = Json.Str(Json.Get(d, "text"), null);
                    minLv = Json.Int(Json.Get(d, "minLv"), 0);
                }
            }
            if (string.IsNullOrEmpty(t)) continue;
            loose.Add(t);
            if (minLv <= lv) pool.Add(t);
        }
        if (pool.Count == 0) pool = loose;     // 高等级台词不够时也不要哑掉
        if (pool.Count == 0) return null;

        List<string> rec;
        if (!recent.TryGetValue(cat, out rec)) { rec = new List<string>(); recent[cat] = rec; }

        var avail = new List<string>();
        foreach (string t in pool) if (!rec.Contains(t)) avail.Add(t);
        if (avail.Count == 0) { rec.Clear(); avail = pool; }

        string pick = avail[rng.Next(avail.Count)];
        rec.Add(pick);
        int keep = Math.Min(4, Math.Max(1, pool.Count / 2));
        while (rec.Count > keep) rec.RemoveAt(0);

        return Fill(pick, vars);
    }

    public string Fill(string text, Dictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(text)) return text;
        string s = text;
        s = s.Replace("{name}", Var(vars, "name", DefaultName));
        s = s.Replace("{days}", Var(vars, "days", "好几"));
        s = s.Replace("{lvName}", Var(vars, "lvName", "朋友"));
        s = s.Replace("{topic}", Var(vars, "topic", "今天吃了什么"));
        s = s.Replace("{time}", Var(vars, "time", "现在"));
        return s;
    }

    static string Var(Dictionary<string, string> vars, string key, string def)
    {
        if (vars == null) return def;
        string v;
        if (vars.TryGetValue(key, out v) && !string.IsNullOrEmpty(v)) return v;
        return def;
    }

    /* ---------------- 本地聊天 ---------------- */

    public static string Normalize(string input)
    {
        if (input == null) return "";
        // 保留空格：英文关键词是多词的（"good morning"），空格全删掉就匹配不上了
        var sb = new StringBuilder();
        bool lastSpace = false;
        string s = input.Trim().ToLowerInvariant();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c))
            {
                if (!lastSpace && sb.Length > 0) { sb.Append(' '); lastSpace = true; }
                continue;
            }
            lastSpace = false;
            sb.Append(c);
        }
        return sb.ToString().TrimEnd();
    }

    static bool IsWordChar(char c)
    {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
    }

    /// <summary>子串匹配；关键词两端只要有一端是 ASCII 字母/数字，就要求词边界（免得 hi 命中 this）</summary>
    public static bool ContainsWord(string hay, string needle)
    {
        if (string.IsNullOrEmpty(hay) || string.IsNullOrEmpty(needle)) return false;
        int from = 0;
        while (true)
        {
            int idx = hay.IndexOf(needle, from, StringComparison.Ordinal);
            if (idx < 0) return false;
            bool leftOk = !IsWordChar(needle[0]) || idx == 0 || !IsWordChar(hay[idx - 1]);
            bool rightOk = !IsWordChar(needle[needle.Length - 1]) ||
                           idx + needle.Length >= hay.Length || !IsWordChar(hay[idx + needle.Length]);
            if (leftOk && rightOk) return true;
            from = idx + 1;
        }
    }

    public string MatchCategory(string norm)
    {
        if (string.IsNullOrEmpty(norm)) return null;
        foreach (Keyword k in Keywords)
        {
            if (k.Word.Length == 0) continue;
            if (ContainsWord(norm, k.Word)) return k.Cat;
        }
        return null;
    }

    /// <summary>本地词库回话：返回 1–2 句</summary>
    public List<string> LocalReply(string input, int lv, Dictionary<string, string> vars)
    {
        var result = new List<string>();
        string norm = Normalize(input);
        if (norm.Length == 0) return result;

        string cat = MatchCategory(norm);
        if (cat == null || !Has(cat))
            cat = (norm.IndexOf('?') >= 0 || norm.IndexOf('？') >= 0) ? "chat_question" : "chat_unknown";

        string first = Pick(cat, vars, lv);
        if (first == null)
        {
            foreach (string alt in ChatFallback)
            {
                first = Pick(alt, vars, lv);
                if (first != null) break;
            }
        }
        if (first != null) result.Add(first);

        string extraCat = ExtraCategory(cat);
        if (extraCat != null && Has(extraCat) && rng.NextDouble() < ExtraChance(cat))
        {
            string second = Pick(extraCat, vars, lv);
            if (!string.IsNullOrEmpty(second) && second != first) result.Add(second);
        }
        return result;
    }

    static string ExtraCategory(string cat)
    {
        if (cat == "chat_sad" || cat == "chat_lonely") return "chat_comfort";
        if (cat == "chat_unknown" || cat == "chat_question") return "chat_ask_back";
        if (cat == "chat_greet" || cat == "chat_howareyou") return "chat_ask_back";
        return "chat_followup";
    }

    static double ExtraChance(string cat)
    {
        if (cat == "chat_sad" || cat == "chat_lonely") return 0.55;
        if (cat == "chat_unknown") return 0.55;
        if (cat == "chat_question") return 0.4;
        if (cat == "chat_greet" || cat == "chat_howareyou") return 0.3;
        return 0.18;
    }

    /* ---------------- 给 AI 用的提示词 ---------------- */

    public string SystemPrompt(string name, string levelName, string extra, int intimacy)
    {
        if (Lang.IsEn) return SystemPromptEn(name, levelName, extra, intimacy);
        var sb = new StringBuilder();
        sb.Append("你叫「").Append(name).Append("」，是一个住在用户电脑桌面上的女孩子。");
        sb.Append("你不是助手、不是学习软件、也不是客服，你是用户的朋友，陪他闲聊解闷。\n\n");
        if (!string.IsNullOrEmpty(Persona)) sb.Append("你的性格：").Append(Persona).Append("\n\n");
        sb.Append("说话规则：\n");
        sb.Append("1. 只用中文口语回答，像微信聊天一样短，一般 1–3 句，不要长篇大论。\n");
        sb.Append("2. 不要用 emoji（桌面宠物的字体画不出 emoji），可以用「～」「！」「……」这样的标点。\n");
        sb.Append("3. 不要有助手腔：别说「有什么可以帮你的吗」「请问」「建议您」「作为AI」。\n");
        sb.Append("4. 不要提学习、作业、考试、背单词、刷题这类话题，你不关心这些。\n");
        sb.Append("5. 可以吐槽、撒娇、开玩笑、反问他；不知道就说不知道，不要编。\n");
        sb.Append("6. 不要输出括号里的动作描写超过一句，也不要写旁白式的长段描述。\n");
        sb.Append("你对用户的好感度：").Append(intimacy).Append("，关系阶段：「").Append(levelName).Append("」。");
        sb.Append("关系越熟你可以越放松、越随便、越爱开玩笑。\n");
        if (!string.IsNullOrEmpty(extra)) sb.Append("\n用户给你补充的设定（优先遵守）：").Append(extra).Append("\n");
        return sb.ToString();
    }

    /// <summary>英文模式下的系统提示词</summary>
    public string SystemPromptEn(string name, string levelName, string extra, int intimacy)
    {
        var sb = new StringBuilder();
        sb.Append("Your name is \"").Append(name).Append("\" and you are a girl who lives on the user's Windows desktop. ");
        sb.Append("You are NOT an assistant, NOT a study helper and NOT customer support — you are the user's friend, hanging out and chatting.\n\n");
        if (!string.IsNullOrEmpty(Persona)) sb.Append("Your personality: ").Append(Persona).Append("\n\n");
        sb.Append("Speaking rules:\n");
        sb.Append("1. Reply in casual spoken English, like a text message: usually 1-3 short sentences, never a wall of text.\n");
        sb.Append("2. Do not use emoji (the desktop pet draws text with GDI and cannot render them). \"...\" and \"!\" are fine.\n");
        sb.Append("3. Never sound like an assistant: no \"How can I help you?\", \"Please note\", \"I recommend\", \"As an AI\".\n");
        sb.Append("4. Never bring up studying, homework, exams, school or grades. You don't care about those.\n");
        sb.Append("5. Tease, joke, be a little clingy, ask questions back. If you don't know something, say so — don't make things up.\n");
        sb.Append("6. Use at most one short parenthetical action, and don't write long narration.\n");
        sb.Append("Your affection toward the user is ").Append(intimacy).Append(", friendship stage: \"").Append(levelName).Append("\". ");
        sb.Append("The closer you two are, the more relaxed, casual and playful you can be.\n");
        if (!string.IsNullOrEmpty(extra)) sb.Append("\nExtra instructions from the user (follow these first): ").Append(extra).Append("\n");
        return sb.ToString();
    }
}
