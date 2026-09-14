// 大模型聊天通道（OpenAI 兼容：DeepSeek / 通义 / OpenAI / 本地 Ollama…都走这个格式）
// 在后台线程里发请求，成功失败都回主线程回调。
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

class AiChat
{
    public const string DefaultBase = "https://api.deepseek.com/v1/chat/completions";
    public const string DefaultModel = "deepseek-chat";

    /// <summary>用户可能只填了域名或 /v1，这里补齐成完整的 chat/completions 地址</summary>
    public static string NormalizeUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return DefaultBase;
        string u = url.Trim();
        while (u.EndsWith("/")) u = u.Substring(0, u.Length - 1);
        if (u.EndsWith("/chat/completions")) return u;
        if (u.EndsWith("/v1")) return u + "/chat/completions";
        int scheme = u.IndexOf("://", StringComparison.Ordinal);
        if (scheme > 0 && u.IndexOf('/', scheme + 3) < 0) return u + "/v1/chat/completions";
        return u;
    }

    public static void Ask(string url, string key, string model, string system,
        List<KeyValuePair<string, string>> messages, Action<string> ok, Action<string> fail)
    {
        Thread th = new Thread(delegate ()
        {
            try
            {
                var body = new Dictionary<string, object>();
                body["model"] = string.IsNullOrEmpty(model) ? DefaultModel : model;
                body["stream"] = false;
                body["temperature"] = 0.9;
                body["max_tokens"] = 400;

                var msgs = new List<object>();
                if (!string.IsNullOrEmpty(system))
                {
                    var sm = new Dictionary<string, object>();
                    sm["role"] = "system";
                    sm["content"] = system;
                    msgs.Add(sm);
                }
                foreach (KeyValuePair<string, string> kv in messages)
                {
                    var m = new Dictionary<string, object>();
                    m["role"] = kv.Key;
                    m["content"] = kv.Value;
                    msgs.Add(m);
                }
                body["messages"] = msgs;

                string payload = Json.Write(body, false);
                byte[] data = Encoding.UTF8.GetBytes(payload);

                var req = (HttpWebRequest)WebRequest.Create(NormalizeUrl(url));
                req.Method = "POST";
                req.ContentType = "application/json";
                req.Accept = "application/json";
                req.UserAgent = "YayaDesktopPet/1.1";
                req.Timeout = 45000;
                req.ReadWriteTimeout = 45000;
                req.Proxy = null;
                req.ContentLength = data.Length;
                if (!string.IsNullOrEmpty(key))
                    req.Headers["Authorization"] = "Bearer " + key;

                using (Stream rs = req.GetRequestStream()) rs.Write(data, 0, data.Length);

                string text;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    text = sr.ReadToEnd();

                string reply = Extract(text);
                if (string.IsNullOrEmpty(reply))
                {
                    if (fail != null) fail("模型返回里没有内容");
                    return;
                }
                if (ok != null) ok(reply);
            }
            catch (WebException we)
            {
                string detail = we.Message;
                try
                {
                    if (we.Response != null)
                    {
                        using (var sr = new StreamReader(we.Response.GetResponseStream(), Encoding.UTF8))
                        {
                            string err = sr.ReadToEnd();
                            if (err.Length > 300) err = err.Substring(0, 300);
                            detail = err;
                        }
                    }
                }
                catch { }
                if (fail != null) fail(detail);
            }
            catch (Exception ex)
            {
                if (fail != null) fail(ex.Message);
            }
        });
        th.IsBackground = true;
        th.Start();
    }

    static string Extract(string json)
    {
        try
        {
            object root = Json.Parse(json);
            var choices = Json.Arr(Json.Get(root, "choices"));
            if (choices == null || choices.Count == 0) return null;
            object msg = Json.Get(choices[0], "message");
            string content = Json.Str(Json.Get(msg, "content"), null);
            if (!string.IsNullOrEmpty(content)) return content.Trim();
            // 有些服务把内容放在 text 里
            return Json.Str(Json.Get(choices[0], "text"), null);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>设置窗口的「测试连接」用：同步发一句很短的话</summary>
    public static string Test(string url, string key, string model, string system)
    {
        string result = null;
        var done = new ManualResetEvent(false);
        var msgs = new List<KeyValuePair<string, string>>();
        msgs.Add(new KeyValuePair<string, string>("user", "在吗？（这是一次连接测试，回一句很短的话就行）"));
        Ask(url, key, model, system, msgs,
            delegate (string ok) { result = "连接成功，她说：" + (ok.Length > 40 ? ok.Substring(0, 40) + "…" : ok); done.Set(); },
            delegate (string err) { result = "连接失败：" + err; done.Set(); });
        done.WaitOne(50000);
        return result ?? "连接超时（50 秒没有回应）";
    }
}
