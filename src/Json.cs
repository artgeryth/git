// 迷你 JSON 解析 / 序列化
// 桌面宠物要能在「只有 .NET Framework、不装任何东西」的机器上跑，所以不引用任何外部 JSON 库。
// 只支持标准 JSON：对象 / 数组 / 字符串 / 数字 / true / false / null。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

static class Json
{
    /* ================= 解析 ================= */

    public static object Parse(string s)
    {
        if (s == null) throw new FormatException("JSON 内容为空");
        int i = 0;
        object v = ParseValue(s, ref i);
        return v;
    }

    static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

    static object ParseValue(string s, ref int i)
    {
        Ws(s, ref i);
        if (i >= s.Length) throw new FormatException("JSON 意外结束");
        char c = s[i];
        switch (c)
        {
            case '{': return ParseObject(s, ref i);
            case '[': return ParseArray(s, ref i);
            case '"': return ParseString(s, ref i);
            case 't': Lit(s, ref i, "true"); return true;
            case 'f': Lit(s, ref i, "false"); return false;
            case 'n': Lit(s, ref i, "null"); return null;
            default: return ParseNumber(s, ref i);
        }
    }

    static void Lit(string s, ref int i, string lit)
    {
        if (i + lit.Length > s.Length || string.CompareOrdinal(s, i, lit, 0, lit.Length) != 0)
            throw new FormatException("非法字面量（位置 " + i + "）");
        i += lit.Length;
    }

    static Dictionary<string, object> ParseObject(string s, ref int i)
    {
        var d = new Dictionary<string, object>();
        i++; // {
        Ws(s, ref i);
        if (i < s.Length && s[i] == '}') { i++; return d; }
        while (true)
        {
            Ws(s, ref i);
            if (i >= s.Length || s[i] != '"') throw new FormatException("对象的键必须是字符串（位置 " + i + "）");
            string k = ParseString(s, ref i);
            Ws(s, ref i);
            if (i >= s.Length || s[i] != ':') throw new FormatException("键后面缺少冒号（位置 " + i + "）");
            i++;
            d[k] = ParseValue(s, ref i);
            Ws(s, ref i);
            if (i >= s.Length) throw new FormatException("对象没有闭合");
            if (s[i] == ',') { i++; continue; }
            if (s[i] == '}') { i++; return d; }
            throw new FormatException("对象里出现意外字符（位置 " + i + "）");
        }
    }

    static List<object> ParseArray(string s, ref int i)
    {
        var a = new List<object>();
        i++; // [
        Ws(s, ref i);
        if (i < s.Length && s[i] == ']') { i++; return a; }
        while (true)
        {
            a.Add(ParseValue(s, ref i));
            Ws(s, ref i);
            if (i >= s.Length) throw new FormatException("数组没有闭合");
            if (s[i] == ',') { i++; continue; }
            if (s[i] == ']') { i++; return a; }
            throw new FormatException("数组里出现意外字符（位置 " + i + "）");
        }
    }

    static string ParseString(string s, ref int i)
    {
        var sb = new StringBuilder();
        i++; // 开引号
        while (true)
        {
            if (i >= s.Length) throw new FormatException("字符串没有闭合");
            char c = s[i++];
            if (c == '"') return sb.ToString();
            if (c != '\\') { sb.Append(c); continue; }
            if (i >= s.Length) throw new FormatException("转义没写完");
            char e = s[i++];
            switch (e)
            {
                case '"': sb.Append('"'); break;
                case '\\': sb.Append('\\'); break;
                case '/': sb.Append('/'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'u':
                    if (i + 4 > s.Length) throw new FormatException("\\u 不完整");
                    sb.Append((char)ushort.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    i += 4;
                    break;
                default: throw new FormatException("未知转义：\\" + e);
            }
        }
    }

    static object ParseNumber(string s, ref int i)
    {
        int start = i;
        if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
        while (i < s.Length)
        {
            char c = s[i];
            if (char.IsDigit(c) || c == '.' || c == 'e' || c == 'E' || c == '-' || c == '+') i++;
            else break;
        }
        string t = s.Substring(start, i - start);
        double d;
        if (t.Length == 0 || !double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
            throw new FormatException("不是合法的数字：" + t);
        return d;
    }

    /* ================= 序列化 ================= */

    public static string Write(object v) { return Write(v, true); }

    public static string Write(object v, bool pretty)
    {
        var sb = new StringBuilder();
        WriteValue(sb, v, pretty, 0);
        return sb.ToString();
    }

    static void WriteValue(StringBuilder sb, object v, bool pretty, int depth)
    {
        if (v == null) { sb.Append("null"); return; }

        if (v is string) { WriteString(sb, (string)v); return; }
        if (v is bool) { sb.Append(((bool)v) ? "true" : "false"); return; }
        if (v is int || v is long) { sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture)); return; }
        if (v is double || v is float || v is decimal)
        {
            double d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
            if (d == Math.Floor(d) && Math.Abs(d) < 1e15) sb.Append(((long)d).ToString(CultureInfo.InvariantCulture));
            else sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
            return;
        }

        var dict = v as IDictionary<string, object>;
        if (dict != null)
        {
            if (dict.Count == 0) { sb.Append("{}"); return; }
            sb.Append('{');
            bool first = true;
            foreach (KeyValuePair<string, object> kv in dict)
            {
                if (!first) sb.Append(',');
                first = false;
                if (pretty) { sb.Append('\n'); Indent(sb, depth + 1); }
                WriteString(sb, kv.Key);
                sb.Append(':');
                if (pretty) sb.Append(' ');
                WriteValue(sb, kv.Value, pretty, depth + 1);
            }
            if (pretty) { sb.Append('\n'); Indent(sb, depth); }
            sb.Append('}');
            return;
        }

        var list = v as IEnumerable;
        if (list != null)
        {
            sb.Append('[');
            bool first = true;
            foreach (object o in list)
            {
                if (!first) sb.Append(',');
                first = false;
                if (pretty) { sb.Append('\n'); Indent(sb, depth + 1); }
                WriteValue(sb, o, pretty, depth + 1);
            }
            if (pretty && !first) { sb.Append('\n'); Indent(sb, depth); }
            sb.Append(']');
            return;
        }

        WriteString(sb, v.ToString());
    }

    static void Indent(StringBuilder sb, int n) { for (int i = 0; i < n; i++) sb.Append("  "); }

    static void WriteString(StringBuilder sb, string s)
    {
        sb.Append('"');
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }

    /* ================= 取值助手 ================= */

    public static Dictionary<string, object> Obj(object o) { return o as Dictionary<string, object>; }
    public static List<object> Arr(object o) { return o as List<object>; }

    public static object Get(object o, string key)
    {
        var d = Obj(o);
        if (d == null) return null;
        object v;
        return d.TryGetValue(key, out v) ? v : null;
    }

    public static string Str(object o, string def)
    {
        if (o is string) return (string)o;
        return def;
    }

    public static double Num(object o, double def)
    {
        if (o is double) return (double)o;
        if (o is int) return (int)o;
        if (o is long) return (long)o;
        return def;
    }

    public static int Int(object o, int def) { return (int)Math.Round(Num(o, def)); }

    public static bool Bool(object o, bool def) { return (o is bool) ? (bool)o : def; }

    /// <summary>安全地取 obj.key.key……</summary>
    public static object Path(object root, params string[] keys)
    {
        object cur = root;
        for (int i = 0; i < keys.Length; i++)
        {
            cur = Get(cur, keys[i]);
            if (cur == null) return null;
        }
        return cur;
    }
}
