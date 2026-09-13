// 排障用小工具：截图
//   · 默认从屏幕抓（带 CAPTUREBLT，能抓分层窗口）
//   · --window "<标题>" 用 PrintWindow 直接抓某个窗口自己的内容（不受窗口相互遮挡影响）
// 编译：csc /target:exe /out:tools\shot.exe tools\ScreenShot.cs /reference:System.Drawing.dll
// 用法：shot.exe <输出png> [x y w h]
//       shot.exe <输出png> --window "娅娅"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

static class ScreenShot
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine("用法: shot.exe <输出png> [x y w h | --window 标题]"); return; }
        string outPath = args[0];

        if (args[1] == "--desktop")
        {
            // 抓整个桌面（PrintWindow 桌面窗口，走的是合成后的桌面图像，和 BitBlt 不是同一条路）
            IntPtr desk = GetDesktopWindow();
            RECT dr; GetWindowRect(desk, out dr);
            int dw = dr.R - dr.L, dh = dr.B - dr.T;
            IntPtr sdc2 = GetDC(IntPtr.Zero);
            IntPtr mdc2 = CreateCompatibleDC(sdc2);
            IntPtr bmp2 = CreateCompatibleBitmap(sdc2, dw, dh);
            IntPtr old2 = SelectObject(mdc2, bmp2);
            bool okd = PrintWindow(desk, mdc2, 2);
            SelectObject(mdc2, old2);
            try
            {
                using (Image full = Image.FromHbitmap(bmp2))
                {
                    if (args.Length >= 7)
                    {
                        int cx = int.Parse(args[2]), cy = int.Parse(args[3]), cw = int.Parse(args[4]), ch = int.Parse(args[5]);
                        using (var crop = new Bitmap(cw, ch))
                        {
                            using (var g = Graphics.FromImage(crop)) g.DrawImage(full, new Rectangle(0, 0, cw, ch), new Rectangle(cx, cy, cw, ch), GraphicsUnit.Pixel);
                            crop.Save(outPath, ImageFormat.Png);
                        }
                    }
                    else full.Save(outPath, ImageFormat.Png);
                }
                Console.WriteLine("saved(desktop) " + outPath + "  " + dw + "x" + dh + "  printwindow=" + okd);
            }
            finally { DeleteObject(bmp2); DeleteDC(mdc2); ReleaseDC(IntPtr.Zero, sdc2); }
            return;
        }

        if (args[1] == "--window")
        {
            string title = args.Length > 2 ? args[2] : "";
            IntPtr hwnd = FindWindow(null, title);
            if (hwnd == IntPtr.Zero) { Console.WriteLine("找不到窗口：" + title); return; }
            RECT r;
            GetWindowRect(hwnd, out r);
            int w = r.R - r.L, h = r.B - r.T;
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBmp = CreateCompatibleBitmap(screenDc, w, h);
            IntPtr old = SelectObject(memDc, hBmp);
            bool ok = PrintWindow(hwnd, memDc, 2 /*PW_RENDERFULLCONTENT*/);
            SelectObject(memDc, old);
            try
            {
                using (Image img = Image.FromHbitmap(hBmp)) img.Save(outPath, ImageFormat.Png);
                Console.WriteLine("saved(window) " + outPath + "  " + w + "x" + h + "  printwindow=" + ok + "  hwnd=" + hwnd);
            }
            finally { DeleteObject(hBmp); DeleteDC(memDc); ReleaseDC(IntPtr.Zero, screenDc); }
            return;
        }

        int x, y, w2, h2;
        if (args.Length >= 5)
        {
            x = int.Parse(args[1]); y = int.Parse(args[2]); w2 = int.Parse(args[3]); h2 = int.Parse(args[4]);
        }
        else
        {
            x = GetSystemMetrics(76); y = GetSystemMetrics(77);
            w2 = GetSystemMetrics(78); h2 = GetSystemMetrics(79);
        }

        IntPtr sdc = GetDC(IntPtr.Zero);
        IntPtr mdc = CreateCompatibleDC(sdc);
        IntPtr bmp = CreateCompatibleBitmap(sdc, w2, h2);
        IntPtr oldB = SelectObject(mdc, bmp);
        bool ok2 = BitBlt(mdc, 0, 0, w2, h2, sdc, x, y, 0x00CC0020 | 0x40000000 /*SRCCOPY|CAPTUREBLT*/);
        SelectObject(mdc, oldB);
        try
        {
            using (Image img = Image.FromHbitmap(bmp)) img.Save(outPath, ImageFormat.Png);
            Console.WriteLine("saved(screen) " + outPath + "  " + w2 + "x" + h2 + "  bitblt=" + ok2);
        }
        finally { DeleteObject(bmp); DeleteDC(mdc); ReleaseDC(IntPtr.Zero, sdc); }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int L, T, R, B; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, int flags);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int w, int h);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hDC, IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hDC);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
}
