// 聊天窗口：普通（非分层）窗口，因为里面要放真的输入框
//   · 上方是对话记录（自己画的，支持中文换行与滚动）
//   · 下方是输入框，回车发送；她能记住最近 12 轮上下文（AI 模式会用上）
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

class ChatWindow : Form
{
    public class Turn { public bool Me; public string Text; public DateTime At; }

    App App2;
    List<Turn> turns = new List<Turn>();
    List<Turn> history = new List<Turn>();      // 只留对话内容，给 AI 当上下文

    Panel transcript;
    TextBox input;
    Button send;
    Timer anim;
    bool thinking = false;
    string status = "";

    float S = 1f;

    public ChatWindow(App app)
    {
        App2 = app;
        S = app.S;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Text = Lang.F("chat.titleFmt", app.PetName);
        BackColor = Color.FromArgb(252, 250, 252);
        AutoScaleMode = AutoScaleMode.None;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        BuildUi();

        anim = new Timer();
        anim.Interval = 120;
        anim.Tick += delegate { if (thinking) transcript.Invalidate(); };
    }

    void BuildUi()
    {
        int W = (int)(380 * S), H = (int)(470 * S);
        Size = new Size(W, H);

        transcript = new Panel();
        transcript.Location = new Point((int)(10 * S), (int)(46 * S));
        transcript.Size = new Size(W - (int)(20 * S), H - (int)(46 * S) - (int)(62 * S));
        transcript.BackColor = Color.FromArgb(255, 255, 255);
        transcript.AutoScroll = true;
        transcript.Paint += delegate (object s, PaintEventArgs e) { DrawTranscript(e.Graphics); };
        Controls.Add(transcript);

        input = new TextBox();
        input.BorderStyle = BorderStyle.FixedSingle;
        input.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
        input.Location = new Point((int)(12 * S), H - (int)(48 * S));
        input.Size = new Size(W - (int)(90 * S), (int)(30 * S));
        input.KeyDown += delegate (object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                Send();
            }
            else if (e.KeyCode == Keys.Escape) Hide();
        };
        Controls.Add(input);

        send = new Button();
        send.Text = Lang.T("chat.send");
        send.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        send.FlatStyle = FlatStyle.Flat;
        send.BackColor = Color.FromArgb(255, 226, 238);
        send.ForeColor = Color.FromArgb(190, 60, 110);
        send.Location = new Point(W - (int)(72 * S), H - (int)(48 * S));
        send.Size = new Size((int)(60 * S), (int)(30 * S));
        send.Click += delegate { Send(); };
        Controls.Add(send);

        MouseDown += delegate (object s, MouseEventArgs e) { DragStart(e); };
        MouseMove += delegate (object s, MouseEventArgs e) { DragMove(e); };
        MouseUp += delegate { dragging = false; };
    }

    /* ---------------- 拖动 / 关闭 ---------------- */

    bool dragging = false;
    Point dragOffset;

    void DragStart(MouseEventArgs e)
    {
        if (e.Y < 44 * S) { dragging = true; dragOffset = new Point(e.X, e.Y); }
    }

    void DragMove(MouseEventArgs e)
    {
        if (!dragging) return;
        Point p = PointToScreen(new Point(e.X, e.Y));
        Location = new Point(p.X - dragOffset.X, p.Y - dragOffset.Y);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(Color.FromArgb(252, 248, 251))) g.FillRectangle(b, ClientRectangle);
        using (var b = new SolidBrush(Color.FromArgb(255, 226, 238)))
            g.FillRectangle(b, new Rectangle(0, 0, Width, (int)(44 * S)));
        using (var f = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold, GraphicsUnit.Point))
        using (var b = new SolidBrush(Color.FromArgb(170, 50, 80)))
            g.DrawString(Lang.F("chat.titleFmt", App2.PetName), f, b, 12 * S, 10 * S);
        using (var f = new Font("Microsoft YaHei UI", 8f, FontStyle.Regular, GraphicsUnit.Point))
        using (var b = new SolidBrush(Color.FromArgb(190, 120, 140)))
            g.DrawString(App2.ChatModeLabel(), f, b, 12 * S, 28 * S);

        // 关闭按钮
        var cr = new RectangleF(Width - 40 * S, 8 * S, 28 * S, 28 * S);
        using (var b = new SolidBrush(Color.FromArgb(255, 245, 248))) g.FillEllipse(b, cr);
        using (var p = new Pen(Color.FromArgb(200, 120, 140), 1.4f * S)) g.DrawEllipse(p, cr);
        using (var f = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold, GraphicsUnit.Point))
        using (var b = new SolidBrush(Color.FromArgb(160, 70, 100)))
            g.DrawString("×", f, b, cr.X + 8 * S, cr.Y + 4 * S);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.X > Width - 44 * S && e.Y < 40 * S) Hide();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnFormClosing(e);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
        {
            App2.OnChatShown();
            ReflowTranscript();
            input.Focus();
        }
    }

    /* ---------------- 对话记录绘制 ---------------- */

    void ReflowTranscript()
    {
        // 用「虚拟高度」撑开滚动条
        int w = transcript.ClientSize.Width - (int)(16 * S);
        int total = (int)(8 * S);
        using (var f = FontForBubble())
        {
            foreach (Turn t in turns)
            {
                Size sz = TextRenderer.MeasureText(t.Text, f, new Size((int)(w * 0.72f), 10000),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl);
                total += sz.Height + (int)(18 * S);
            }
        }
        if (thinking) total += (int)(34 * S);
        transcript.AutoScrollMinSize = new Size(0, total);
        transcript.Invalidate();
        if (transcript.VerticalScroll.Visible)
            transcript.VerticalScroll.Value = transcript.VerticalScroll.Maximum;
    }

    Font FontForBubble()
    {
        return new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
    }

    void DrawTranscript(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.White);
        g.TranslateTransform(0, transcript.AutoScrollPosition.Y);

        int pad = (int)(10 * S);
        int w = transcript.ClientSize.Width - (int)(16 * S);
        int y = (int)(8 * S);

        using (var f = FontForBubble())
        {
            if (turns.Count == 0)
            {
                using (var b = new SolidBrush(Color.FromArgb(160, 160, 160, 170)))
                    g.DrawString(Lang.T("chat.empty"), f, b, pad + 4 * S, y + 6 * S);
            }
            foreach (Turn t in turns)
            {
                int maxW = (int)(w * 0.72f);
                Size sz = TextRenderer.MeasureText(t.Text, f, new Size(maxW, 10000),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl);
                int bw = sz.Width + (int)(18 * S);
                int bh = sz.Height + (int)(14 * S);
                int bx = t.Me ? (w - bw) : pad;
                var rect = new RectangleF(bx, y, bw, bh);
                using (var path = RoundedRect(rect, 10 * S))
                using (var b = new SolidBrush(t.Me ? Color.FromArgb(255, 226, 238) : Color.FromArgb(244, 243, 246)))
                    g.FillPath(b, path);
                TextRenderer.DrawText(g, t.Text, f,
                    new Rectangle((int)rect.X + (int)(9 * S), (int)rect.Y + (int)(7 * S), sz.Width, sz.Height),
                    Color.FromArgb(255, 55, 45, 55),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl);
                y += bh + (int)(8 * S);
            }

            if (thinking)
            {
                int dots = (int)(DateTime.Now.TimeOfDay.TotalSeconds * 3) % 4;
                string t = Lang.F("chat.thinkingFmt", App2.PetName, new string('…', Math.Max(1, dots)));
                var rect = new RectangleF(pad, y, TextRenderer.MeasureText(t, f).Width + 20 * S, 26 * S);
                using (var path = RoundedRect(rect, 10 * S))
                using (var b = new SolidBrush(Color.FromArgb(244, 243, 246)))
                    g.FillPath(b, path);
                TextRenderer.DrawText(g, t, f, new Point((int)rect.X + (int)(9 * S), (int)rect.Y + (int)(5 * S)),
                    Color.FromArgb(255, 130, 130, 140));
            }
        }
    }

    GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = radius * 2f;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /* ---------------- 发消息 ---------------- */

    public void ShowChat()
    {
        if (!Visible)
        {
            // 出现在她旁边，别盖住她
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int x = App2.pet.PetX - Width - (int)(16 * S);
            if (x < wa.Left + 10) x = App2.pet.PetX + App2.pet.PetPixelW + (int)(16 * S);
            if (x + Width > wa.Right) x = wa.Right - Width - (int)(16 * S);
            int y = App2.pet.PetY + App2.pet.PetPixelH - Height;
            if (y + Height > wa.Bottom) y = wa.Bottom - Height - (int)(10 * S);
            if (y < wa.Top) y = wa.Top + (int)(10 * S);
            Location = new Point(x, y);
            Show();
        }
        BringToFront();
        input.Focus();
        Invalidate();
    }

    void Send()
    {
        string text = (input.Text ?? "").Trim();
        if (text.Length == 0) return;
        input.Clear();

        AddTurn(true, text);
        thinking = true;
        ReflowTranscript();
        anim.Start();

        string mode = App2.store.GetString("chatMode", "local");
        if (mode == "ai")
        {
            string key = App2.store.GetSecret("apiKey");
            if (string.IsNullOrEmpty(key))
            {
                thinking = false; anim.Stop();
                AddTurn(false, Lang.T("set.badKeyLine"));
                ReflowTranscript();
                return;
            }
            App2.AskAi(history, text, OnAiReply, OnAiError);
        }
        else
        {
            Timer t = new Timer();
            int delay = 420 + Math.Min(1400, text.Length * 45);
            t.Interval = delay;
            t.Tick += delegate
            {
                t.Stop(); t.Dispose();
                thinking = false; anim.Stop();
                List<string> reply = App2.lines.LocalReply(text, App2.pet.Level, App2.Vars());
                if (reply.Count == 0) reply.Add(Lang.T("chat.noReply"));
                foreach (string line in reply) AddTurn(false, line);
                App2.pet.Say(reply[0], "normal");
                if (reply.Count > 1) App2.pet.Say(reply[1]);
                ReflowTranscript();
            };
            t.Start();
        }
    }

    void OnAiReply(string reply)
    {
        if (IsDisposed) return;
        BeginInvoke((MethodInvoker)delegate
        {
            thinking = false;
            anim.Stop();
            foreach (string part in SplitReply(reply)) AddTurn(false, part);
            App2.pet.Say(FirstLine(reply), "normal");
            ReflowTranscript();
        });
    }

    void OnAiError(string err)
    {
        if (IsDisposed) return;
        BeginInvoke((MethodInvoker)delegate
        {
            thinking = false;
            anim.Stop();
            string line = App2.lines.Pick("error", App2.Vars(), App2.pet.Level);
            if (line == null) line = Lang.T("chat.aiErrorFallback");
            AddTurn(false, line);
            status = err;
            App2.pet.Say(line, "shy");
            ReflowTranscript();
        });
    }

    static string FirstLine(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string[] parts = text.Replace("\r", "").Split('\n');
        foreach (string p in parts) if (p.Trim().Length > 0) return p.Trim();
        return text;
    }

    static List<string> SplitReply(string text)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(text)) return list;
        string[] parts = text.Replace("\r", "").Split('\n');
        foreach (string p in parts)
        {
            string s = p.Trim();
            if (s.Length > 0) list.Add(s);
            if (list.Count >= 3) break;
        }
        if (list.Count == 0) list.Add(text.Trim());
        return list;
    }

    void AddTurn(bool me, string text)
    {
        var t = new Turn();
        t.Me = me;
        t.Text = text;
        t.At = DateTime.Now;
        turns.Add(t);
        history.Add(t);
        while (turns.Count > 60) turns.RemoveAt(0);
        while (history.Count > 24) history.RemoveAt(0);
    }

    /// <summary>宠物那边先开口（比如你点「找个话题」），聊天窗也记一笔</summary>
    public void NotePetSaid(string text)
    {
        AddTurn(false, text);
        if (Visible) ReflowTranscript();
    }

    public string StatusText { get { return status; } }

    /* ---------------- 自检用的接口 ---------------- */

    /// <summary>自检：把一句话走一遍真实发送流程（输入框 → 发送 → 回话）</summary>
    public void TestSend(string text)
    {
        input.Text = text;
        Send();
    }

    public List<Turn> Transcript { get { return turns; } }
}
