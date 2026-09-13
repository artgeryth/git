// 排障用：打印一张 PNG 里「非透明像素」的包围盒（含每列/每行的分布摘要）
// 编译：csc /target:exe /out:tools\bbox.exe tools\BBox.cs /reference:System.Drawing.dll
// 用法：bbox.exe <png> [--grid 8]
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

static class BBox
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length < 1) { Console.WriteLine("用法: bbox.exe <png>"); return; }
        int grid = 0;
        for (int i = 1; i < args.Length - 1; i++) if (args[i] == "--grid") grid = int.Parse(args[i + 1]);

        using (var bmp = new Bitmap(args[0]))
        {
            int W = bmp.Width, H = bmp.Height;
            var rect = new Rectangle(0, 0, W, H);
            BitmapData d = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int minX = W, minY = H, maxX = -1, maxY = -1;
            long count = 0;
            int[] colCount = new int[W];
            int[] rowCount = new int[H];
            try
            {
                byte[] px = new byte[d.Stride];
                for (int y = 0; y < H; y++)
                {
                    Marshal.Copy((IntPtr)((long)d.Scan0 + (long)y * d.Stride), px, 0, d.Stride);
                    for (int x = 0; x < W; x++)
                    {
                        if (px[x * 4 + 3] > 8)
                        {
                            count++;
                            colCount[x]++;
                            rowCount[y]++;
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                }
            }
            finally { bmp.UnlockBits(d); }

            Console.WriteLine(args[0]);
            Console.WriteLine("  画布 " + W + "x" + H + "，非透明像素 " + count);
            if (maxX < 0) { Console.WriteLine("  整张图全透明"); return; }
            Console.WriteLine("  包围盒 x=" + minX + ".." + maxX + " (宽 " + (maxX - minX + 1) + ")  y=" + minY + ".." + maxY + " (高 " + (maxY - minY + 1) + ")");
            if (grid > 0)
            {
                Console.Write("  每行像素（按 " + grid + " 行合并）: ");
                for (int y = 0; y < H; y += grid)
                {
                    int s = 0;
                    for (int k = y; k < Math.Min(H, y + grid); k++) s += rowCount[k];
                    Console.Write(s + " ");
                }
                Console.WriteLine();
                Console.Write("  每列像素（按 " + grid + " 列合并）: ");
                for (int x = 0; x < W; x += grid)
                {
                    int s = 0;
                    for (int k = x; k < Math.Min(W, x + grid); k++) s += colCount[k];
                    Console.Write(s + " ");
                }
                Console.WriteLine();
            }
        }
    }
}
