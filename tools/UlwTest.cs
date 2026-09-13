// 排障用：一个最小化的 UpdateLayeredWindow 窗口（纯红方块），用来验证「截图能不能抓到分层窗口」
// 编译：csc /target:winexe /out:tools\ulwtest.exe tools\UlwTest.cs /reference:System.Drawing.dll /reference:System.Windows.Forms.dll
// 用法：ulwtest.exe <x> <y> [秒] [颜色 hex] [宽] [高]
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class UlwTest : Form
{
    int secs; Color color; int bw, bh;
    Timer t;

    public UlwTest(int x, int y, int s, Color c, int w, int h)
    {
        secs = s; color = c; bw = w; bh = h;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        SetBounds(x, y, w, h);
        var tm = new Timer();
        tm.Interval = 200;
        tm.Tick += delegate { Paint_(); };
        tm.Start();
        t = tm;
        var kill = new Timer();
        kill.Interval = secs * 1000;
        kill.Tick += delegate { kill.Stop(); Application.Exit(); };
        kill.Start();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00080000 | 0x00000080 | 0x08000000; // LAYERED | TOOLWINDOW | NOACTIVATE
            return cp;
        }
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    void Paint_()
    {
        if (!IsHandleCreated) return;
        using (var bmp = new Bitmap(bw, bh, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                using (var b = new SolidBrush(color)) g.FillEllipse(b, 10, 10, bw - 20, bh - 20);
                using (var f = new Font("Arial", 40, FontStyle.Bold))
                using (var b = new SolidBrush(Color.White)) g.DrawString("TEST", f, b, 10, bh / 2f - 30);
            }
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBmp = bmp.GetHbitmap(Color.FromArgb(0));
            IntPtr old = SelectObject(memDc, hBmp);
            var size = new SIZE(bw, bh);
            var src = new POINT(0, 0);
            var dst = new POINT(Left, Top);
            var bf = new BLENDFUNCTION();
            bf.BlendOp = 0; bf.BlendFlags = 0; bf.SourceConstantAlpha = 255; bf.AlphaFormat = 1;
            bool ok = UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref bf, 2);
            SelectObject(memDc, old);
            DeleteObject(hBmp);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
            try
            {
                System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ulwtest.log"),
                    DateTime.Now.ToString("HH:mm:ss") + " ulw=" + ok + " err=" + Marshal.GetLastWin32Error() +
                    " pos=" + Left + "," + Top + " size=" + bw + "x" + bh + " hwnd=" + Handle + Environment.NewLine);
            }
            catch { }
            if (!ok) Console.WriteLine("ULW 失败 " + Marshal.GetLastWin32Error());
        }
    }

    [STAThread]
    static void Main(string[] args)
    {
        int x = args.Length > 0 ? int.Parse(args[0]) : 100;
        int y = args.Length > 1 ? int.Parse(args[1]) : 100;
        int s = args.Length > 2 ? int.Parse(args[2]) : 8;
        Color c = Color.Red;
        if (args.Length > 3) c = Color.FromArgb(Convert.ToInt32(args[3], 16));
        int w = args.Length > 4 ? int.Parse(args[4]) : 220;
        int h = args.Length > 5 ? int.Parse(args[5]) : 220;
        Application.EnableVisualStyles();
        var f = new UlwTest(x, y, s, c, w, h);
        f.Show();
        Application.Run(f);
    }

    [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; public SIZE(int a, int b) { cx = a; cy = b; } }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; public POINT(int a, int b) { x = a; y = b; } }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr d);
    [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr dst, ref POINT pd, ref SIZE ps, IntPtr src, ref POINT pSrc, int key, ref BLENDFUNCTION bf, int flags);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr h);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr h, IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr h);
}
