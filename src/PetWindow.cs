// 桌面宠物主窗口
//   · 真透明 + 置顶 + 不进任务栏 + 不抢焦点；透明的地方鼠标能点穿
//   · 点她 / 连点 / 双击换姿势 / 拖动 / 鼠标靠近 / 自己走动 / 投喂 / 动作 / 好感度
//   · 挂机分级用「全系统多久没碰键鼠」判断：3 分钟找你 → 5 分钟走开 → 8 分钟打盹 → 12 分钟睡熟
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

class PetWindow : LayeredWindow
{
    public const float BasePetH = 280f;   // 逻辑像素：中等大小的高度

    public App App;

    /* ---------------- 外观 / 设置 ---------------- */
    public string Pose = Sprite.Front;
    public bool Flipped = false;
    public float SizeScale = 1f;
    public bool RoamEnabled = true;

    /* ---------------- 数据 ---------------- */
    public string PetName = "娅娅";
    public int Intimacy = 0;
    public int Level = 1;
    int feedCount = 0;
    string feedDay = "";

    /// <summary>启动时把「今天已经喂了几块蛋糕」读回来</summary>
    public void LoadFeed(string day, int count)
    {
        feedDay = day;
        feedCount = count;
    }

    /* ---------------- 位置（精灵包围盒左上角，屏幕物理坐标） ---------------- */
    public int PetX = 0, PetY = 0;

    /* ---------------- 运行时 ---------------- */
    Timer timer;
    Random rng = new Random();
    MenuWindow menu;
    Timer pendingClick;                 // 单击延迟判定（等 320ms 看是不是双击）

    // 动画量
    float hop = 0f, hopV = 0f;
    float shake = 0f;
    float spin = 0f, spinV = 0f;
    float squashX = 1f, squashY = 1f;
    bool loggedLayout = false;
    string actionKind = null;
    DateTime actionStart = DateTime.MinValue, actionEnd = DateTime.MinValue;
    string actionPoseBackup = null;

    // 走动
    bool walking = false;
    int walkFromX = 0, walkToX = 0;
    DateTime walkStart = DateTime.MinValue;
    int walkDurMs = 2500;

    // 气泡
    string bubbleText = null;
    DateTime bubbleUntil = DateTime.MinValue;
    string bubbleMood = "normal";
    Queue<string> bubbleQueue = new Queue<string>();

    // 特效
    class Fx { public string Kind; public float X, Y, VX, VY, Life, Age, Size; }
    List<Fx> fx = new List<Fx>();

    // 挂机 / 打扰控制
    int idleStage = 0;
    bool sawActivity = false;      // 启动时如果电脑本来就闲置很久，先别急着让她睡着
    DateTime lastChatter = DateTime.Now;
    DateTime nextChatterAt = DateTime.Now.AddSeconds(20);
    DateTime lastNotice = DateTime.MinValue;
    DateTime nextRoamAt = DateTime.Now.AddSeconds(20);
    DateTime lastHeartbeat = DateTime.MinValue;
    public bool Busy = false;          // 聊天/AI 思考中 → 不打扰

    // 鼠标
    bool dragging = false;
    Point dragStartScreen;
    Point dragWinStart;
    bool dragMoved = false;
    bool mouseDown = false;

    // 点击
    List<DateTime> clickTimes = new List<DateTime>();
    DateTime lastCountedClick = DateTime.MinValue;

    public PetWindow(App app)
    {
        App = app;
        Text = "娅娅";
        ShowInTaskbar = false;
        Diag = delegate (string msg) { App.Log("PetWindow " + msg); };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyLayout();
        timer = new Timer();
        timer.Interval = 40;
        timer.Tick += delegate { Tick(); };
        timer.Start();
    }

    /* ================= 布局 ================= */

    public int PetPixelH { get { return (int)Math.Round(BasePetH * SizeScale * App.S); } }
    public int PetPixelW
    {
        get
        {
            Bitmap b = App.sprite.Get(Pose);
            return (int)Math.Round(PetPixelH * (float)b.Width / b.Height);
        }
    }

    // 头顶留给气泡的高度：她调大时气泡跟着抬高，免得贴脸
    int TopPad
    {
        get
        {
            int fixedPad = (int)Math.Round(120 * App.S);
            int byPet = (int)Math.Round(PetPixelH * 0.42);
            return Math.Max(fixedPad, byPet);
        }
    }
    int BottomPad { get { return (int)Math.Round(14 * App.S); } }
    int WindowW { get { return Math.Max((int)Math.Round(360 * App.S), PetPixelW + (int)Math.Round(40 * App.S)); } }
    int WindowH { get { return PetPixelH + TopPad + BottomPad; } }

    /// <summary>她「脚下」窗内坐标 → 重新摆好窗口</summary>
    public void ApplyLayout()
    {
        SuspendLayout();
        Size = new Size(WindowW, WindowH);
        ClampPet();
        Location = new Point(PetX - (WindowW - PetPixelW) / 2, PetY - TopPad);
        ResumeLayout(false);
        Redraw();
    }

    void ClampPet()
    {
        Rectangle vs = SystemInformation.VirtualScreen;
        int pW = PetPixelW, pH = PetPixelH;
        int minX = vs.Left - (int)(pW * 0.35f);
        int maxX = vs.Right - (int)(pW * 0.65f);
        // 纵向：保证她整个人都在屏幕里（头顶留气泡的区域可以探出屏幕外，但身体不被裁）
        int minY = vs.Top;
        int maxY = vs.Bottom - pH;
        if (maxY < minY) maxY = minY;          // 屏幕比她还矮时的兜底
        if (PetX < minX) PetX = minX;
        if (PetX > maxX) PetX = maxX;
        if (PetY < minY) PetY = minY;
        if (PetY > maxY) PetY = maxY;
    }

    public void MovePetTo(int petX, int petY)
    {
        PetX = petX; PetY = petY;
        ClampPet();
        MoveTo(PetX - (WindowW - PetPixelW) / 2, PetY - TopPad);
    }

    /// <summary>把窗口位置同步回 PetX / PetY（拖动后）</summary>
    void SyncPetFromWindow()
    {
        PetX = Left + (WindowW - PetPixelW) / 2;
        PetY = Top + TopPad;
    }

    /* ================= 说 话 ================= */

    public void Say(string text)
    {
        Say(text, "normal");
    }

    public void Say(string text, string mood)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (bubbleText != null && DateTime.Now < bubbleUntil.AddMilliseconds(-500))
        {
            if (bubbleQueue.Count < 4) bubbleQueue.Enqueue(text + "\u0001" + mood);
            return;
        }
        bubbleText = text;
        bubbleMood = mood;
        int ms = Math.Max(2800, (int)(text.Length * 190));
        if (ms > 9000) ms = 9000;
        bubbleUntil = DateTime.Now.AddMilliseconds(ms);
    }

    public void SayCat(string cat)
    {
        SayCat(cat, "normal");
    }

    public void SayCat(string cat, string mood)
    {
        string line = App.lines.Pick(cat, App.Vars(), Level);
        if (line != null)
        {
            LastCategory = cat;                 // 自检用：最近一次说了哪一类
            Say(line, mood);
        }
        // AI 模式：本地台词先保证「点下去立刻有反应」，再异步让模型现场补一句。
        // 具体哪些分类会走 AI、冷却多久，都在 AiReaction.cs 里。
        if (App != null) App.AskAiReaction(cat, mood);
    }

    /// <summary>自检用：最近一次触发的台词分类</summary>
    public string LastCategory = "";

    public void ShowThinking(string text)
    {
        bubbleText = text;
        bubbleMood = "think";
        bubbleUntil = DateTime.Now.AddSeconds(30);   // 有结果时会被覆盖
    }

    public void ClearBubble()
    {
        bubbleText = null;
        bubbleUntil = DateTime.MinValue;
    }

    /* ================= 好感度 ================= */

    public void AddIntimacy(int n)
    {
        int oldLevel = App.lines.LevelOf(Intimacy);
        Intimacy += n;
        if (Intimacy < 0) Intimacy = 0;
        Level = App.lines.LevelOf(Intimacy);
        if (Level > oldLevel)
        {
            AddFx("sparkle", 0, 0, 3);
            SayCat("level_up", "happy");
        }
        App.SaveSoon();
    }

    /* ================= 动作 ================= */

    public void DoAction(string kind)
    {
        float S = App.S;
        actionKind = kind;
        actionStart = DateTime.Now;
        actionPoseBackup = Pose;
        switch (kind)
        {
            case "hop": actionEnd = actionStart.AddMilliseconds(900); hopV = 8.5f * S; break;
            case "spin": actionEnd = actionStart.AddMilliseconds(1000); spinV = 900f; break;
            case "wave": actionEnd = actionStart.AddMilliseconds(1100); shake = 7f * S; AddFx("heart", 0, -0.5f, 1); break;
            case "dance": actionEnd = actionStart.AddMilliseconds(1900); AddFx("sparkle", 0, 0, 2); break;
            case "stretch": actionEnd = actionStart.AddMilliseconds(1300); break;
            case "lookback": actionEnd = actionStart.AddMilliseconds(1800); Pose = Sprite.Back; break;
            case "sit": actionEnd = actionStart.AddMilliseconds(2000); Pose = Sprite.Side; break;
            default: actionEnd = actionStart.AddMilliseconds(900); break;
        }
        SayCat("action_" + kind, kind == "dance" ? "happy" : "normal");
    }

    public void RandomAction()
    {
        string[] ids = new string[] { "hop", "spin", "wave", "dance", "stretch", "lookback", "sit" };
        DoAction(ids[rng.Next(ids.Length)]);
    }

    public void CyclePose()
    {
        Pose = Sprite.NextPose(Pose);
        App.store.Set("pose", Pose);
        App.SaveSoon();
        SayCat("pose_" + Pose);
    }

    /* ================= 投喂 ================= */

    public void Feed()
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        if (feedDay != today) { feedDay = today; feedCount = 0; }
        if (feedCount >= 3)
        {
            SayCat("feed_full", "shy");
            return;
        }
        feedCount++;
        App.store.Set("feedDay", feedDay);
        App.store.Set("feedCount", feedCount);
        AddFx("cake", 0, -0.9f, 1);
        AddFx("heart", 0.2f, -0.3f, 2);
        hopV = 5f * App.S;
        AddIntimacy(3);
        SayCat("feed", "happy");
        App.SaveSoon();
    }

    /* ================= 走动 ================= */

    public void WalkSomewhere(bool quiet)
    {
        if (walking || dragging) return;
        Rectangle wa = Screen.PrimaryScreen.WorkingArea;
        int pW = PetPixelW;
        int min = wa.Left + (int)(20 * App.S);
        int max = wa.Right - pW - (int)(20 * App.S);
        if (max <= min) return;
        int target = min + rng.Next(max - min);
        if (Math.Abs(target - PetX) < (int)(110 * App.S))
            target = (target + (max - min) / 2) % Math.Max(1, max - min) + min;

        walking = true;
        walkFromX = PetX;
        walkToX = target;
        walkStart = DateTime.Now;
        walkDurMs = 1800 + rng.Next(1800);
        Flipped = walkToX < walkFromX;
        if (!quiet && rng.NextDouble() < 0.6) SayCat("walk_start");
    }

    void StopWalking(bool arrived)
    {
        if (!walking) return;
        walking = false;
        if (arrived)
        {
            App.store.Set("x", PetX);
            App.store.Set("y", PetY);
            App.SaveSoon();
            if (rng.NextDouble() < 0.65) SayCat("walk_arrive");
        }
        if (actionKind == null) Pose = Sprite.Front;
    }

    /* ================= 特效 ================= */

    void AddFx(string kind, float ox, float oy, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var f = new Fx();
            f.Kind = kind;
            f.X = ox + (float)(rng.NextDouble() - 0.5) * 0.5f;
            f.Y = oy + (float)(rng.NextDouble() - 0.5) * 0.3f;
            f.VX = (float)(rng.NextDouble() - 0.5) * 30f;
            f.VY = -30f - (float)rng.NextDouble() * 40f;
            f.Life = 1.1f + (float)rng.NextDouble() * 0.7f;
            f.Size = 0.7f + (float)rng.NextDouble() * 0.6f;
            f.Age = 0f;
            fx.Add(f);
        }
        if (fx.Count > 40) fx.RemoveRange(0, fx.Count - 40);
    }

    /* ================= 主循环 ================= */

    void Tick()
    {
        DateTime now = DateTime.Now;
        float dt = 0.04f;
        float S = App.S;

        // 跳跃物理
        if (hopV != 0f || hop > 0f)
        {
            hopV -= 34f * S * dt;
            hop += hopV * dt;
            if (hop <= 0f) { hop = 0f; hopV = 0f; }
        }

        // 摇晃 / 旋转衰减
        if (shake != 0f)
        {
            shake *= 0.90f;
            if (Math.Abs(shake) < 0.3f) shake = 0f;
        }
        if (spinV != 0f)
        {
            spin += spinV * dt;
            spinV *= 0.94f;
            if (Math.Abs(spinV) < 12f) { spinV = 0f; spin = 0f; }
        }

        // 动作进度
        if (actionKind != null)
        {
            float p = (float)(now - actionStart).TotalMilliseconds / Math.Max(1f, (float)(actionEnd - actionStart).TotalMilliseconds);
            if (p >= 1f)
            {
                if ((actionKind == "lookback" || actionKind == "sit") && actionPoseBackup != null) Pose = actionPoseBackup;
                actionKind = null;
                squashX = 1f; squashY = 1f;
            }
            else ApplyActionPose(actionKind, p);
        }
        else
        {
            squashX += (1f - squashX) * 0.25f;
            squashY += (1f - squashY) * 0.25f;
        }

        // 走动
        if (walking)
        {
            float p = (float)(now - walkStart).TotalMilliseconds / walkDurMs;
            if (p >= 1f) { PetX = walkToX; StopWalking(true); }
            else
            {
                float e = p < 0.5f ? 2f * p * p : 1f - (float)Math.Pow(-2f * p + 2f, 2) / 2f;
                PetX = walkFromX + (int)Math.Round((walkToX - walkFromX) * e);
                Pose = Sprite.Side;
                // 关键：把窗口真的挪过去。只改 PetX 的话她只在原地做走路动画（曾经就是这个 bug）
                Location = new Point(PetX - (WindowW - PetPixelW) / 2, PetY - TopPad);
            }
        }

        // 特效
        for (int i = fx.Count - 1; i >= 0; i--)
        {
            Fx f = fx[i];
            f.Age += dt;
            f.X += f.VX * dt / 100f;
            f.Y += f.VY * dt / 100f;
            f.VY *= 0.985f;
            if (f.Age >= f.Life) fx.RemoveAt(i);
        }

        // 挂机分级（启动后先等你真正碰过一次键鼠，再开始计时）
        int idle = LayeredWindow.SystemIdleMs();
        if (!sawActivity && idle < 60000) sawActivity = true;
        int stage = idle < 180000 ? 0 : idle < 300000 ? 3 : idle < 480000 ? 5 : idle < 720000 ? 8 : 12;
        if (!sawActivity) stage = 0;
        if (stage != idleStage) OnIdleStageChanged(idleStage, stage);
        idleStage = stage;

        // 久坐 / 熬夜提醒
        App.CareTick();

        // 她自己的嘀咕（你不理她、但你在用电脑的时候）——自检时也跳过，理由同上
        if (stage == 0 && !Busy && !App.headless && !App.ChatVisible && !walking && !MenuVisible && now >= nextChatterAt)
        {
            SayCat(rng.NextDouble() < 0.25 ? "bored" : "idle");
            nextChatterAt = now.AddSeconds(100 + rng.Next(160));
        }

        // 自动走动
        if (RoamEnabled && stage == 0 && !Busy && !App.ChatVisible && !walking && !dragging && !MenuVisible && now >= nextRoamAt)
        {
            WalkSomewhere(false);
            nextRoamAt = now.AddSeconds(15 + rng.Next(16));
        }

        // 光标靠近（自检时跳过：那时鼠标停哪儿全看你的手，会让自检结果飘）
        if (stage == 0 && !Busy && !App.headless && (now - lastNotice).TotalSeconds > 90)
        {
            Point c = Cursor.Position;
            float cx = PetX + PetPixelW / 2f, cy = PetY + PetPixelH / 2f;
            double dist = Math.Sqrt((c.X - cx) * (c.X - cx) + (c.Y - cy) * (c.Y - cy));
            if (dist < 95 * S)
            {
                lastNotice = now;
                hopV = 4.5f * S;
                SayCat("notice_cursor", "shy");
            }
        }

        // 气泡队列
        if (bubbleText != null && now >= bubbleUntil)
        {
            bubbleText = null;
        }
        if (bubbleText == null && bubbleQueue.Count > 0)
        {
            string raw = bubbleQueue.Dequeue();
            int bar = raw.IndexOf('\u0001');
            if (bar >= 0) Say(raw.Substring(0, bar), raw.Substring(bar + 1));
            else Say(raw);
        }

        // 菜单自动收起：改成「点她一下 / 选中一项」才收，不再靠鼠标移开

        // 排障心跳：窗口位置 vs 内部坐标，看看是谁在挪她
        if (App.Debug && (now - lastHeartbeat).TotalSeconds >= 5)
        {
            lastHeartbeat = now;
            App.Log("心跳 窗口=(" + Left + "," + Top + ") 尺寸=" + Width + "x" + Height +
                    " PetX=" + PetX + " PetY=" + PetY + " TopPad=" + TopPad +
                    " 走动=" + walking + " 拖动=" + dragging + " 帧位=" + Location.X + "," + Location.Y);
        }

        Redraw();
    }

    void ApplyActionPose(string kind, float p)
    {
        float S = App.S;
        float s = (float)Math.Sin(p * Math.PI);
        if (kind == "stretch") { squashY = 1f + 0.13f * s; squashX = 1f - 0.07f * s; }
        else if (kind == "dance") { squashX = 1f + 0.09f * (float)Math.Sin(p * Math.PI * 4); squashY = 1f - 0.07f * (float)Math.Sin(p * Math.PI * 4); shake = 3f * S * (float)Math.Sin(p * Math.PI * 6); }
        else if (kind == "hop") { if (hop == 0f && p < 0.1f) hopV = 6f * S; }
        else if (kind == "sit") { squashY = 1f - 0.14f * s; }
        else if (kind == "lookback") { squashX = 1f - 0.05f * s; }
        else if (kind == "wave") { shake = 4f * S * (float)Math.Sin(p * Math.PI * 8); }
        else if (kind == "spin") { squashX = Math.Max(0.15f, Math.Abs((float)Math.Cos(p * Math.PI * 2))); }
    }

    void OnIdleStageChanged(int from, int to)
    {
        if (to > from)
        {
            if (to == 3) SayCat("idle_3min");
            else if (to == 5) { SayCat("idle_5min"); if (RoamEnabled) WalkSomewhere(true); }
            else if (to == 8) { SayCat("idle_8min", "sleepy"); Pose = Sprite.Side; }
            else if (to == 12) SayCat("idle_12min", "sleepy");
        }
        else if (to == 0 && from >= 8)
        {
            Pose = Sprite.Front;
            SayCat("wake_up", "shy");
            hopV = 3.5f * App.S;
        }
        else if (to == 0 && from > 0)
        {
            Pose = Sprite.Front;
        }
    }

    /* ================= 绘制 ================= */

    protected override void Render(Graphics g)
    {
        float S = App.S;
        bool sleeping = idleStage >= 8;
        Bitmap bmp = App.sprite.Get(Pose);

        int pH = PetPixelH;
        int pW = PetPixelW;
        float cx = Width / 2f;
        float spriteTop = TopPad;
        float spriteLeft = cx - pW / 2f;

        // 走动时的上下颠簸
        float bob = 0f;
        if (walking)
        {
            float p = (float)(DateTime.Now - walkStart).TotalMilliseconds / walkDurMs;
            bob = -(float)Math.Abs(Math.Sin(p * 14)) * 6f * S;
        }
        float breathe = sleeping ? (float)Math.Sin(DateTime.Now.TimeOfDay.TotalSeconds * 1.6) * 2.2f * S
                                 : (float)Math.Sin(DateTime.Now.TimeOfDay.TotalSeconds * 1.1) * 1.2f * S;

        float dx = shake + (Pose == Sprite.Side && walking ? 0f : 0f);

        // 影子
        using (var sh = new SolidBrush(Color.FromArgb(sleeping ? 40 : 70, 0, 0, 0)))
        {
            float sw = pW * 0.52f;
            g.FillEllipse(sh, cx - sw / 2f, Height - BottomPad - 6f * S, sw, 10f * S);
        }

        // 她本人
        GraphicsState st = g.Save();
        g.TranslateTransform(cx, spriteTop + pH / 2f);
        g.RotateTransform(spin);
        g.ScaleTransform(squashX, squashY);
        g.TranslateTransform(-cx, -(spriteTop + pH / 2f));

        var dest = new RectangleF(spriteLeft + dx, spriteTop + hop + bob + breathe, pW, pH);
        if (!loggedLayout)
        {
            loggedLayout = true;
            System.Drawing.Drawing2D.Matrix m = g.Transform;
            App.Log("布局 S=" + App.S + " sizeScale=" + SizeScale + " pH=" + pH + " pW=" + pW +
                    " win=" + Width + "x" + Height + " dest=" + dest.X.ToString("0.#") + "," + dest.Y.ToString("0.#") +
                    "," + dest.Width + "," + dest.Height);
            App.Log("渲染诊断 位图=" + bmp.Width + "x" + bmp.Height + " pageUnit=" + g.PageUnit +
                    " pageScale=" + g.PageScale + " dpiX=" + g.DpiX +
                    " 变换=[" + m.Elements[0].ToString("0.###") + "," + m.Elements[1].ToString("0.###") + "," +
                    m.Elements[2].ToString("0.###") + "," + m.Elements[3].ToString("0.###") + "," +
                    m.Elements[4].ToString("0.###") + "," + m.Elements[5].ToString("0.###") + "]");
        }
        Sprite.Draw(g, bmp, dest, Flipped, sleeping ? 0.96f : 1f);
        g.Restore(st);

        // 特效
        foreach (Fx f in fx)
        {
            float a = 1f - f.Age / f.Life;
            if (a < 0f) a = 0f;
            float x = cx + f.X * pW;
            float y = spriteTop + pH * 0.45f + f.Y * pH;
            int alpha = (int)(a * 235);
            if (f.Kind == "heart")
            {
                using (var b = new SolidBrush(Color.FromArgb(alpha, 255, 120, 160)))
                    DrawHeart(g, b, x, y, 18f * S * f.Size);
            }
            else if (f.Kind == "sparkle")
            {
                using (var b = new SolidBrush(Color.FromArgb(alpha, 255, 232, 140)))
                    DrawSparkle(g, b, x, y, 14f * S * f.Size);
            }
            else if (f.Kind == "cake")
            {
                DrawCake(g, x, y, 26f * S, alpha);
            }
        }

        // 睡觉的 Zzz
        if (sleeping)
        {
            float t = (float)(DateTime.Now.TimeOfDay.TotalSeconds);
            for (int i = 0; i < 3; i++)
            {
                float ph = (t * 0.5f + i * 0.33f) % 1f;
                int al = (int)((1f - ph) * 190);
                float zx = cx + pW * 0.40f + ph * 26f * S;
                float zy = spriteTop + pH * 0.06f - ph * 40f * S;
                using (var f2 = new Font("Microsoft YaHei UI", (7f + ph * 5f) * S, FontStyle.Bold, GraphicsUnit.Point))
                using (var b2 = new SolidBrush(Color.FromArgb(al, 90, 90, 110)))
                    g.DrawString("Z", f2, b2, zx, zy);
            }
        }

        // 气泡
        if (bubbleText != null)
        {
            if (bubbleMood == "think") DrawBubble(g, ThinkingText(), cx, spriteTop - 8f * S, "think");
            else DrawBubble(g, bubbleText, cx, spriteTop - 8f * S, bubbleMood);
        }
    }

    string ThinkingText()
    {
        int dots = (int)(DateTime.Now.TimeOfDay.TotalSeconds * 2) % 4;
        return "正在想" + new string('…', Math.Max(1, dots));
    }

    void DrawBubble(Graphics g, string text, float centerX, float bottomY, string mood)
    {
        float S = App.S;
        float maxW = 300f * S;
        // 注意：TextRenderer / GDI 自己会按屏幕 DPI 放大字号，这里不能再乘 S（乘了就双重放大）
        using (var font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point))
        {
            Size sz = TextRenderer.MeasureText(text, font, new Size((int)maxW, 10000),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl);
            float pad = 10f * S;
            float w = Math.Min(maxW, sz.Width) + pad * 2f;
            float h = sz.Height + pad * 2f;
            float x = centerX - w / 2f;
            float y = bottomY - h;
            if (x < 4f * S) x = 4f * S;
            if (x + w > Width - 4f * S) x = Width - 4f * S - w;
            if (y < 2f * S) y = 2f * S;

            Color fill, border;
            if (mood == "shy") { fill = Color.FromArgb(248, 255, 240, 246); border = Color.FromArgb(235, 150, 185); }
            else if (mood == "happy") { fill = Color.FromArgb(248, 255, 250, 235); border = Color.FromArgb(240, 200, 120); }
            else if (mood == "think") { fill = Color.FromArgb(240, 245, 245, 250); border = Color.FromArgb(180, 190, 210); }
            else { fill = Color.FromArgb(248, 255, 255, 255); border = Color.FromArgb(225, 170, 195); }

            var rect = new RectangleF(x, y, w, h);
            using (var path = RoundedRect(rect, 12f * S))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                using (var p = new Pen(border, 1.6f * S)) g.DrawPath(p, path);
            }
            // 小尖角
            using (var tri = new GraphicsPath())
            {
                float tx = centerX;
                tri.AddPolygon(new PointF[] {
                    new PointF(tx - 7f * S, y + h - 1f),
                    new PointF(tx + 7f * S, y + h - 1f),
                    new PointF(tx, y + h + 9f * S)
                });
                using (var b = new SolidBrush(fill)) g.FillPath(b, tri);
                using (var p = new Pen(border, 1.6f * S))
                {
                    g.DrawLine(p, tx - 7f * S, y + h - 0.5f, tx, y + h + 9f * S);
                    g.DrawLine(p, tx + 7f * S, y + h - 0.5f, tx, y + h + 9f * S);
                }
            }
            using (var tb = new SolidBrush(Color.FromArgb(255, 58, 42, 50)))
            {
                var tr = new Rectangle((int)(x + pad), (int)(y + pad), (int)(w - pad * 2f), (int)(h - pad * 2f));
                TextRenderer.DrawText(g, text, font, tr, Color.FromArgb(255, 58, 42, 50),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl);
            }
        }
    }

    static void DrawHeart(Graphics g, Brush b, float cx, float cy, float s)
    {
        float half = s / 2f;
        using (var p = new GraphicsPath())
        {
            p.AddBezier(cx, cy + half * 0.95f, cx - half * 1.45f, cy - half * 0.15f, cx - half * 0.55f, cy - half * 1.15f, cx, cy - half * 0.35f);
            p.AddBezier(cx, cy - half * 0.35f, cx + half * 0.55f, cy - half * 1.15f, cx + half * 1.45f, cy - half * 0.15f, cx, cy + half * 0.95f);
            p.CloseFigure();
            g.FillPath(b, p);
        }
    }

    static void DrawSparkle(Graphics g, Brush b, float cx, float cy, float s)
    {
        float r = s / 2f, t = s / 7f;
        var pts = new PointF[] {
            new PointF(cx, cy - r), new PointF(cx + t, cy - t), new PointF(cx + r, cy),
            new PointF(cx + t, cy + t), new PointF(cx, cy + r), new PointF(cx - t, cy + t),
            new PointF(cx - r, cy), new PointF(cx - t, cy - t)
        };
        g.FillPolygon(b, pts);
    }

    static void DrawCake(Graphics g, float cx, float cy, float s, int alpha)
    {
        float w = s, h = s * 0.72f;
        var body = new RectangleF(cx - w / 2f, cy - h / 2f, w, h);
        using (var b = new SolidBrush(Color.FromArgb(alpha, 255, 240, 210)))
        using (var path = RoundedRect(body, s * 0.12f))
            g.FillPath(b, path);
        using (var b = new SolidBrush(Color.FromArgb(alpha, 250, 205, 225)))
            g.FillRectangle(b, body.X, body.Y + h * 0.45f, w, h * 0.32f);
        using (var b = new SolidBrush(Color.FromArgb(alpha, 230, 70, 90)))
            g.FillEllipse(b, cx - s * 0.13f, body.Y - s * 0.17f, s * 0.26f, s * 0.26f);
        using (var p = new Pen(Color.FromArgb(alpha / 2, 200, 150, 170), 1.2f))
            g.DrawPath(p, RoundedRect(body, s * 0.12f));
    }

    /* ================= 自检用的接口 ================= */

    /// <summary>自检：强行让她进入/退出「睡着」状态</summary>
    public void TestSetSleeping(bool on)
    {
        idleStage = on ? 8 : 0;
        sawActivity = !on;
        Pose = on ? Sprite.Side : Sprite.Front;
    }

    public MenuWindow MenuForTest { get { return menu; } }

    /// <summary>自检：模拟一次鼠标点击（走真实判定逻辑）</summary>
    public void TestClick() { HandleClick(); }

    /* ================= 鼠标 ================= */

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            mouseDown = true;
            dragMoved = false;
            dragging = false;
            dragStartScreen = Cursor.Position;
            dragWinStart = Location;
            // 左键按下时先不动菜单：交给 HandleClick 决定是「收起菜单」还是「摸摸头」
        }
        else if (e.Button == MouseButtons.Right)
        {
            OpenMenu();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (mouseDown)
        {
            Point cur = Cursor.Position;
            int dx = cur.X - dragStartScreen.X, dy = cur.Y - dragStartScreen.Y;
            if (!dragging && (Math.Abs(dx) > 4 || Math.Abs(dy) > 4))
            {
                dragging = true;
                HideMenu();                    // 开始拖她的时候把菜单收掉，别留在原地
                StopWalking(false);
                if (actionKind != null) { actionKind = null; squashX = 1f; squashY = 1f; }
            }
            if (dragging)
            {
                dragMoved = true;
                MoveTo(dragWinStart.X + dx, dragWinStart.Y + dy);
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        mouseDown = false;
        if (dragging)
        {
            dragging = false;
            SyncPetFromWindow();
            ClampPet();
            ApplyLayout();
            App.store.Set("x", PetX);
            App.store.Set("y", PetY);
            App.SaveSoon();
            if (dragMoved && rng.NextDouble() < 0.35) SayCat("walk_arrive");
            return;
        }
        HandleClick();
    }

    /// <summary>单击 = 摸摸头，双击 = 召唤菜单，1.2 秒内点 4 下以上 = 假生气</summary>
    void HandleClick()
    {
        DateTime now = DateTime.Now;

        clickTimes.Add(now);
        for (int i = clickTimes.Count - 1; i >= 0; i--)
            if ((now - clickTimes[i]).TotalMilliseconds > 1200) clickTimes.RemoveAt(i);

        // 手速党：1.2 秒点了 4 下以上 → 假生气（顺手把菜单收掉）
        if (clickTimes.Count >= 4)
        {
            clickTimes.Clear();
            CancelPendingPat();
            if (MenuVisible) HideMenu();
            shake = 11f * App.S;
            hopV = 3f * App.S;
            SayCat("click_many", "shy");
            return;
        }

        // 菜单开着的时候点她 = 把菜单收起来
        if (MenuVisible) { HideMenu(); return; }

        // 320ms 内又点了一下 = 双击 → 召唤菜单（这一下不算摸摸头）
        if (pendingClick != null && pendingClick.Enabled)
        {
            CancelPendingPat();
            OpenMenu();
            return;
        }

        // 单击先挂起 320ms：确认后面没有第二下，才真的摸摸头
        if (pendingClick == null)
        {
            pendingClick = new Timer();
            pendingClick.Interval = 320;
            pendingClick.Tick += delegate
            {
                pendingClick.Stop();
                App.MaybeTimeGreeting();      // 本次运行第一次互动：先按时间问候一句
                Pat();
            };
        }
        pendingClick.Stop();
        pendingClick.Start();
    }

    void CancelPendingPat()
    {
        if (pendingClick != null) pendingClick.Stop();
    }

    /// <summary>摸摸头：蹦一下 + 飘爱心 + 好感度（2.5 秒内不重复计数）</summary>
    public void Pat()
    {
        hopV = 5f * App.S;
        AddFx("heart", 0f, -0.35f, 2);
        DateTime now = DateTime.Now;
        if ((now - lastCountedClick).TotalSeconds >= 2.5)
        {
            lastCountedClick = now;
            AddIntimacy(1);
        }
        SayCat(rng.NextDouble() < 0.35 ? "click" : "praise_pat", "happy");
    }

    /* ================= 菜单 ================= */

    public bool MenuVisible { get { return menu != null && menu.Visible; } }

    public void OpenMenu()
    {
        if (menu == null)
        {
            menu = new MenuWindow(App);
            menu.ItemChosen += delegate (string id) { HideMenu(); HandleMenu(id); };
        }
        // 菜单开着的时候让她站定，否则她走到哪菜单就落在原地，看着像「乱跑」
        StopWalking(false);
        List<MenuWindow.Item> items = new List<MenuWindow.Item>();
        items.Add(new MenuWindow.Item("pat", Lang.T("menu.pat")));
        items.Add(new MenuWindow.Item("feed", Lang.T("menu.feed")));
        items.Add(new MenuWindow.Item("chat", Lang.T("menu.chat")));
        items.Add(new MenuWindow.Item("topic", Lang.T("menu.topic")));
        items.Add(new MenuWindow.Item("playRps", Lang.T("menu.playRps")));
        items.Add(new MenuWindow.Item("playNum", Lang.T("menu.playNum")));
        items.Add(new MenuWindow.Item("action", Lang.T("menu.action")));
        items.Add(new MenuWindow.Item("walk", Lang.T("menu.walk")));
        items.Add(new MenuWindow.Item("roam", RoamEnabled ? Lang.T("menu.roamOn") : Lang.T("menu.roamOff")));
        items.Add(new MenuWindow.Item("pose", Lang.F("menu.poseFmt", Lang.T("pose." + Pose))));
        items.Add(new MenuWindow.Item("settings", Lang.T("menu.settings")));
        items.Add(new MenuWindow.Item("hide", Lang.T("menu.hide")));

        float S = App.S;
        int w = (int)(148 * S);
        int h = (int)(items.Count * 30 * S + 16 * S);
        Rectangle wa = Screen.FromPoint(new Point(PetX + PetPixelW / 2, PetY + PetPixelH / 2)).WorkingArea;
        int x = PetX + PetPixelW + (int)(10 * S);
        if (x + w > wa.Right) x = PetX - w - (int)(10 * S);
        if (x < wa.Left) x = wa.Left + (int)(6 * S);
        int y = PetY + TopPad + (int)(20 * S);
        if (y + h > wa.Bottom) y = wa.Bottom - h - (int)(6 * S);
        if (y < wa.Top) y = wa.Top + (int)(6 * S);

        menu.ShowItems(items, x, y, w, h);
    }

    public void HideMenu()
    {
        if (menu != null && menu.Visible) menu.Hide();
    }

    /// <summary>菜单「陪我猜拳 / 陪我猜数字」：打开聊天窗并直接开局</summary>
    void StartGame(bool rps)
    {
        App.OpenChat();
        string reply = Game.Start(rps);
        if (App.chat != null) App.chat.NotePetSaid(reply);
        Say(reply, "happy");
    }

    void HandleMenu(string id)
    {
        switch (id)
        {
            case "pat": Pat(); break;
            case "feed": Feed(); break;
            case "chat": App.OpenChat(); break;
            case "topic": SayCat("topic"); break;
            case "playRps": StartGame(true); break;
            case "playNum": StartGame(false); break;
            case "action": RandomAction(); break;
            case "walk": WalkSomewhere(false); break;
            case "roam":
                RoamEnabled = !RoamEnabled;
                App.store.Set("roam", RoamEnabled);
                App.SaveSoon();
                SayCat(RoamEnabled ? "roam_on" : "roam_off");
                break;
            case "pose": CyclePose(); break;
            case "settings": App.OpenSettings(); break;
            case "hide": App.HidePet(); break;
        }
    }

    /* ================= 显示 / 隐藏 ================= */

    public void ShowPet()
    {
        if (!Visible) Show();
        ApplyLayout();
        Redraw();
    }

    public void HidePet()
    {
        SayCat("hide");
        App.SaveSoon();
        Timer t = new Timer();
        t.Interval = 1400;
        t.Tick += delegate
        {
            t.Stop(); t.Dispose();
            Hide();
        };
        t.Start();
    }

    public void Recall()
    {
        ShowPet();
        SayCat("recall", "happy");
        hopV = 7f * App.S;
        AddFx("heart", 0f, -0.3f, 2);
    }
}
