// 分层窗口基类
// 桌面宠物要的「真透明」用 UpdateLayeredWindow 实现：每个像素带 alpha，
// 于是她能真的趴在壁纸/其他窗口上面，边缘也不会有白色方块；
// 完全透明的像素 Windows 会自动让鼠标点穿（不挡你操作别的窗口）。
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class LayeredWindow : Form
{
    Bitmap buffer;
    Graphics g;

    public LayeredWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.Black;
        SetStyle(ControlStyles.Opaque, true);
        SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x00080000;  // WS_EX_LAYERED
            cp.ExStyle |= 0x00000080;  // WS_EX_TOOLWINDOW（不出现在 alt-tab / 任务栏）
            cp.ExStyle |= 0x08000000;  // WS_EX_NOACTIVATE（点她不会抢走你正在打字的窗口的焦点）
            return cp;
        }
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    /// <summary>子类把这一帧画进来；背景已经被清成全透明</summary>
    protected virtual void Render(Graphics g) { }

    void EnsureBuffer()
    {
        int w = Math.Max(1, Width), h = Math.Max(1, Height);
        if (buffer == null || buffer.Width != w || buffer.Height != h)
        {
            if (g != null) { g.Dispose(); g = null; }
            if (buffer != null) { buffer.Dispose(); buffer = null; }
            buffer = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            // 关键：显式声明 96 DPI。否则位图会继承屏幕 DPI（150% 屏 = 144），
            // 而 Graphics 默认的 Display 单位会按 dpi/96 缩放，所有像素坐标被悄悄放大 1.5 倍，
            // 立绘就只画得出左上角一块（表现为「人物只剩一半」）。
            buffer.SetResolution(96f, 96f);
        }
        if (g == null)
        {
            g = Graphics.FromImage(buffer);
            g.PageUnit = GraphicsUnit.Pixel;      // 再钉死一次：坐标就是像素，不随 DPI 变
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }
    }

    protected Graphics FrameGraphics { get { return g; } }

    /// <summary>第一次绘制后回调一次，用来把「到底画没画上去」写进日志</summary>
    public Action<string> Diag;

    public void Redraw()
    {
        if (!IsHandleCreated || IsDisposed) return;
        EnsureBuffer();
        g.Clear(Color.Transparent);
        Render(g);
        g.Flush();

        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memDc = CreateCompatibleDC(screenDc);
        IntPtr hBmp = IntPtr.Zero;
        IntPtr oldBmp = IntPtr.Zero;
        bool ok = false;
        int err = 0;
        try
        {
            hBmp = buffer.GetHbitmap(Color.FromArgb(0));
            oldBmp = SelectObject(memDc, hBmp);
            var size = new SIZE(buffer.Width, buffer.Height);
            var src = new POINT(0, 0);
            var dst = new POINT(Left, Top);
            var blend = new BLENDFUNCTION();
            blend.BlendOp = 0;              // AC_SRC_OVER
            blend.BlendFlags = 0;
            blend.SourceConstantAlpha = 255;
            blend.AlphaFormat = 1;          // AC_SRC_ALPHA
            ok = UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, 2 /*ULW_ALPHA*/);
            if (!ok) err = Marshal.GetLastWin32Error();
        }
        catch (Exception ex)
        {
            err = -1;
            if (Diag != null) { Diag("分层窗口绘制抛异常：" + ex.Message); Diag = null; }
        }
        finally
        {
            if (hBmp != IntPtr.Zero)
            {
                SelectObject(memDc, oldBmp);
                DeleteObject(hBmp);
            }
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }

        if (Diag != null)
        {
            Diag("首次绘制 ulw=" + ok + " err=" + err + " size=" + buffer.Width + "x" + buffer.Height +
                 " pos=" + Left + "," + Top + " hwnd=" + Handle + " screenDc=" + screenDc);
            Diag = null;
        }
    }

    /// <summary>排障用：把当前这一帧（含透明通道）存成 PNG，看看她到底画成了什么</summary>
    public bool DumpFrame(string path)
    {
        try
        {
            Redraw();
            if (buffer == null) return false;
            buffer.Save(path, ImageFormat.Png);
            return true;
        }
        catch { return false; }
    }

    /// <summary>移动窗口（分层窗口必须重画才看得见新位置）</summary>
    public void MoveTo(int x, int y)
    {
        Location = new Point(x, y);
        Redraw();
    }

    /* ---------------- 画气泡的小工具（宠物 / 菜单共用） ---------------- */

    public static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = radius * 2f;
        if (d <= 0.5f) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /* ---------------- Win32 ---------------- */

    [StructLayout(LayoutKind.Sequential)]
    struct SIZE { public int cx; public int cy; public SIZE(int x, int y) { cx = x; cy = y; } }

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int x; public int y; public POINT(int a, int b) { x = a; y = b; } }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct BLENDFUNCTION { public byte BlendOp; public byte BlendFlags; public byte SourceConstantAlpha; public byte AlphaFormat; }

    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

    /// <summary>全系统「多久没碰键盘鼠标了」（毫秒）。用来判断你是不是在忙/在不在。</summary>
    public static int SystemIdleMs()
    {
        var li = new LASTINPUTINFO();
        li.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
        if (!GetLastInputInfo(ref li)) return 0;
        return unchecked(Environment.TickCount - (int)li.dwTime);
    }
}
