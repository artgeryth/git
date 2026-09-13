// 排障用：从系统的角度确认「桌面宠物窗口到底在不在这块屏幕上、透明处是否点穿」
//   WindowFromPoint 返回某个屏幕坐标上最上层、且该像素不透明的窗口。
//   · 打在她身上 → 应该返回她自己（说明她确实盖在浏览器上面）
//   · 打在窗口的透明角落 → 应该返回下面的窗口（说明透明区域鼠标能点穿，不挡你点东西）
// 编译：csc /target:exe /out:tools\probe.exe tools\Probe.cs
// 用法：probe.exe "娅娅"
using System;
using System.Runtime.InteropServices;
using System.Text;

static class Probe
{
    [STAThread]
    static void Main(string[] args)
    {
        string title = args.Length > 0 ? args[0] : "娅娅";
        try { SetProcessDPIAware(); } catch { }

        IntPtr hwnd = FindWindow(null, title);
        if (hwnd == IntPtr.Zero) { Console.WriteLine("找不到标题为「" + title + "」的窗口"); return; }
        RECT r;
        GetWindowRect(hwnd, out r);
        int w = r.R - r.L, h = r.B - r.T;
        Console.WriteLine("窗口 hwnd=" + hwnd + "  物理矩形 (" + r.L + "," + r.T + ")-(" + r.R + "," + r.B + ")  尺寸 " + w + "x" + h);
        Console.WriteLine("屏幕 " + GetSystemMetrics(0) + "x" + GetSystemMetrics(1) + "，DPI=" + GetDpiForSystemSafe());

        // 采样点：窗内相对坐标（比例）。她画在窗口下方中间，气泡在上方中间。
        double[][] pts = new double[][] {
            new double[] { 0.50, 0.75 },   // 她的身体
            new double[] { 0.50, 0.55 },   // 她的头/肩
            new double[] { 0.50, 0.22 },   // 气泡位置
            new double[] { 0.02, 0.02 },   // 左上角（透明）
            new double[] { 0.98, 0.02 },   // 右上角（透明）
        };
        string[] names = new string[] { "身体", "头部", "气泡区", "左上透明角", "右上透明角" };

        for (int i = 0; i < pts.Length; i++)
        {
            int px = r.L + (int)(w * pts[i][0]);
            int py = r.T + (int)(h * pts[i][1]);
            IntPtr hit = WindowFromPoint(new POINT(px, py));
            var sb = new StringBuilder(256);
            GetWindowText(hit, sb, 256);
            var cls = new StringBuilder(256);
            GetClassName(hit, cls, 256);
            Console.WriteLine("  " + names[i] + " (" + px + "," + py + ") → hwnd=" + hit +
                              "  标题=\"" + Trunc(sb.ToString()) + "\"  类=" + cls +
                              (hit == hwnd ? "   ★是她" : ""));
        }
    }

    static string Trunc(string s) { return s.Length > 28 ? s.Substring(0, 28) + "…" : s; }

    static int GetDpiForSystemSafe()
    {
        try { return GetDpiForSystem(); } catch { return 0; }
    }

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; public POINT(int a, int b) { x = a; y = b; } }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern int GetDpiForSystem();
}
