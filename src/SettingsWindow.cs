// 设置窗口
//   · 聊天方式：本地词库 / AI 对话（就是你选的那个「两种都做，设置里自己挑」）
//   · AI 三件套：接口地址、模型、API Key（Key 用 DPAPI 加密后才落盘）
//   · 外观与行为：大小、自动走动、开机自启、桌面快捷方式
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

class SettingsWindow : Form
{
    App App2;
    float S = 1f;

    TextBox nameBox, baseBox, modelBox, keyBox, extraBox;
    RadioButton localRadio, aiRadio;
    ComboBox langBox;
    TrackBar sizeSlider;
    Label sizeLabel;
    CheckBox roamBox, autoStartBox;
    Label testLabel, infoLabel;
    double originalScale = 1.0;

    bool dragging = false;
    Point dragOffset;

    public SettingsWindow(App app)
    {
        App2 = app;
        S = app.S;
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        Text = Lang.F("set.titleFmt", App2.PetName);
        BackColor = Color.FromArgb(252, 249, 251);
        AutoScaleMode = AutoScaleMode.None;
        Size = new Size((int)(470 * S), (int)(590 * S));
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        BuildUi();

        MouseDown += delegate (object s, MouseEventArgs e) { if (e.Y < 44 * S) { dragging = true; dragOffset = new Point(e.X, e.Y); } };
        MouseMove += delegate (object s, MouseEventArgs e)
        {
            if (dragging)
            {
                Point p = PointToScreen(new Point(e.X, e.Y));
                Location = new Point(p.X - dragOffset.X, p.Y - dragOffset.Y);
            }
        };
        MouseUp += delegate { dragging = false; };
    }

    Label Lab(string text, int x, int y, float size, bool bold)
    {
        var l = new Label();
        l.Text = text;
        l.Font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
        l.ForeColor = Color.FromArgb(70, 60, 70);
        l.AutoSize = true;
        l.Location = new Point((int)(x * S), (int)(y * S));
        Controls.Add(l);
        return l;
    }

    TextBox Box(int x, int y, int w, string value, bool password)
    {
        var t = new TextBox();
        t.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        t.Location = new Point((int)(x * S), (int)(y * S));
        t.Size = new Size((int)(w * S), (int)(26 * S));
        t.Text = value ?? "";
        t.BorderStyle = BorderStyle.FixedSingle;
        if (password) t.UseSystemPasswordChar = true;
        Controls.Add(t);
        return t;
    }

    Button Btn(string text, int x, int y, int w, int h, EventHandler onClick)
    {
        var b = new Button();
        b.Text = text;
        b.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        b.FlatStyle = FlatStyle.Flat;
        b.BackColor = Color.FromArgb(255, 232, 241);
        b.ForeColor = Color.FromArgb(175, 60, 105);
        b.FlatAppearance.BorderColor = Color.FromArgb(240, 190, 210);
        b.Location = new Point((int)(x * S), (int)(y * S));
        b.Size = new Size((int)(w * S), (int)(h * S));
        b.Click += onClick;
        Controls.Add(b);
        return b;
    }

    void BuildUi()
    {
        Lab(Lang.F("set.titleFmt", App2.PetName), 16, 10, 11.5f, true);

        /* 名字 */
        Lab(Lang.T("set.name"), 20, 52, 9.5f, true);
        nameBox = Box(20, 74, 200, App2.pet.PetName, false);

        /* 语言 */
        Lab(Lang.T("set.lang"), 250, 52, 9.5f, true);
        langBox = new ComboBox();
        langBox.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        langBox.DropDownStyle = ComboBoxStyle.DropDownList;
        langBox.Items.Add("中文");
        langBox.Items.Add("English");
        langBox.Location = new Point((int)(250 * S), (int)(74 * S));
        langBox.Size = new Size((int)(150 * S), (int)(26 * S));
        langBox.SelectedIndex = Lang.IsEn ? 1 : 0;
        Controls.Add(langBox);

        /* 聊天方式 */
        Lab(Lang.T("set.chatMode"), 20, 116, 9.5f, true);
        localRadio = new RadioButton();
        localRadio.Text = Lang.T("set.local");
        localRadio.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        localRadio.Location = new Point((int)(20 * S), (int)(138 * S));
        localRadio.AutoSize = true;
        Controls.Add(localRadio);

        aiRadio = new RadioButton();
        aiRadio.Text = Lang.T("set.ai");
        aiRadio.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        aiRadio.Location = new Point((int)(20 * S), (int)(162 * S));
        aiRadio.AutoSize = true;
        Controls.Add(aiRadio);

        string mode = App2.store.GetString("chatMode", "local");
        localRadio.Checked = mode != "ai";
        aiRadio.Checked = mode == "ai";

        /* AI 参数 */
        Lab(Lang.T("set.endpoint"), 20, 196, 9f, false);
        baseBox = Box(20, 216, 430, App2.store.GetString("apiBase", AiChat.DefaultBase), false);
        Lab(Lang.T("set.model"), 20, 248, 9f, false);
        modelBox = Box(20, 268, 200, App2.store.GetString("apiModel", AiChat.DefaultModel), false);
        Lab(Lang.T("set.key"), 20, 300, 9f, false);
        keyBox = Box(20, 320, 300, App2.store.GetSecret("apiKey"), true);
        Btn(Lang.T("set.test"), 330, 319, 90, 28, delegate { TestConnection(); });
        testLabel = Lab("", 20, 352, 8.5f, false);
        testLabel.ForeColor = Color.FromArgb(130, 130, 140);
        testLabel.MaximumSize = new Size((int)(430 * S), 0);

        /* 性格补充 */
        Lab(Lang.T("set.extra"), 20, 380, 9f, false);
        extraBox = new TextBox();
        extraBox.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        extraBox.Location = new Point((int)(20 * S), (int)(400 * S));
        extraBox.Size = new Size((int)(430 * S), (int)(44 * S));
        extraBox.Multiline = true;
        extraBox.BorderStyle = BorderStyle.FixedSingle;
        extraBox.Text = App2.store.GetString("personaExtra", "");
        Controls.Add(extraBox);

        /* 外观与行为 */
        Lab(Lang.T("set.size"), 20, 446, 9f, true);
        originalScale = App2.store.GetDouble("sizeScale", 1.0);
        sizeSlider = new TrackBar();
        sizeSlider.Minimum = 40;
        sizeSlider.Maximum = 250;
        sizeSlider.TickFrequency = 20;
        sizeSlider.SmallChange = 5;
        sizeSlider.LargeChange = 20;
        sizeSlider.Value = (int)Math.Round(originalScale * 100);
        if (sizeSlider.Value < sizeSlider.Minimum) sizeSlider.Value = sizeSlider.Minimum;
        if (sizeSlider.Value > sizeSlider.Maximum) sizeSlider.Value = sizeSlider.Maximum;
        sizeSlider.Location = new Point((int)(124 * S), (int)(440 * S));
        sizeSlider.Size = new Size((int)(210 * S), (int)(34 * S));
        sizeSlider.BackColor = Color.FromArgb(252, 249, 251);
        sizeSlider.ValueChanged += delegate { OnSizeChanged(); };
        Controls.Add(sizeSlider);

        sizeLabel = Lab("", 20, 474, 8.5f, false);
        sizeLabel.ForeColor = Color.FromArgb(120, 120, 130);

        roamBox = new CheckBox();
        roamBox.Text = Lang.T("set.roam");
        roamBox.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        roamBox.Location = new Point((int)(20 * S), (int)(500 * S));
        roamBox.AutoSize = true;
        roamBox.Checked = App2.store.GetBool("roam", true);
        Controls.Add(roamBox);

        autoStartBox = new CheckBox();
        autoStartBox.Text = Lang.T("set.autostart");
        autoStartBox.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        autoStartBox.Location = new Point((int)(250 * S), (int)(500 * S));
        autoStartBox.AutoSize = true;
        autoStartBox.Checked = App2.IsAutoStart();
        Controls.Add(autoStartBox);

        Btn(Lang.T("set.shortcut"), 20, 524, 170, 28, delegate
        {
            bool ok = App2.CreateShortcut();
            testLabel.Text = ok ? Lang.T("set.shortcutOk") : Lang.T("set.shortcutFail");
        });

        /* 底部按钮 */
        Btn(Lang.T("set.save"), 250, 550, 90, 32, delegate { Save(); });
        Btn(Lang.T("set.cancel"), 350, 550, 100, 32, delegate { CancelSize(); Hide(); });

        /* 信息 */
        infoLabel = new Label();
        infoLabel.Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Regular, GraphicsUnit.Point);
        infoLabel.ForeColor = Color.FromArgb(150, 150, 160);
        infoLabel.AutoSize = false;
        infoLabel.Location = new Point((int)(205 * S), (int)(524 * S));
        infoLabel.Size = new Size((int)(245 * S), (int)(30 * S));
        infoLabel.Text = Lang.F("set.infoFmt", App2.pet.Intimacy, App2.lines.LevelName(App2.pet.Intimacy));
        Controls.Add(infoLabel);

        UpdateSizeLabel();
    }

    /// <summary>拖动滑杆：立刻按比例改她的大小，所见即所得</summary>
    void OnSizeChanged()
    {
        App2.pet.SizeScale = (float)(sizeSlider.Value / 100.0);
        App2.pet.ApplyLayout();
        UpdateSizeLabel();
    }

    void UpdateSizeLabel()
    {
        int px = App2.pet.PetPixelH;
        double pct = px * 100.0 / Math.Max(1, Screen.PrimaryScreen.Bounds.Height);
        sizeLabel.Text = Lang.F("set.sizeFmt", sizeSlider.Value, px, pct.ToString("0"));
    }

    void CancelSize()
    {
        App2.pet.SizeScale = (float)originalScale;
        App2.pet.ApplyLayout();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(Color.FromArgb(252, 248, 251))) g.FillRectangle(b, ClientRectangle);
        using (var b = new SolidBrush(Color.FromArgb(255, 226, 238)))
            g.FillRectangle(b, new Rectangle(0, 0, Width, (int)(40 * S)));
        using (var p = new Pen(Color.FromArgb(240, 200, 215), 1f)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);

        var cr = new RectangleF(Width - 38 * S, 7 * S, 26 * S, 26 * S);
        using (var b = new SolidBrush(Color.FromArgb(255, 245, 248))) g.FillEllipse(b, cr);
        using (var p = new Pen(Color.FromArgb(200, 130, 150), 1.3f * S)) g.DrawEllipse(p, cr);
        using (var f = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold, GraphicsUnit.Point))
        using (var b = new SolidBrush(Color.FromArgb(160, 70, 100)))
            g.DrawString("×", f, b, cr.X + 7 * S, cr.Y + 3 * S);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.X > Width - 42 * S && e.Y < 38 * S) { CancelSize(); Hide(); }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; CancelSize(); Hide(); }
        base.OnFormClosing(e);
    }

    /* ---------------- 保存 / 测试 ---------------- */

    void Save()
    {
        string newName = (nameBox.Text ?? "").Trim();
        if (newName.Length == 0) newName = App2.lines.DefaultName;
        if (newName.Length > 12) newName = newName.Substring(0, 12);

        // 名字如果还是默认名，跟着语言一起换（娅娅 ↔ Yaya）
        string langCode = langBox.SelectedIndex == 1 ? "en" : "zh";
        if (langCode == "en" && newName == "娅娅") newName = "Yaya";
        else if (langCode == "zh" && newName == "Yaya") newName = "娅娅";

        App2.store.Set("name", newName);
        App2.store.Set("chatMode", aiRadio.Checked ? "ai" : "local");
        App2.store.Set("apiBase", (baseBox.Text ?? "").Trim());
        App2.store.Set("apiModel", (modelBox.Text ?? "").Trim());
        App2.store.SetSecret("apiKey", (keyBox.Text ?? "").Trim());
        App2.store.Set("personaExtra", (extraBox.Text ?? "").Trim());
        App2.store.Set("roam", roamBox.Checked);
        App2.store.Set("sizeScale", sizeSlider.Value / 100.0);
        App2.store.Save();

        App2.ApplyAutoStart(autoStartBox.Checked);
        App2.ApplySettings();
        Hide();

        if (aiRadio.Checked && string.IsNullOrEmpty((keyBox.Text ?? "").Trim()))
            App2.pet.Say(Lang.T("set.aiNoKey"), "shy");

        // 语言变了：切台词库 + 刷新托盘，然后重开设置窗让文案也换过来
        if (langCode != Lang.Code)
        {
            App2.ApplyLanguage(langCode);
            App2.OpenSettings();
        }
    }

    /// <summary>自检用：模拟在语言下拉里选一项并点保存（走的是真实控件与保存流程）</summary>
    public void TestPickLanguage(int index)
    {
        langBox.SelectedIndex = index;
        Save();
    }

    /// <summary>自检用：语言下拉的 x / y / 宽 / 高（物理像素），用来给截图标位置</summary>
    public Rectangle LanguageBoxBounds
    {
        get { return new Rectangle(langBox.Left, langBox.Top, langBox.Width, langBox.Height); }
    }

    void TestConnection()    {
        string url = (baseBox.Text ?? "").Trim();
        string model = (modelBox.Text ?? "").Trim();
        string key = (keyBox.Text ?? "").Trim();
        if (string.IsNullOrEmpty(key))
        {
            testLabel.Text = Lang.T("set.noKey");
            return;
        }
        testLabel.Text = Lang.T("set.testing");
        Application.DoEvents();
        string sys = App2.lines.SystemPrompt(App2.pet.PetName, App2.lines.LevelName(App2.pet.Intimacy), "", App2.pet.Intimacy);
        string r = AiChat.Test(url, key, model, sys);
        testLabel.Text = r;
    }
}
