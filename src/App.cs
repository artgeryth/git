// 应用程序本体：装配各个窗口、托盘图标、状态存取、AI 通道
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

class App
{
    public float S = 1f;                       // DPI 缩放（150% 屏 → 1.5）
    public string AppDir = "";
    public string ContentDir = "";
    public string LogFile = "";

    public Store store;
    public Lines lines;
    public Sprite sprite;
    public PetWindow pet;
    public ChatWindow chat;
    public SettingsWindow settings;

    NotifyIcon tray;
    System.Windows.Forms.Timer saveTimer;
    bool savePending;
    public bool timeGreeted;
    static EventWaitHandle activateEvent;
    Random rng = new Random();

    static readonly string[] Topics = new string[]
    {
        "今天吃了什么", "周末打算干嘛", "最近在追什么剧", "有没有好听的歌推荐",
        "你那边天气怎么样", "最近在玩什么游戏", "有没有想买但还没买的东西", "今天遇到什么好玩的事"
    };

    public string PetName { get { return pet == null ? "娅娅" : pet.PetName; } }

    /// <summary>当前语言用哪个台词库</summary>
    public string LinesFileName() { return Lang.IsEn ? "lines.en.json" : "lines.json"; }

    /// <summary>切换语言：换台词库 + 刷新托盘菜单（菜单/聊天窗/设置窗都是打开时取文案，会自己跟上）</summary>
    public void ApplyLanguage(string code)
    {
        Lang.Code = code == "en" ? "en" : "zh";
        store.Set("lang", Lang.Code);
        store.Save();
        lines = Lines.Load(Path.Combine(ContentDir, LinesFileName()));
        if (lines.LoadError.Length > 0) Log("切换语言后台词库读取失败：" + lines.LoadError);
        if (tray != null) { tray.Text = PetName + " · " + Lang.T("tray.tip"); BuildTrayMenu(); }
        if (pet != null) pet.Say(Lang.T(Lang.IsEn ? "lang.switched" : "lang.switchedZh"), "happy");
        Log("语言切换为 " + Lang.Code);
    }
    public bool ChatVisible { get { return chat != null && chat.Visible; } }
    /// <summary>排障开关：设 YAYA_DEBUG=1 时每 5 秒把窗口位置/坐标写进日志</summary>
    public bool Debug = Environment.GetEnvironmentVariable("YAYA_DEBUG") == "1";
    /// <summary>无界面模式（--selftest / --dump）：不弹任何对话框</summary>
    public bool headless = false;

    /* ================= 入口 ================= */

    [STAThread]
    static void Main(string[] args)
    {
        string dumpPath = null;
        string selfTestDir = null;
        int dumpAfter = 1500;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--dump" && i + 1 < args.Length) dumpPath = args[i + 1];
            if (args[i] == "--dump-after" && i + 1 < args.Length) dumpAfter = int.Parse(args[i + 1]);
            if (args[i] == "--selftest" && i + 1 < args.Length) selfTestDir = args[i + 1];
        }

        bool created;
        Mutex mtx = new Mutex(true, "YayaDesktopPet_SingleInstance", out created);
        if (!created)
        {
            // 已经在跑了：把那个实例叫出来
            try { EventWaitHandle.OpenExisting("YayaDesktopPet_Activate").Set(); }
            catch { }
            return;
        }

        activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "YayaDesktopPet_Activate");
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        App app = new App();
        app.headless = (dumpPath != null || selfTestDir != null);
        try
        {
            app.Start();
            if (dumpPath != null) app.StartFrameDump(dumpPath, dumpAfter);
            if (selfTestDir != null)
            {
                AttachParentConsole();
                app.RunSelfTest(selfTestDir);
                app.SaveNow();
                return;
            }
        }
        catch (Exception ex)
        {
            app.Log("启动失败：" + ex);
            MessageBox.Show(Lang.T("app.startFailed") + "\n" + ex.Message + "\n\n" + app.LogFile,
                Lang.T("app.startFailedTitle"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Thread watch = new Thread(delegate ()
        {
            while (true)
            {
                try { activateEvent.WaitOne(); }
                catch { break; }
                try { app.pet.BeginInvoke((MethodInvoker)delegate { app.RecallPet(); }); }
                catch { }
            }
        });
        watch.IsBackground = true;
        watch.Start();

        Application.Run(app.pet);
        app.SaveNow();
    }

    /* ================= 启动 ================= */

    void Start()
    {
        AppDir = Path.GetDirectoryName(Application.ExecutablePath);
        ContentDir = Path.Combine(AppDir, "content");

        store = Store.Load(AppDir);
        LogFile = Path.Combine(store.Dir, "pet.log");
        Lang.Code = store.GetString("lang", "zh") == "en" ? "en" : "zh";
        lines = Lines.Load(Path.Combine(ContentDir, LinesFileName()));
        sprite = new Sprite(Path.Combine(ContentDir, "sprites"), Path.Combine(ContentDir, "placeholder"));

        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) S = g.DpiX / 96f;
        if (S < 1f) S = 1f;

        Log("启动 · 版本 1.1 · DPI 缩放 " + S.ToString("0.##") +
            (lines.LoadError.Length > 0 ? " · 台词库有问题：" + lines.LoadError : " · 台词库 OK"));
        Sprite.Probe = delegate (string s) { Log("SpriteProbe " + s); };

        pet = new PetWindow(this);
        pet.PetName = store.GetString("name", lines.DefaultName);
        pet.Intimacy = Math.Max(0, store.GetInt("intimacy", 0));
        pet.Level = lines.LevelOf(pet.Intimacy);
        pet.Pose = store.GetString("pose", Sprite.Front);
        pet.RoamEnabled = store.GetBool("roam", true);
        pet.SizeScale = (float)store.GetDouble("sizeScale", 1.0);

        Rectangle wa = Screen.PrimaryScreen.WorkingArea;
        pet.PetX = store.GetInt("x", wa.Right - (int)(260 * S));
        pet.PetY = store.GetInt("y", wa.Bottom - (int)(380 * S));

        string today = DateTime.Now.ToString("yyyy-MM-dd");
        string feedDay = store.GetString("feedDay", today);
        int feedCount = (feedDay == today) ? store.GetInt("feedCount", 0) : 0;
        if (feedDay != today) { store.Set("feedDay", today); store.Set("feedCount", 0); }
        pet.LoadFeed(today, feedCount);

        pet.ShowPet();
        InitTray();

        saveTimer = new System.Windows.Forms.Timer();
        saveTimer.Interval = 1500;
        saveTimer.Tick += delegate { if (savePending) SaveNow(); };
        saveTimer.Start();

        bool firstRun = !store.Has("installed");
        if (firstRun) store.Set("installed", true);
        // 第一次运行先问一句要不要建桌面快捷方式（公开发布时，不该不打招呼就往别人桌面放东西）
        // 已经有的快捷方式则每次启动重写一遍，用来刷新图标/路径
        bool allowShortcut = true;
        if (firstRun)
        {
            allowShortcut = headless || AskShortcutConsent();
            store.Set("shortcutDeclined", !allowShortcut);
        }
        bool shortcutOk = allowShortcut && EnsureShortcut(firstRun);

        System.Windows.Forms.Timer welcome = new System.Windows.Forms.Timer();
        welcome.Interval = 1000;
        welcome.Tick += delegate
        {
            welcome.Stop();
            welcome.Dispose();
            try
            {
                int days = DaysSinceLastSeen();
                if (firstRun)
                {
                    pet.SayCat("first_run");
                    if (shortcutOk) pet.SayCat("shortcut_made");
                }
                else if (days >= 2) pet.SayCat("welcome_back");
                else pet.SayCat("boot");
                if (sprite.UsedFallback)
                {
                    Log("立绘缺失，已回退到原创占位形象");
                    pet.Say(Lang.T("fallbackNotice"), "shy");
                }
                // 节日彩蛋：说完开场白再补一句（她会排队说出来）
                try
                {
                    string feast = Feast.Key(this);
                    if (!string.IsNullOrEmpty(feast)) { Log("今天是节日：" + feast); pet.SayCat(feast, "happy"); }
                }
                catch (Exception ex) { Log("节日彩蛋出错：" + ex.Message); }
                SaveNow();
            }
            catch (Exception ex) { Log("欢迎语出错：" + ex.Message); }
        };
        welcome.Start();

        // 台词库坏了要给个明确提示，而不是让她变哑巴
        if (lines.LoadError.Length > 0)
        {
            System.Windows.Forms.Timer warn = new System.Windows.Forms.Timer();
            warn.Interval = 2600;
            warn.Tick += delegate
            {
                warn.Stop(); warn.Dispose();
                pet.Say(Lang.T("app.noLines"), "shy");
            };
            warn.Start();
        }
    }

    public void StartFrameDump(string path) { StartFrameDump(path, 1500); }

    /// <summary>排障：启动一段时间后把当前帧存成 PNG，然后退出（--dump &lt;路径&gt; [--dump-after 毫秒]）</summary>
    public void StartFrameDump(string path, int afterMs)
    {
        System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
        t.Interval = afterMs;
        t.Tick += delegate
        {
            t.Stop();
            t.Dispose();
            bool ok = pet.DumpFrame(path);
            Log("帧转储 " + (ok ? "成功" : "失败") + "：" + path);
            Application.Exit();
        };
        t.Start();
    }

    void InitTray()
    {
        tray = new NotifyIcon();
        tray.Icon = sprite.TrayIcon != null ? sprite.TrayIcon : SystemIcons.Application;
        tray.Text = PetName + " · " + Lang.T("tray.tip");
        tray.Visible = true;
        BuildTrayMenu();
        tray.DoubleClick += delegate { RecallPet(); };
    }

    /// <summary>托盘菜单（单独一层，切语言时可以重建）</summary>
    void BuildTrayMenu()
    {
        ContextMenuStrip cm = new ContextMenuStrip();
        cm.Items.Add(Lang.T("tray.recall"), null, delegate { RecallPet(); });
        cm.Items.Add(Lang.T("tray.hide"), null, delegate { HidePet(); });
        cm.Items.Add(Lang.T("tray.chat"), null, delegate { OpenChat(); });
        cm.Items.Add(Lang.T("tray.topic"), null, delegate { MaybeTimeGreeting(); pet.SayCat("topic"); });
        cm.Items.Add(new ToolStripSeparator());
        cm.Items.Add(Lang.T("tray.settings"), null, delegate { OpenSettings(); });
        cm.Items.Add(Lang.T("tray.quit"), null, delegate { Quit(); });
        ContextMenuStrip old = tray.ContextMenuStrip;
        tray.ContextMenuStrip = cm;
        if (old != null) old.Dispose();
    }

    /* ================= 各种动作 ================= */

    public void OpenChat()
    {
        if (pet != null) pet.HideMenu();
        if (chat == null || chat.IsDisposed) chat = new ChatWindow(this);
        chat.ShowChat();
        MaybeTimeGreeting();
    }

    public void OpenSettings()
    {
        if (settings != null) { settings.Dispose(); settings = null; }
        settings = new SettingsWindow(this);
        settings.Show();
        settings.BringToFront();
    }

    public void OnChatShown()
    {
        if (pet != null) pet.HideMenu();
        MaybeTimeGreeting();
    }

    public void MaybeTimeGreeting()
    {
        if (timeGreeted) return;
        timeGreeted = true;
        pet.SayCat(GreetCat());
    }

    public string GreetCat()
    {
        int h = DateTime.Now.Hour;
        if (h >= 5 && h < 11) return "greet_morning";
        if (h >= 11 && h < 17) return "greet_afternoon";
        if (h >= 17 && h < 23) return "greet_evening";
        return "greet_night";
    }

    public void HidePet()
    {
        if (pet == null) return;
        pet.HidePet();
        if (tray != null)
        {
            try
            {
                tray.BalloonTipTitle = Lang.F("balloon.title", PetName);
                tray.BalloonTipText = Lang.T("balloon.text");
                tray.ShowBalloonTip(2500);
            }
            catch { }
        }
    }

    public void RecallPet()
    {
        if (pet == null) return;
        pet.ShowPet();
        pet.Recall();
    }

    public void Quit()
    {
        try { SaveNow(); } catch { }
        if (tray != null) tray.Visible = false;
        Application.Exit();
    }

    public void ApplySettings()
    {
        pet.PetName = store.GetString("name", lines.DefaultName);
        pet.SizeScale = (float)store.GetDouble("sizeScale", 1.0);
        pet.RoamEnabled = store.GetBool("roam", true);
        pet.LoadFeed(store.GetString("feedDay", DateTime.Now.ToString("yyyy-MM-dd")), store.GetInt("feedCount", 0));
        pet.ApplyLayout();
        if (tray != null) tray.Text = PetName + " · " + Lang.T("tray.tip");
        if (chat != null && chat.Visible) chat.Invalidate();
    }

    public string ChatModeLabel()
    {
        string mode = store.GetString("chatMode", "local");
        if (mode == "ai")
        {
            string key = store.GetSecret("apiKey");
            return string.IsNullOrEmpty(key)
                ? Lang.T("chat.modeAiNoKey")
                : Lang.F("chat.modeAiFmt", store.GetString("apiModel", AiChat.DefaultModel));
        }
        return Lang.T("chat.modeLocal");
    }

    /// <summary>互动反馈是否也走 AI：设置里开着 + 当前是 AI 模式 + 填了 Key</summary>
    public bool AiReactionsOn
    {
        get
        {
            if (!store.GetBool("aiReactions", true)) return false;
            if (store.GetString("chatMode", "local") != "ai") return false;
            return !string.IsNullOrEmpty(store.GetSecret("apiKey"));
        }
    }

    /// <summary>互动（摸头/投喂/动作/挂机…）的 AI 反馈入口，实现见 AiReaction.cs</summary>
    public void AskAiReaction(string cat, string mood)
    {
        AiReaction.Ask(this, cat, mood);
    }

    /// <summary>关心提醒（久坐 / 熬夜）的心跳，由宠物窗口的动画定时器驱动，实现见 Care.cs</summary>
    public void CareTick()
    {
        Care.Tick(this);
    }

    /* ================= AI ================= */

    public void AskAi(List<ChatWindow.Turn> history, string userText, Action<string> ok, Action<string> fail)
    {
        pet.Busy = true;
        string typing = lines.Pick("typing", Vars(), pet.Level);
        pet.ShowThinking(string.IsNullOrEmpty(typing) ? "……" : typing);

        var msgs = new List<KeyValuePair<string, string>>();
        foreach (ChatWindow.Turn t in history)
            msgs.Add(new KeyValuePair<string, string>(t.Me ? "user" : "assistant", t.Text));

        string sys = lines.SystemPrompt(pet.PetName, lines.LevelName(pet.Intimacy), store.GetString("personaExtra", ""), pet.Intimacy)
                     + Memory.PromptBlock(this);
        Log("AI 请求 · " + store.GetString("apiBase", AiChat.DefaultBase) + " · " + msgs.Count + " 条上下文");

        AiChat.Ask(store.GetString("apiBase", AiChat.DefaultBase), store.GetSecret("apiKey"),
            store.GetString("apiModel", AiChat.DefaultModel), sys, msgs,
            delegate (string reply)
            {
                pet.Busy = false;
                // 回答末尾可能夹着 @@记：…@@（她这次学到了一条关于你的事），摘出来存下、并从回答里抹掉
                string clean = Memory.ExtractTag(this, reply);
                if (string.IsNullOrEmpty(clean)) clean = Lang.T("chat.noReply");
                Log("AI 回复 " + clean.Length + " 字");
                if (ok != null) ok(clean);
            },
            delegate (string err)
            {
                pet.Busy = false;
                Log("AI 失败：" + err);
                if (fail != null) fail(err);
            });
    }

    /* ================= 状态 ================= */

    public Dictionary<string, string> Vars()
    {
        Dictionary<string, string> v = new Dictionary<string, string>();
        v["name"] = PetName;
        v["lvName"] = lines.LevelName(pet.Intimacy);
        v["days"] = DaysSinceLastSeen().ToString();
        v["topic"] = Topics[rng.Next(Topics.Length)];
        return v;
    }

    public int DaysSinceLastSeen()
    {
        if (!store.Has("lastSeen")) return 0;
        double t = store.GetDouble("lastSeen", 0);
        if (t <= 0) return 0;
        try
        {
            DateTime last = new DateTime((long)t);
            double d = (DateTime.Now.Date - last.Date).TotalDays;
            return d <= 0 ? 0 : (int)d;
        }
        catch { return 0; }
    }

    public void SaveSoon() { savePending = true; }

    public void SaveNow()
    {
        if (pet != null && store != null)
        {
            store.Set("name", pet.PetName);
            store.Set("intimacy", pet.Intimacy);
            store.Set("pose", pet.Pose);
            store.Set("x", pet.PetX);
            store.Set("y", pet.PetY);
            store.Set("roam", pet.RoamEnabled);
            store.Set("sizeScale", (double)pet.SizeScale);
            store.Set("lastSeen", (double)DateTime.Now.Ticks);
        }
        if (store != null) store.Save();
        savePending = false;
    }

    /* ================= 快捷方式 / 开机自启 ================= */

    /// <summary>首次运行询问：要不要在桌面建快捷方式</summary>
    bool AskShortcutConsent()
    {
        try
        {
            // 挂在她那个置顶窗口下面：弹窗一定在别的东西上面，不会被盖住
            DialogResult r = pet != null
                ? MessageBox.Show(pet, Lang.T("first.shortcutText"), Lang.T("first.shortcutTitle"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1)
                : MessageBox.Show(Lang.T("first.shortcutText"), Lang.T("first.shortcutTitle"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
            return r == DialogResult.Yes;
        }
        catch { return true; }
    }

    /// <summary>快捷方式落在哪（YAYA_SHORTCUT_DIR 是排障/测试用，可改到别的目录）</summary>
    public string ShortcutPath()
    {
        string dir = Environment.GetEnvironmentVariable("YAYA_SHORTCUT_DIR");
        if (string.IsNullOrEmpty(dir)) dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        else Directory.CreateDirectory(dir);
        return Path.Combine(dir, "娅娅桌面宠物.lnk");
    }

    /// <summary>不存在且允许创建就新建；已经存在就重写一遍（刷新图标与目标路径）</summary>
    public bool EnsureShortcut(bool createIfMissing)
    {
        try
        {
            bool exists = File.Exists(ShortcutPath());
            if (!exists && !createIfMissing) return false;
            bool ok = CreateShortcut();
            if (ok) Log(exists ? "已刷新桌面快捷方式（图标/目标路径）" : "已创建桌面快捷方式");
            return ok;
        }
        catch (Exception ex)
        {
            Log("快捷方式处理失败：" + ex.Message);
            return false;
        }
    }

    public bool CreateShortcut()
    {
        try
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null) return false;
            object sh = Activator.CreateInstance(t);
            string link = ShortcutPath();
            object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { link });
            Type sct = sc.GetType();
            sct.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { Application.ExecutablePath });
            sct.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { AppDir });
            // 图标优先用 exe 旁边那个独立的「娅娅.ico」：
            // 图标路径换成一个新文件，Windows 外壳就不会拿这个路径的旧缓存糊弄（exe 内嵌图标容易被缓存成旧样子）
            string icoFile = Path.Combine(AppDir, "娅娅.ico");
            string iconLoc = File.Exists(icoFile) ? icoFile + ",0" : Application.ExecutablePath + ",0";
            sct.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { iconLoc });
            sct.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { "娅娅 · 桌面宠物" });
            sct.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
            Log("已创建桌面快捷方式：" + link);
            return true;
        }
        catch (Exception ex)
        {
            Log("创建快捷方式失败：" + ex);   // 记完整异常（含内部异常），方便排障
            return false;
        }    }

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "YayaDesktopPet";

    public bool IsAutoStart()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
            {
                if (k == null) return false;
                object v = k.GetValue(RunName);
                return v != null && v.ToString().IndexOf(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
        catch { return false; }
    }

    public void ApplyAutoStart(bool on)
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (k == null) return;
                if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
                else if (k.GetValue(RunName) != null) k.DeleteValue(RunName, false);
            }
        }
        catch (Exception ex) { Log("设置开机自启失败：" + ex.Message); }
    }

    /// <summary>自检模式要能在命令行窗口里看到输出（winexe 默认没有控制台）</summary>
    static void AttachParentConsole()
    {
        try
        {
            if (AttachConsole(-1))
            {
                StreamWriter w = new StreamWriter(Console.OpenStandardOutput());
                w.AutoFlush = true;
                Console.SetOut(w);
            }
        }
        catch { }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    static extern bool AttachConsole(int pid);

    /* ================= 自检（--selftest <目录>）================= */
    /// <summary>不点鼠标也能验证：词库完整性、关键词路由、等级、禁词、聊天全链路、三个界面快照</summary>
    public void RunSelfTest(string outDir)
    {
        try { Directory.CreateDirectory(outDir); } catch { }
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("== 娅娅桌面宠物 · 自检 ==");
        sb.AppendLine("时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("DPI 缩放 S=" + S.ToString("0.##") + "，主屏 " + Screen.PrimaryScreen.Bounds.Width + "x" + Screen.PrimaryScreen.Bounds.Height);
        sb.AppendLine("台词库：" + (lines.LoadError.Length > 0 ? "【读取出错】" + lines.LoadError : "OK"));
        int totalLines = 0;
        foreach (KeyValuePair<string, List<object>> kv in lines.Cats) totalLines += kv.Value.Count;
        sb.AppendLine("分类 " + lines.Cats.Count + " 个，台词 " + totalLines + " 条，关键词 " + lines.Keywords.Count + " 条");
        sb.AppendLine("等级：" + lines.Levels.Count + " 级，默认名「" + lines.DefaultName + "」，人格 " + lines.Persona.Length + " 字");
        sb.AppendLine();

        // 1. 关键词路由 + 本地回话
        sb.AppendLine("-- 本地聊天路由（你打字 → 匹配到的分类 → 她怎么回）--");
        string[] samples = new string[] {
            "你好", "在吗", "晚安我要睡了", "早安", "今天好累", "我好难过", "好烦啊",
            "讲个笑话", "唱首歌", "你喜欢什么", "外面下雨了", "加班到现在", "周末去哪玩",
            "我饿了", "你是谁", "谢谢", "你真笨", "好无聊", "有点孤单", "我睡不着",
            "想你了", "打游戏吗", "今天吃了蛋糕", "在干嘛呢", "随便说点什么", "？？？"
        };
        foreach (string s in samples)
        {
            string cat = lines.MatchCategory(Lines.Normalize(s));
            List<string> reply = lines.LocalReply(s, pet.Level, Vars());
            sb.AppendLine("  「" + s + "」 → " + (cat == null ? "(无关键词，走兜底)" : cat) + " → " +
                          string.Join(" / ", reply.ToArray()));
        }
        sb.AppendLine();

        // 2. 等级递进
        sb.AppendLine("-- 好感度等级 --");
        int[] marks = new int[] { 0, 14, 15, 39, 40, 79, 80, 139, 140, 200 };
        foreach (int m in marks)
            sb.AppendLine("  好感度 " + m + " → Lv" + lines.LevelOf(m) + " " + lines.LevelName(m) +
                          "（下一级需要 " + lines.NextLevelNeed(m) + "）");
        sb.AppendLine();

        // 3. 禁词扫描（确保不再是「学习搭子」）
        sb.AppendLine("-- 禁词扫描（不该出现在闲聊宠物的嘴里）--");
        string[] banned = new string[] { "学习", "作业", "考试", "背单词", "刷题", "复习", "错题", "知识点", "打卡", "分数线", "考纲", "模考", "四级", "专转本" };
        int bannedHits = 0;
        foreach (KeyValuePair<string, List<object>> kv in lines.Cats)
        {
            foreach (object o in kv.Value)
            {
                string t = o is string ? (string)o : Json.Str(Json.Get(o, "text"), "");
                if (t == null) continue;
                foreach (string b in banned)
                {
                    if (t.IndexOf(b, StringComparison.Ordinal) >= 0)
                    {
                        bannedHits++;
                        sb.AppendLine("  【命中】" + kv.Key + "：" + t + "（含“" + b + "”）");
                    }
                }
            }
        }
        sb.AppendLine(bannedHits == 0 ? "  干净：0 处命中" : "  共 " + bannedHits + " 处命中");
        sb.AppendLine();

        // 4. 聊天全链路（真的走 输入框 → 发送 → 回话）
        sb.AppendLine("-- 聊天全链路（本地词库模式）--");
        try
        {
            chat = new ChatWindow(this);
            // 故意聊长一点：要出现滚动条，才能暴露「气泡滚了、文字没滚」这类问题
            string[] chatProbes = Lang.IsEn
                ? new string[] { "hey", "i'm tired", "tell me a joke", "what do you like", "it's raining",
                                 "i'm bored", "can't sleep", "thanks", "i'm sad", "play a game",
                                 "what are you doing", "say something funny" }
                : new string[] { "你好呀", "今天加班到现在，好累", "给我讲个笑话", "你喜欢什么", "外面下雨了",
                                 "好无聊", "我睡不着", "谢谢", "我好难过", "打游戏吗",
                                 "你在干嘛呢", "讲个故事" };
            foreach (string s in chatProbes)
            {
                chat.TestSend(s);
                Pump(1500);
            }
            sb.AppendLine("  聊了 " + chatProbes.Length + " 轮，共 " + chat.Transcript.Count + " 条气泡" +
                          (chat.Transcript.Count > 12 ? "（已经超出窗口高度 → 有滚动条）" : ""));
            foreach (ChatWindow.Turn t in chat.Transcript)
                sb.AppendLine("  " + (t.Me ? "我" : PetName) + "：" + t.Text);
        }
        catch (Exception ex)
        {
            sb.AppendLine("  【异常】" + ex.Message);
        }
        sb.AppendLine();

        // 4e. 小游戏（不联网）
        sb.AppendLine("-- 小游戏（猜拳 / 猜数字）--");
        try
        {
            string r = null;
            Game.ResetForTest();
            bool s1 = Game.TryHandle(this, "猜拳", out r) && Game.StateForTest() == "rps";
            sb.AppendLine("  「猜拳」开局：" + (s1 ? "✓" : "✗") + "  → " + (r ?? ""));
            bool s2 = Game.TryHandle(this, "石头", out r) && !string.IsNullOrEmpty(r);
            sb.AppendLine("  出「石头」：" + (s2 ? "✓" : "✗") + "  → " + (r ?? ""));

            Game.ResetForTest();
            bool s3 = Game.TryHandle(this, "猜数字", out r) && Game.StateForTest() == "num";
            sb.AppendLine("  「猜数字」开局：" + (s3 ? "✓" : "✗") + "  → " + (r ?? ""));

            // 二分法一定能猜中 1..100；猜中时她会把对局结束（状态回 idle）
            int lo = 1, hi = 100, used = 0;
            bool won = false;
            string rr = null;
            while (lo <= hi && used < 8)
            {
                int g = (lo + hi) / 2;
                used++;
                Game.TryHandle(this, g.ToString(), out rr);
                if (Game.StateForTest() == "idle") { won = true; break; }
                if (rr == Lang.T("game.numLow")) lo = g + 1; else hi = g - 1;
            }
            sb.AppendLine("  真的玩一局（二分法猜 1..100）：" + (won ? "✓ 用了 " + used + " 次" : "✗ 8 次没猜中"));

            Game.ResetForTest();
            Game.TryHandle(this, "猜数字", out r);
            bool q = Game.TryHandle(this, "不玩了", out r) && Game.StateForTest() == "idle";
            sb.AppendLine("  「不玩了」结束对局：" + (q ? "✓" : "✗"));

            Game.ResetForTest();
            bool pass = !Game.TryHandle(this, "今天天气不错", out r);
            sb.AppendLine("  普通聊天不受影响（原样交回词库/AI）：" + (pass ? "✓" : "✗"));
            sb.AppendLine("  小游戏纯本地，不联网、不调 API");
        }
        catch (Exception ex)
        {
            sb.AppendLine("  【异常】" + ex.Message);
        }
        sb.AppendLine();

        // 4d. 关心提醒（久坐 / 熬夜）
        sb.AppendLine("-- 关心提醒（久坐 / 熬夜）--");
        try
        {
            sb.AppendLine("  阈值：连续用电脑 " + Care.SitMinutes + " 分钟提醒久坐；离开满 " +
                          Care.RestMinutes + " 分钟算休息过（重新计时，允许再提醒）");
            sb.AppendLine("  熬夜时段：" + Care.LateFromHour + ":00 – " + Care.LateToHour + ":00，一晚只念一次");
            bool a = lines.Has("care_sit"), b = lines.Has("care_late");
            sb.AppendLine("  台词分类 care_sit=" + (a ? "有 ✓" : "缺 ✗") + "   care_late=" + (b ? "有 ✓" : "缺 ✗"));
            sb.AppendLine("  判断依据只有系统的「最后输入时间」，不记录按键内容、不装钩子");
        }
        catch (Exception ex)
        {
            sb.AppendLine("  【异常】" + ex.Message);
        }
        sb.AppendLine();

        // 4c. 节日彩蛋（不联网，用固定日期逐项验证）
        sb.AppendLine("-- 节日彩蛋 --");
        try
        {
            string savedBirthday = store.GetString("birthday", "");
            store.Set("birthday", "");       // 避免用户生日干扰这一轮验证
            var probe = new string[][]
            {
                new string[] { "2026-02-17", "feast_spring"     },
                new string[] { "2026-09-25", "feast_midautumn"  },
                new string[] { "2026-01-01", "feast_newyear"    },
                new string[] { "2026-12-31", "feast_newyeareve" },
                new string[] { "2026-02-14", "feast_valentine"  },
                new string[] { "2026-06-01", "feast_children"   },
                new string[] { "2026-10-01", "feast_national"   },
                new string[] { "2026-10-31", "feast_halloween"  },
                new string[] { "2026-12-25", "feast_christmas"  },
                new string[] { "2026-05-20", ""                 },
            };
            int ok = 0, bad = 0;
            foreach (string[] row in probe)
            {
                string[] p = row[0].Split('-');
                DateTime d = new DateTime(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]));
                string k = Feast.KeyFor(this, d);
                bool pass = (row[1].Length == 0) ? (k == null) : (k == row[1]);
                if (pass && k != null && !lines.Has(k)) pass = false;   // 台词库里得有这个分类
                if (pass) ok++; else bad++;
                sb.AppendLine("  " + row[0] + " -> " + (k == null ? "(普通日子)" : k) + (pass ? "  ✓" : "  ✗ 期望 " + row[1]));
            }
            sb.AppendLine("  " + ok + " 项正确 / " + bad + " 项异常");

            // 生日：设一次、查一次、清掉
            store.Set("birthday", "03-15");
            bool hit = Feast.KeyFor(this, new DateTime(2026, 3, 15)) == "feast_birthday";
            store.Set("birthday", savedBirthday);
            sb.AppendLine("  生日识别：" + (hit ? "✓" : "✗") + "（当前存的生日：" +
                          (savedBirthday.Length > 0 ? savedBirthday : "未设置") + "）");
            sb.AppendLine("  春节/中秋是农历，用对照表（当前表到 2030 年）；生日由用户自己告诉她");
        }
        catch (Exception ex)
        {
            sb.AppendLine("  【异常】" + ex.Message);
        }
        sb.AppendLine();

        // 4a. 记忆系统（不联网）。注意：先备份真实记忆，测完还原——自检不该毁掉用户的数据
        sb.AppendLine("-- 记忆系统 --");
        try
        {
            var backup = Memory.All(this);
            int before = backup.Count;

            bool w1 = Memory.Remember(this, "自检临时记忆：草莓蛋糕");
            bool w2 = Memory.Remember(this, "自检临时记忆：草莓蛋糕");     // 第二次应判重
            sb.AppendLine("  写入=" + (w1 ? "成功" : "失败 ✗") + "；重复写入判重=" + (w2 ? "没判出来 ✗" : "正确 ✓"));

            string stripped = Memory.ExtractTag(this, "好啊我知道啦@@记：自检临时记忆：怕黑@@");
            bool tagOk = stripped == "好啊我知道啦" && Memory.All(this).Count == before + 2;
            sb.AppendLine("  从回答里摘出记忆标记 → 正文=\"" + stripped + "\"  " + (tagOk ? "✓" : "✗"));

            string r1;
            bool c1 = Memory.TryCommand(this, "记住：自检临时记忆：不吃香菜", out r1);
            string r2;
            bool c2 = Memory.TryCommand(this, "你记得什么", out r2);
            string r3;
            bool c3 = Memory.TryCommand(this, "清空记忆", out r3);
            sb.AppendLine("  认指令：「记住：…」=" + (c1 ? "✓" : "✗") +
                          "  「你记得什么」=" + (c2 ? "✓" : "✗") +
                          "  「清空记忆」=" + (c3 ? "✓" : "✗"));
            sb.AppendLine("  她会念出来 → " + (r2 ?? "").Replace("\r", "").Replace("\n", " / "));
            sb.AppendLine("  清空后剩 " + Memory.All(this).Count + " 条 " + (Memory.All(this).Count == 0 ? "✓" : "✗"));

            // 还原用户原本的记忆
            foreach (string s in backup) Memory.Remember(this, s);
            sb.AppendLine("  已还原原有记忆 " + Memory.All(this).Count + " 条（自检不破坏用户数据）");
        }
        catch (Exception ex)
        {
            sb.AppendLine("  【异常】" + ex.Message);
        }
        sb.AppendLine();

        // 4b. AI 互动反馈覆盖检查（不联网：只核对分类映射与台词库是否对得上）
        sb.AppendLine("-- AI 互动反馈（接 API 后，这些互动不再只说固定台词）--");
        try
        {
            var covered = AiReaction.CoveredCategories();
            var missing = new List<string>();
            foreach (string c in covered) if (!lines.Has(c)) missing.Add(c);
            sb.AppendLine("  会走 AI 的互动分类 " + covered.Count + " 个");
            if (missing.Count > 0)
                sb.AppendLine("  [错误] 这些分类在台词库里不存在（分类名拼错了？）：" + string.Join(", ", missing.ToArray()));
            else
                sb.AppendLine("  全部 " + covered.Count + " 个分类在台词库里都存在  ✓");
            sb.AppendLine("  冷却 " + (AiReaction.CooldownMs / 1000) + " 秒；本地台词仍然先说，AI 那句是异步追加");
            sb.AppendLine("  " + string.Join("  ", covered.ToArray()));
        }
        catch (Exception ex)
        {
            sb.AppendLine("  【异常】" + ex.Message);
        }
        sb.AppendLine();

        // 5. 界面快照
        sb.AppendLine("-- 界面快照 --");
        try
        {
            pet.DumpFrame(Path.Combine(outDir, "01-pet-frame.png"));
            Snap(chat, Path.Combine(outDir, "02-chat.png"));
            sb.AppendLine("  宠物帧 → 01-pet-frame.png");
            sb.AppendLine("  聊天窗 → 02-chat.png");
        }
        catch (Exception ex) { sb.AppendLine("  快照异常：" + ex.Message); }

        try
        {
            settings = new SettingsWindow(this);
            Snap(settings, Path.Combine(outDir, "03-settings.png"));
            sb.AppendLine("  设置窗 → 03-settings.png");
        }
        catch (Exception ex) { sb.AppendLine("  设置窗异常：" + ex.Message); }

        // 5.6 鼠标规则：单击=摸摸头，双击=菜单，连点=假生气
        sb.AppendLine("-- 鼠标交互规则 --");
        try
        {
            pet.TestSetSleeping(false);
            pet.HideMenu();
            int before = pet.Intimacy;
            pet.TestClick();
            Pump(600);                                    // 等过 320ms 的单击判定
            int afterSingle = pet.Intimacy;
            string catSingle = pet.LastCategory;

            pet.TestClick();                              // 第一下
            Pump(60);
            pet.TestClick();                              // 320ms 内第二下 → 双击
            Pump(150);
            bool menuOpen = pet.MenuVisible;
            if (menuOpen) pet.MenuForTest.DumpFrame(Path.Combine(outDir, "16-menu-dblclick.png"));
            pet.TestClick();                              // 菜单开着时点一下 → 收起
            Pump(120);
            bool menuClosed = !pet.MenuVisible;

            for (int i = 0; i < 4; i++) { pet.TestClick(); Pump(30); }   // 手速党
            Pump(120);

            sb.AppendLine("  单击 → 分类 " + catSingle + "，好感度 " + before + " → " + afterSingle +
                          ((catSingle == "praise_pat" || catSingle == "click") && afterSingle == before + 1 ? "  ✓（摸摸头）" : "  ✗"));
            sb.AppendLine("  双击 → 菜单" + (menuOpen ? "弹出 ✓" : "没弹出 ✗") +
                          "；菜单开着时单击 → " + (menuClosed ? "收起 ✓" : "没收起 ✗"));
            sb.AppendLine("  1.2 秒内点 4 下 → 分类 " + pet.LastCategory +
                          (pet.LastCategory == "click_many" ? "  ✓（假生气）" : "  ✗"));
            pet.HideMenu();
        }
        catch (Exception ex) { sb.AppendLine("  鼠标规则自检异常：" + ex.Message); }
        sb.AppendLine();

        // 5.7 走动：确认她真的挪动了窗口，而不只是做走路动画
        sb.AppendLine("-- 走动 --");
        try
        {
            pet.TestSetSleeping(false);
            pet.HideMenu();
            int x0 = pet.Left;
            int y0 = pet.Top;
            pet.WalkSomewhere(true);
            Pump(1500);
            int x1 = pet.Left;
            int y1 = pet.Top;
            bool moved = Math.Abs(x1 - x0) > 20;
            sb.AppendLine("  窗口位置 (" + x0 + "," + y0 + ") → (" + x1 + "," + y1 + ")，横向移动 " +
                          Math.Abs(x1 - x0) + " 像素" + (moved ? "  ✓" : "  ✗（只动了动画没移动窗口）"));
            sb.AppendLine("  脚下高度不变：" + (y0 == y1 ? "✓" : "✗（" + y0 + " → " + y1 + "）"));
            Pump(2600);
        }
        catch (Exception ex) { sb.AppendLine("  走动自检异常：" + ex.Message); }
        sb.AppendLine();

        // 5.8 双语检查 + 关键词词边界
        sb.AppendLine("-- 双语台词库 / 关键词匹配 --");
        try
        {
            sb.AppendLine("  当前语言：" + Lang.Code + "（" + LinesFileName() + "）");
            sb.AppendLine("  词边界：「this is fine」不该命中 hi → " +
                          (Lines.ContainsWord(Lines.Normalize("this is fine"), "hi") ? "✗ 误命中" : "✓"));
            sb.AppendLine("           「hello there」应命中 hello → " +
                          (Lines.ContainsWord(Lines.Normalize("hello there"), "hello") ? "✓" : "✗"));
            sb.AppendLine("           「good morning!」应命中 good morning → " +
                          (Lines.ContainsWord(Lines.Normalize("good morning!"), "good morning") ? "✓" : "✗"));
            sb.AppendLine("           「你好呀」应命中 你好 → " +
                          (Lines.ContainsWord(Lines.Normalize("你好呀"), "你好") ? "✓" : "✗"));

            string other = Lang.IsEn ? "lines.json" : "lines.en.json";
            Lines other2 = Lines.Load(Path.Combine(ContentDir, other));
            int on = 0;
            foreach (KeyValuePair<string, List<object>> kv in other2.Cats) on += kv.Value.Count;
            sb.AppendLine("  另一门语言 " + other + "：" +
                          (other2.LoadError.Length > 0 ? "【读取失败】" + other2.LoadError : "OK") +
                          "，分类 " + other2.Cats.Count + "，台词 " + on + "，关键词 " + other2.Keywords.Count + " 条");
            string[] probes = Lang.IsEn
                ? new string[] { "你好", "晚安", "今天好累", "我好难过", "讲个笑话", "唱首歌", "你是谁", "谢谢", "好无聊", "打游戏吗" }
                : new string[] { "hello", "good night", "i'm tired", "i'm sad", "tell me a joke", "sing a song", "who are you", "thanks", "i'm bored", "play a game" };
            Dictionary<string, string> v = Vars();
            v["lvName"] = other2.LevelName(pet.Intimacy);
            foreach (string s in probes)
            {
                string cat = other2.MatchCategory(Lines.Normalize(s));
                List<string> rep = other2.LocalReply(s, 4, v);
                sb.AppendLine("    「" + s + "」 → " + (cat == null ? "(兜底)" : cat) + " → " + string.Join(" / ", rep.ToArray()));
            }
        }
        catch (Exception ex) { sb.AppendLine("  双语检查异常：" + ex.Message); }
        sb.AppendLine();

        // 5.9 语言开关：走设置窗里那个下拉（切一次再切回来）
        sb.AppendLine("-- 语言开关（设置窗里的下拉框）--");
        try
        {
            string orig = Lang.Code;
            settings = new SettingsWindow(this);
            int other = orig == "zh" ? 1 : 0;
            settings.TestPickLanguage(other);
            Pump(500);
            string want = other == 1 ? "en" : "zh";
            bool ok1 = Lang.Code == want && store.GetString("lang", "") == want &&
                       lines.DefaultName == (want == "en" ? "Yaya" : "娅娅");
            sb.AppendLine("  选「" + (other == 1 ? "English" : "中文") + "」→ Lang=" + Lang.Code +
                          "，台词库=" + LinesFileName() + "，默认名=" + lines.DefaultName + (ok1 ? "  ✓" : "  ✗"));
            settings = new SettingsWindow(this);
            settings.TestPickLanguage(orig == "zh" ? 0 : 1);
            Pump(500);
            bool ok2 = Lang.Code == orig && store.GetString("lang", "") == orig;
            sb.AppendLine("  选回「" + (orig == "zh" ? "中文" : "English") + "」→ Lang=" + Lang.Code + (ok2 ? "  ✓" : "  ✗"));
            if (settings != null) { settings.Dispose(); settings = null; }
        }
        catch (Exception ex) { sb.AppendLine("  语言开关异常：" + ex.Message); }
        sb.AppendLine();

        // 6. 桌面快捷方式（设了 YAYA_SHORTCUT_DIR 时会写到那个目录，方便在受控环境里验证这段代码）
        bool sc = EnsureShortcut(true);
        sb.AppendLine("-- 桌面快捷方式 --");
        sb.AppendLine("  " + (sc ? "创建成功" : "创建失败（多半是没权限写桌面，日志里有原因）") +
                      "  目标目录：" + (Environment.GetEnvironmentVariable("YAYA_SHORTCUT_DIR") ?? "(桌面)"));
        sb.AppendLine();

        // 5.5 动作 / 姿势 / 睡觉 / 菜单 逐帧出图
        sb.AppendLine("-- 动作帧 --");
        string[] acts = new string[] { "hop", "spin", "wave", "dance", "stretch", "lookback", "sit" };
        foreach (string a in acts)
        {
            try
            {
                pet.DoAction(a);
                Pump(300);
                string f = Path.Combine(outDir, "10-action-" + a + ".png");
                pet.DumpFrame(f);
                Pump(1500);
            }
            catch (Exception ex) { sb.AppendLine("  " + a + " 异常：" + ex.Message); }
        }
        sb.AppendLine("  7 个动作各出一帧 → 10-action-*.png");

        try
        {
            pet.CyclePose();
            Pump(200);
            pet.DumpFrame(Path.Combine(outDir, "11-pose-side.png"));
            pet.CyclePose();
            Pump(200);
            pet.DumpFrame(Path.Combine(outDir, "12-pose-back.png"));
            pet.CyclePose();
            Pump(200);
            sb.AppendLine("  换姿势（正面/侧面/背面）→ 11/12-pose-*.png");
        }
        catch (Exception ex) { sb.AppendLine("  换姿势异常：" + ex.Message); }

        try
        {
            pet.TestSetSleeping(true);
            Pump(400);
            pet.DumpFrame(Path.Combine(outDir, "13-sleeping.png"));
            pet.TestSetSleeping(false);
            Pump(200);
            sb.AppendLine("  睡着（侧面 + Zzz）→ 13-sleeping.png");
        }
        catch (Exception ex) { sb.AppendLine("  睡觉异常：" + ex.Message); }

        try
        {
            int before = pet.Intimacy;
            pet.Feed();
            Pump(400);
            pet.DumpFrame(Path.Combine(outDir, "14-feed.png"));
            sb.AppendLine("  投喂：好感度 " + before + " → " + pet.Intimacy + " → 14-feed.png");
        }
        catch (Exception ex) { sb.AppendLine("  投喂异常：" + ex.Message); }

        try
        {
            pet.OpenMenu();
            Pump(500);
            MenuWindow m = pet.MenuForTest;
            if (m != null && m.DumpFrame(Path.Combine(outDir, "15-menu.png")))
                sb.AppendLine("  菜单（10 项）→ 15-menu.png");
            else sb.AppendLine("  菜单帧输出失败");
            pet.HideMenu();
        }
        catch (Exception ex) { sb.AppendLine("  菜单异常：" + ex.Message); }
        sb.AppendLine();

        string txt = sb.ToString();        try { File.WriteAllText(Path.Combine(outDir, "selftest.txt"), txt, new UTF8Encoding(false)); } catch { }
        Log("自检完成，结果写入 " + Path.Combine(outDir, "selftest.txt"));
        Console.WriteLine(txt);
    }

    void Snap(Form f, string path)
    {
        if (f == null) return;
        try
        {
            f.Show();
            Pump(800);
            using (Bitmap bmp = new Bitmap(f.Width, f.Height))
            {
                f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
            f.Hide();
        }
        catch (Exception ex) { Log("快照失败 " + path + "：" + ex.Message); }
    }

    void Pump(int ms)
    {
        int end = Environment.TickCount + ms;
        while (Environment.TickCount < end)
        {
            Application.DoEvents();
            Thread.Sleep(20);
        }
    }

    /* ================= 日志 ================= */

    public void Log(string msg)
    {
        try
        {
            if (LogFile == null) return;
            if (File.Exists(LogFile) && new FileInfo(LogFile).Length > 256 * 1024)
                File.Delete(LogFile);
            File.AppendAllText(LogFile,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine,
                new UTF8Encoding(false));
        }
        catch { }
    }
}
