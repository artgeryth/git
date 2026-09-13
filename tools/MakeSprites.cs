// 从「三视图角色设定图」切出三张透明 PNG 精灵图（+ 一张圆形头像）
// 改自 学习系统\tools\MakePet.cs，两处加固：
//   1) 纵向内容判定放宽（MIN_PIX 2），避免**浅色的腿和脚**被漏掉、人物被切成半截
//   2) 切完会打印每个人的范围，方便核对有没有切到手脚
// 编译：csc /target:exe /out:tools\make-sprites.exe tools\MakeSprites.cs /reference:System.Drawing.dll
// 用法：make-sprites.exe <设定图> <输出目录> [横向范围，如 59-376,392-791,809-1226]
//       不给范围就自动分段（注意：三个人的头发挨在一起时会被并成一段）
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

static class MakeSprites
{
    const int TOL = 238;          // 「接近白」阈值（含）：alpha 归 0
    const int SOFT = 224;         // SOFT..TOL 做半透明过渡
    const int CONTENT_TOL = 248;  // 判定「这一列/行有内容」的阈值
    const int MIN_PIX = 2;        // 一行至少这么多非白像素才算有内容（2 能保住细腿和脚）

    static void Main(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine("用法: make-sprites <设定图> <输出目录> [横向范围]"); return; }
        string src = args[0], outDir = args[1];
        Directory.CreateDirectory(outDir);

        using (var bmp = new Bitmap(src))
        {
            Console.WriteLine("源图 " + bmp.Width + "x" + bmp.Height);
            var segs = new List<int[]>();

            if (args.Length >= 3 && args[2].IndexOf('-') > 0)
            {
                foreach (string part in args[2].Split(','))
                {
                    string[] mm = part.Split('-');
                    if (mm.Length == 2) segs.Add(new int[] { int.Parse(mm[0]), int.Parse(mm[1]) });
                }
                Console.WriteLine("使用手动分段：" + args[2]);
            }
            else
            {
                bool[] colHas = new bool[bmp.Width];
                for (int x = 0; x < bmp.Width; x++)
                {
                    int n = 0;
                    for (int y = 0; y < bmp.Height; y++)
                    {
                        Color c = bmp.GetPixel(x, y);
                        if (c.R < CONTENT_TOL || c.G < CONTENT_TOL || c.B < CONTENT_TOL) n++;
                    }
                    colHas[x] = n >= 5;
                }
                int start = -1, gap = 0;
                const int MAXGAP = 18;
                for (int x = 0; x < bmp.Width; x++)
                {
                    if (colHas[x]) { if (start < 0) start = x; gap = 0; }
                    else if (start >= 0)
                    {
                        gap++;
                        if (gap > MAXGAP) { segs.Add(new int[] { start, x - gap }); start = -1; gap = 0; }
                    }
                }
                if (start >= 0) segs.Add(new int[] { start, bmp.Width - 1 - gap });
                segs.RemoveAll(delegate (int[] s) { return s[1] - s[0] < 80; });
            }

            Console.WriteLine("共 " + segs.Count + " 段");
            foreach (int[] s in segs) Console.WriteLine("  段: x " + s[0] + " ~ " + s[1] + " (宽 " + (s[1] - s[0] + 1) + ")");

            string[] names = { "side", "front", "back" };
            for (int i = 0; i < segs.Count && i < 3; i++)
            {
                int x0 = segs[i][0], x1 = segs[i][1];

                // 纵向范围：放宽到 MIN_PIX=2，别把细腿和脚切掉
                int y0 = -1, y1 = -1;
                for (int y = 0; y < bmp.Height; y++)
                {
                    int n = 0;
                    for (int x = x0; x <= x1; x++)
                    {
                        Color c = bmp.GetPixel(x, y);
                        if (c.R < CONTENT_TOL || c.G < CONTENT_TOL || c.B < CONTENT_TOL) n++;
                    }
                    if (n >= MIN_PIX) { if (y0 < 0) y0 = y; y1 = y; }
                }
                if (y0 < 0) continue;

                // 横向也再收紧一次（范围比人宽时，去掉两侧多余空白）
                int cx0 = -1, cx1 = -1;
                for (int x = x0; x <= x1; x++)
                {
                    int n = 0;
                    for (int y = y0; y <= y1; y++)
                    {
                        Color c = bmp.GetPixel(x, y);
                        if (c.R < CONTENT_TOL || c.G < CONTENT_TOL || c.B < CONTENT_TOL) n++;
                    }
                    if (n >= MIN_PIX) { if (cx0 < 0) cx0 = x; cx1 = x; }
                }
                if (cx0 >= 0) { x0 = cx0; x1 = cx1; }

                int pad = 6;
                x0 = Math.Max(0, x0 - pad); x1 = Math.Min(bmp.Width - 1, x1 + pad);
                y0 = Math.Max(0, y0 - pad); y1 = Math.Min(bmp.Height - 1, y1 + pad);
                int w = x1 - x0 + 1, h = y1 - y0 + 1;

                using (var cut = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(cut))
                        g.DrawImage(bmp, new Rectangle(0, 0, w, h), new Rectangle(x0, y0, w, h), GraphicsUnit.Pixel);
                    PunchOutBackground(cut);
                    string name = i < names.Length ? names[i] : "extra" + i;
                    string file = Path.Combine(outDir, "pet-" + name + ".png");
                    cut.Save(file, ImageFormat.Png);
                    Console.WriteLine("  " + name + ": 裁自 x " + x0 + "~" + x1 + ", y " + y0 + "~" + y1 +
                                      "  ->  " + w + "x" + h + "  " + file);
                }
            }
        }

        // 圆形头像：取正面图的头部
        string frontPath = Path.Combine(outDir, "pet-front.png");
        if (File.Exists(frontPath))
        {
            using (var f = new Bitmap(frontPath))
            {
                int r = Math.Min(f.Width / 2, (int)(f.Height * 0.28));
                int cx = f.Width / 2, cy = (int)(f.Height * 0.30);
                var rect = new Rectangle(Math.Max(0, cx - r), Math.Max(0, cy - r), r * 2, r * 2);
                using (var av = new Bitmap(r * 2, r * 2, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(av))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        using (var path = new GraphicsPath())
                        {
                            path.AddEllipse(0, 0, r * 2 - 1, r * 2 - 1);
                            g.SetClip(path);
                            g.DrawImage(f, new Rectangle(0, 0, r * 2, r * 2), rect, GraphicsUnit.Pixel);
                        }
                    }
                    av.Save(Path.Combine(outDir, "pet-avatar.png"), ImageFormat.Png);
                    Console.WriteLine("  avatar: " + (r * 2) + "x" + (r * 2));
                }
            }
        }
        Console.WriteLine("完成。");
    }

    /// <summary>从四边 flood fill 抠背景；白裙子因为不与边缘连通所以保留</summary>
    static void PunchOutBackground(Bitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height;
        BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int stride = data.Stride;
        byte[] px = new byte[stride * h];
        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, px, 0, px.Length);

        bool[] visited = new bool[w * h];
        Stack<int> stack = new Stack<int>();
        Action<int, int> push = delegate (int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int idx = y * w + x;
            if (visited[idx]) return;
            int o = y * stride + x * 4;
            if (px[o] >= SOFT && px[o + 1] >= SOFT && px[o + 2] >= SOFT)
            {
                visited[idx] = true;
                stack.Push(idx);
            }
        };
        for (int x = 0; x < w; x++) { push(x, 0); push(x, h - 1); }
        for (int y = 0; y < h; y++) { push(0, y); push(w - 1, y); }

        while (stack.Count > 0)
        {
            int idx = stack.Pop();
            int x = idx % w, y = idx / w;
            int o = y * stride + x * 4;
            int min = Math.Min(px[o], Math.Min(px[o + 1], px[o + 2]));
            int a = min >= TOL ? 0 : (int)(255.0 * (TOL - min) / (TOL - SOFT));
            if (a < 0) a = 0;
            if (a > 255) a = 255;
            px[o + 3] = (byte)a;
            push(x + 1, y); push(x - 1, y); push(x, y + 1); push(x, y - 1);
        }

        System.Runtime.InteropServices.Marshal.Copy(px, 0, data.Scan0, px.Length);
        bmp.UnlockBits(data);
    }
}
