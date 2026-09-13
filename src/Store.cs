// 状态与设置持久化
//   · 数据目录默认放在 exe 旁边的 data\（绿色便携：她会记住你，换电脑把文件夹拷走记忆也跟着走）
//   · 旁边写不了（比如放在 Program Files）就退到 %LOCALAPPDATA%\YayaPet
//   · API Key 用 Windows DPAPI 按当前用户加密后再落盘
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

class Store
{
    public string Dir;
    public string FilePath;
    Dictionary<string, object> data = new Dictionary<string, object>();
    public string LoadError;

    public static Store Load(string appDir)
    {
        var s = new Store();
        string custom = Environment.GetEnvironmentVariable("YAYA_DATA_DIR");
        string preferred;
        if (!string.IsNullOrEmpty(custom)) preferred = custom.Trim();
        else preferred = Path.Combine(appDir, "data");

        if (!CanWrite(preferred))
            preferred = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YayaPet");

        s.Dir = preferred;
        s.FilePath = Path.Combine(preferred, "state.json");
        try
        {
            Directory.CreateDirectory(s.Dir);
            if (File.Exists(s.FilePath))
            {
                string txt = File.ReadAllText(s.FilePath, Encoding.UTF8);
                var d = Json.Obj(Json.Parse(txt));
                if (d != null) s.data = d;
            }
        }
        catch (Exception ex)
        {
            s.LoadError = ex.Message;   // 读坏了不影响启动，用默认值继续，下次保存会覆盖
            s.data = new Dictionary<string, object>();
        }
        return s;
    }

    static bool CanWrite(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            string probe = Path.Combine(dir, ".writetest");
            File.WriteAllText(probe, "1", Encoding.UTF8);
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }

    /* ---------------- 读写 ---------------- */

    public string GetString(string key, string def)
    {
        object v;
        if (data.TryGetValue(key, out v) && v is string) return (string)v;
        return def;
    }

    public int GetInt(string key, int def)
    {
        object v;
        if (data.TryGetValue(key, out v)) return Json.Int(v, def);
        return def;
    }

    public double GetDouble(string key, double def)
    {
        object v;
        if (data.TryGetValue(key, out v)) return Json.Num(v, def);
        return def;
    }

    public bool GetBool(string key, bool def)
    {
        object v;
        if (data.TryGetValue(key, out v) && v is bool) return (bool)v;
        return def;
    }

    public bool Has(string key) { return data.ContainsKey(key); }

    public void Set(string key, object value) { data[key] = value; }

    public void Remove(string key) { if (data.ContainsKey(key)) data.Remove(key); }

    /// <summary>原子落盘：先写 .tmp 再替换，避免写一半断电把记忆写坏</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, Json.Write(data), new UTF8Encoding(false));
            if (File.Exists(FilePath))
            {
                try { File.Replace(tmp, FilePath, null); }
                catch { File.Delete(FilePath); File.Move(tmp, FilePath); }
            }
            else File.Move(tmp, FilePath);
        }
        catch { /* 存不下来也不该让她崩掉 */ }
    }

    /* ---------------- 密钥加密（DPAPI） ---------------- */

    static readonly byte[] Entropy = Encoding.UTF8.GetBytes("YayaDesktopPet.v1");

    public static string Protect(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        try
        {
            byte[] enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
            return "dpapi:" + Convert.ToBase64String(enc);
        }
        catch { return "plain:" + plain; }
    }

    public static string Unprotect(string stored)
    {
        if (string.IsNullOrEmpty(stored)) return "";
        try
        {
            if (stored.StartsWith("dpapi:"))
            {
                byte[] enc = Convert.FromBase64String(stored.Substring(6));
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(enc, Entropy, DataProtectionScope.CurrentUser));
            }
            if (stored.StartsWith("plain:")) return stored.Substring(6);
        }
        catch { }
        return stored;
    }

    public string GetSecret(string key)
    {
        return Unprotect(GetString(key, ""));
    }

    public void SetSecret(string key, string plain)
    {
        Set(key, Protect(plain));
    }
}
