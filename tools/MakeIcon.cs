// 生成程序图标（多尺寸 .ico）＋ 样式对比图
//   用法1：make-icon <头像png> <输出ico> [tile|white|circle|plain]      默认 tile
//   用法2：make-icon --lab <头像png> <输出png>                          生成对比图（挑样式用）
// Windows Vista 以上支持 ICO 里直接放 PNG 压缩条目，透明通道完整、文件还小
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

static class MakeIcon
{
    static readonly int[] Sizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length >= 3 && args[0] == "--lab") { Lab(args[1], args[2]); return; }
        if (args.Length >= 4 && args[0] == "--one")
        {
            // 只生成一张合成好的 PNG（挑样式/看效果用，不写 ico）
            int size = args.Length >= 5 ? int.Parse(args[4]) : 256;
            using (var face = new Bitmap(args[1]))
            using (Bitmap b = Compose(face, size, args[2]))
                b.Save(args[3], ImageFormat.Png);
            Console.WriteLine("已生成 " + args[3]);
            return;
        }
        if (args.Length < 2) { Console.WriteLine("用法: make-icon <头像png> <输出ico> [tile|white|circle|plain|body]"); return; }

        string style = args.Length >= 3 ? args[2] : "tile";
        using (var face = new Bitmap(args[0]))
        {
            var entries = new List<byte[]>();
            bool[] asPng = new bool[Sizes.Length];
            for (int i = 0; i < Sizes.Length; i++)
            {
                int s = Sizes[i];
                using (Bitmap b = Compose(face, s, style))
                {
                    // 只有 256 用 PNG 压缩条目（微软官方就是这么做的）；16~128 一律用传统 BMP(DIB) 条目。
                    // Windows 外壳对 <=128 的 PNG 条目支持不稳定，会出现花屏/空白（踩过这个坑）。
                    bool png = s >= 256;
                    asPng[i] = png;
                    entries.Add(png ? EncodePng(b) : EncodeDib(b));
                }
            }
            WriteIco(args[1], entries, asPng);
            Console.WriteLine("已生成 " + args[1] + "（样式 " + style + "，" + Sizes.Length +
                              " 个尺寸：16~128 用 BMP 条目，256 用 PNG 条目）");
        }
    }

    static byte[] EncodePng(Bitmap b)
    {
        using (var ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); return ms.ToArray(); }
    }

    /// <summary>32bpp BGRA + AND 掩码 的传统 DIB 条目（Windows 外壳最认这个）</summary>
    static byte[] EncodeDib(Bitmap b)
    {
        int s = b.Width;
        int maskStride = ((s + 31) / 32) * 4;          // 1bpp，每行按 4 字节对齐
        int maskSize = maskStride * s;
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            // BITMAPINFOHEADER
            w.Write(40);            // biSize
            w.Write(s);             // biWidth
            w.Write(s * 2);         // biHeight = 2 倍（XOR + AND）
            w.Write((short)1);      // biPlanes
            w.Write((short)32);     // biBitCount
            w.Write(0);             // biCompression = BI_RGB
            w.Write(s * s * 4);     // biSizeImage
            w.Write(0); w.Write(0); w.Write(0); w.Write(0);

            // 像素：自下而上，每行 BGRA
            var rect = new Rectangle(0, 0, s, s);
            BitmapData d = b.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[d.Stride];
                for (int y = s - 1; y >= 0; y--)
                {
                    System.Runtime.InteropServices.Marshal.Copy(
                        (IntPtr)((long)d.Scan0 + (long)y * d.Stride), row, 0, d.Stride);
                    w.Write(row, 0, s * 4);
                }
            }
            finally { b.UnlockBits(d); }

            // AND 掩码：全 0（透明度交给 alpha 通道）
            w.Write(new byte[maskSize]);
            w.Flush();
            return ms.ToArray();
        }
    }

    /* ---------------- 画一张方形图标 ---------------- */

    static Bitmap Compose(Bitmap face, int size, string style)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        bmp.SetResolution(96f, 96f);
        using (var g = Graphics.FromImage(bmp))
        {
            g.PageUnit = GraphicsUnit.Pixel;           // 像素就是像素，别让 DPI 掺和
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            float r = size * 0.22f;                     // 圆角半径
            var full = new RectangleF(0.5f, 0.5f, size - 1f, size - 1f);

            if (style == "tile" || style == "body")
            {
                using (var path = Rounded(full, r))
                using (var br = new LinearGradientBrush(full, Color.FromArgb(255, 255, 231, 240), Color.FromArgb(255, 250, 178, 208), 90f))
                    g.FillPath(br, path);
                using (var path = Rounded(full, r))
                using (var p = new Pen(Color.FromArgb(120, 235, 150, 185), Math.Max(1f, size * 0.012f)))
                    g.DrawPath(p, path);
            }
            else if (style == "white")
            {
                using (var path = Rounded(full, r))
                using (var br = new SolidBrush(Color.FromArgb(255, 255, 253, 254)))
                    g.FillPath(br, path);
                using (var path = Rounded(full, r))
                using (var p = new Pen(Color.FromArgb(190, 240, 170, 200), Math.Max(1f, size * 0.02f)))
                    g.DrawPath(p, path);
            }
            else if (style == "circle")
            {
                var c = new RectangleF(size * 0.02f, size * 0.02f, size * 0.96f, size * 0.96f);
                using (var br = new LinearGradientBrush(c, Color.FromArgb(255, 255, 234, 243), Color.FromArgb(255, 246, 176, 208), 90f))
                    g.FillEllipse(br, c);
                using (var p = new Pen(Color.FromArgb(130, 235, 150, 185), Math.Max(1f, size * 0.014f)))
                    g.DrawEllipse(p, c);
            }

            // 她的脸：占方块的多少
            if (style == "body")
            {
                // 全身立绘：按高度装进方块，宽度等比、居中
                float hh = size * 0.94f;
                float ww = hh * face.Width / (float)face.Height;
                if (ww > size * 0.96f) { ww = size * 0.96f; hh = ww * face.Height / (float)face.Width; }
                g.DrawImage(face, new RectangleF((size - ww) / 2f, (size - hh) / 2f, ww, hh));
            }
            else
            {
                float scale = style == "tile" ? 0.90f
                            : style == "white" ? 0.94f
                            : style == "circle" ? 0.98f
                            : 1.14f;                    // plain：放大一点，做成「照片」感
                float d = size * scale;
                float x = (size - d) / 2f;
                float y = (size - d) / 2f + (style == "tile" ? size * 0.01f : 0f);
                g.DrawImage(face, new RectangleF(x, y, d, d));
            }
        }
        return bmp;
    }

    static GraphicsPath Rounded(RectangleF r, float radius)
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

    /* ---------------- 写 ICO ---------------- */

    static void WriteIco(string path, List<byte[]> entries, bool[] asPng)
    {
        using (var fs = File.Create(path))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((ushort)0);
            w.Write((ushort)1);
            w.Write((ushort)entries.Count);
            int offset = 6 + 16 * entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                int s = Sizes[i];
                w.Write((byte)(s >= 256 ? 0 : s));
                w.Write((byte)(s >= 256 ? 0 : s));
                w.Write((byte)0);
                w.Write((byte)0);
                w.Write((ushort)1);
                w.Write((ushort)32);
                w.Write((uint)entries[i].Length);
                w.Write((uint)offset);
                offset += entries[i].Length;
            }
            foreach (byte[] p in entries) w.Write(p);
        }
    }

    /* ---------------- 样式对比图 ---------------- */

    static void Lab(string facePath, string outPath)
    {
        string[] styles = new string[] { "tile", "white", "circle", "plain" };
        using (var face = new Bitmap(facePath))
        {
            int pad = 16, cell = 256;
            int W = pad + styles.Length * (cell + pad);
            int H = pad + cell + 60 + 60 + 24;
            using (var sheet = new Bitmap(W, H))
            using (var g = Graphics.FromImage(sheet))
            {
                sheet.SetResolution(96f, 96f);
                g.PageUnit = GraphicsUnit.Pixel;
                g.Clear(Color.FromArgb(246, 246, 249));
                using (var f = new Font("Microsoft YaHei UI", 10f))
                using (var fb = new SolidBrush(Color.FromArgb(40, 40, 48)))
                {
                    for (int i = 0; i < styles.Length; i++)
                    {
                        int x = pad + i * (cell + pad);
                        using (Bitmap big = Compose(face, cell, styles[i]))
                            g.DrawImage(big, new Rectangle(x, 40, cell, cell));
                        // 小尺寸预览（48 / 32 / 16），看缩小后还认不认得出
                        int sx = x;
                        foreach (int s in new int[] { 48, 32, 16 })
                        {
                            using (Bitmap sm = Compose(face, s, styles[i]))
                                g.DrawImage(sm, new Rectangle(sx, 40 + cell + 12, s, s));
                            sx += s + 10;
                        }
                        g.DrawString(styles[i], f, fb, x, 16);
                    }
                }
                sheet.Save(outPath, ImageFormat.Png);
            }
        }
        Console.WriteLine("对比图 -> " + outPath);
    }
}
