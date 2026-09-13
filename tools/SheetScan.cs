// 排障用：扫描「三视图设定图」，找出三个人的横向分段与各自的纵向内容范围
// 用途：确认切图范围对不对（尤其是脚有没有被切掉）
// 编译：csc /target:exe /out:tools\sheetscan.exe tools\SheetScan.cs /reference:System.Drawing.dll
// 用法：sheetscan.exe <设定图.jpg> [容差]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

static class SheetScan
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length < 1) { Console.WriteLine("用法: sheetscan.exe <图> [容差=246]"); return; }
        int tol = args.Length > 1 ? int.Parse(args[1]) : 246;

        using (var bmp = new Bitmap(args[0]))
        {
            int W = bmp.Width, H = bmp.Height;
            Console.WriteLine(args[0] + "  " + W + "x" + H + "  容差 " + tol);

            BitmapData d = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] px = new byte[d.Stride * H];
            Marshal.Copy(d.Scan0, px, 0, px.Length);
            bmp.UnlockBits(d);
            int stride = d.Stride;

            Func<int, int, bool> content = delegate (int x, int y)
            {
                int o = y * stride + x * 4;
                return px[o] < tol || px[o + 1] < tol || px[o + 2] < tol;
            };

            // 如果给了手动范围，就按范围逐个人扫描（自动分段会把挨着的头发并成一段）
            string manual = args.Length > 2 ? args[2] : null;
            if (!string.IsNullOrEmpty(manual))
            {
                Console.WriteLine("手动范围：" + manual);
                foreach (string part in manual.Split(','))
                {
                    string[] mm = part.Split('-');
                    if (mm.Length != 2) continue;
                    int x0 = int.Parse(mm[0]), x1 = int.Parse(mm[1]);
                    int minX = -1, maxX = -1, minY = -1, maxY = -1;
                    int minY2 = -1, maxY2 = -1;
                    for (int x = x0; x <= x1 && x < W; x++)
                        for (int y = 0; y < H; y++)
                            if (content(x, y))
                            {
                                if (minY < 0) minY = y;
                                maxY = y;
                                if (minX < 0 || x < minX) minX = x;
                                if (x > maxX) maxX = x;
                            }
                    for (int y = 0; y < H; y++)
                    {
                        int n = 0;
                        for (int x = x0; x <= x1 && x < W; x++) if (content(x, y)) n++;
                        if (n >= 2) { if (minY2 < 0) minY2 = y; maxY2 = y; }
                    }
                    Console.WriteLine("  范围 x " + x0 + "~" + x1 + "：内容 x " + minX + "~" + maxX +
                                      "（左边距 " + (minX - x0) + "，右边距 " + (x1 - maxX) + "）");
                    Console.WriteLine("     纵向(任意) " + minY + "~" + maxY + " 高 " + (maxY - minY + 1) +
                                      "   纵向(>=2px) " + minY2 + "~" + maxY2 + " 高 " + (maxY2 - minY2 + 1));
                    Console.Write("     底部 40 行: ");
                    for (int y = Math.Max(0, maxY2 - 40); y <= Math.Min(H - 1, maxY2 + 6); y++)
                    {
                        int n = 0;
                        for (int x = x0; x <= x1 && x < W; x++) if (content(x, y)) n++;
                        Console.Write(y + ":" + n + " ");
                    }
                    Console.WriteLine();
                }
                return;
            }

            // 列分段
            int[] colCount = new int[W];
            for (int x = 0; x < W; x++)
            {
                int n = 0;
                for (int y = 0; y < H; y++) if (content(x, y)) n++;
                colCount[x] = n;
            }
            var segs = new List<int[]>();
            int start = -1, gap = 0;
            for (int x = 0; x < W; x++)
            {
                bool has = colCount[x] >= 5;
                if (has) { if (start < 0) start = x; gap = 0; }
                else if (start >= 0)
                {
                    gap++;
                    if (gap > 18) { segs.Add(new int[] { start, x - gap }); start = -1; gap = 0; }
                }
            }
            if (start >= 0) segs.Add(new int[] { start, W - 1 - gap });

            Console.WriteLine("横向上有内容的列 " + Array.FindAll(colCount, c => c >= 5).Length + " 列；粗分段 " + segs.Count + " 段：");
            foreach (int[] s in segs) Console.WriteLine("   x " + s[0] + "~" + s[1] + "  宽 " + (s[1] - s[0] + 1));

            // 每段的纵向轮廓
            for (int i = 0; i < segs.Count; i++)
            {
                int x0 = segs[i][0], x1 = segs[i][1];
                Console.WriteLine("段 " + (i + 1) + "  x " + x0 + "~" + x1 + "：");
                int y0a = -1, y1a = -1, y0b = -1, y1b = -1;
                var prof = new List<int>();
                int bucket = 0;
                for (int y = 0; y < H; y++)
                {
                    int n = 0;
                    for (int x = x0; x <= x1; x++) if (content(x, y)) n++;
                    bucket += n;
                    if (y % 20 == 19) { prof.Add(bucket); bucket = 0; }
                    if (n >= 5) { if (y0a < 0) y0a = y; y1a = y; }
                    if (n >= 2) { if (y0b < 0) y0b = y; y1b = y; }
                }
                Console.WriteLine("   纵向(>=5px) " + y0a + "~" + y1a + "  高 " + (y1a - y0a + 1));
                Console.WriteLine("   纵向(>=2px) " + y0b + "~" + y1b + "  高 " + (y1b - y0b + 1));
                Console.Write("   每20行像素数: ");
                foreach (int v in prof) Console.Write(v + " ");
                Console.WriteLine();
                // 底部 60 行的明细，用来判断脚在哪
                Console.Write("   底部明细(y: n): ");
                for (int y = Math.Max(0, H - 60); y < H; y++)
                {
                    int n = 0;
                    for (int x = x0; x <= x1; x++) if (content(x, y)) n++;
                    Console.Write(y + ":" + n + " ");
                }
                Console.WriteLine();
            }
        }
    }
}
