// 精灵图：加载 content\sprites\ 下的三视图 PNG，并按目标区域缩放/翻转绘制
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

class Sprite
{
    public const string Front = "front";
    public const string Side = "side";
    public const string Back = "back";

    public string Dir;
    public Bitmap Avatar;
    public Icon TrayIcon;

    Dictionary<string, Bitmap> map = new Dictionary<string, Bitmap>();

    /// <summary>备用素材目录：主目录缺图时用它（公开仓库里带的是原创占位形象）</summary>
    public string FallbackDir;
    /// <summary>这次是不是回退用了备用形象</summary>
    public bool UsedFallback;

    public Sprite(string dir) : this(dir, null) { }

    public Sprite(string dir, string fallbackDir)
    {
        Dir = dir;
        FallbackDir = fallbackDir;
        map[Front] = LoadPng("pet-front.png");
        map[Side] = LoadPng("pet-side.png");
        map[Back] = LoadPng("pet-back.png");
        Avatar = LoadPng("pet-avatar.png");
        try
        {
            IntPtr h = Avatar.GetHicon();
            TrayIcon = Icon.FromHandle(h);
        }
        catch { }
    }

    Bitmap LoadPng(string name)
    {
        string p = Path.Combine(Dir, name);
        if (!File.Exists(p) && !string.IsNullOrEmpty(FallbackDir))
        {
            string alt = Path.Combine(FallbackDir, name);
            if (File.Exists(alt)) { p = alt; UsedFallback = true; }
        }
        if (!File.Exists(p))
            throw new FileNotFoundException("找不到精灵图：" + p +
                (string.IsNullOrEmpty(FallbackDir) ? "" : "（备用目录 " + FallbackDir + " 里也没有）"));
        using (var src = new Bitmap(p))
        {
            int w = src.Width, h = src.Height;
            var copy = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            copy.SetResolution(96f, 96f);
            // 逐像素整块拷贝，**不要**用 Graphics.DrawImage/DrawImageUnscaled：
            // 在 150% 缩放的屏幕上，目标 Graphics 是 144 DPI 而图片是 96 DPI，
            // GDI+ 会按 1.5 倍把图片放大后裁到画布大小 —— 结果就是「人物只剩一半（一个大头）」。
            bool copied = false;
            try
            {
                BitmapData sd = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                BitmapData dd = copy.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    byte[] row = new byte[sd.Stride];
                    for (int y = 0; y < h; y++)
                    {
                        System.Runtime.InteropServices.Marshal.Copy((IntPtr)((long)sd.Scan0 + (long)y * sd.Stride), row, 0, sd.Stride);
                        System.Runtime.InteropServices.Marshal.Copy(row, 0, (IntPtr)((long)dd.Scan0 + (long)y * dd.Stride), dd.Stride);
                    }
                    copied = true;
                }
                finally
                {
                    src.UnlockBits(sd);
                    copy.UnlockBits(dd);
                }
            }
            catch { copied = false; }

            if (!copied)
            {
                // 兜底：显式给出像素矩形 + 像素单位，绕开 DPI 换算
                using (var g = Graphics.FromImage(copy))
                {
                    g.PageUnit = GraphicsUnit.Pixel;
                    g.DrawImage(src, new Rectangle(0, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel);
                }
            }
            return copy;
        }
    }

    public Bitmap Get(string pose)
    {
        Bitmap b;
        if (pose != null && map.TryGetValue(pose, out b)) return b;
        return map[Front];
    }

    public static string NextPose(string pose)
    {
        if (pose == Front) return Side;
        if (pose == Side) return Back;
        return Front;
    }

    public static string PoseLabel(string pose)
    {
        if (pose == Side) return "侧面";
        if (pose == Back) return "背面";
        return "正面";
    }

    /// <summary>按 dest 区域等比绘制；flip＝左右翻转；alpha＝整体透明度</summary>
    public static void Draw(Graphics g, Bitmap bmp, RectangleF dest, bool flip, float alpha)
    {
        if (bmp == null) return;
        GraphicsState st = g.Save();
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        if (alpha < 0.999f)
        {
            var cm = new ColorMatrix();
            cm.Matrix33 = alpha;
            using (var ia = new ImageAttributes())
            {
                ia.SetColorMatrix(cm);
                DrawCore(g, bmp, dest, flip, ia);
            }
        }
        else DrawCore(g, bmp, dest, flip, null);
        g.Restore(st);
    }

    static void DrawCore(Graphics g, Bitmap bmp, RectangleF dest, bool flip, ImageAttributes ia)
    {
        if (flip)
        {
            g.TranslateTransform(dest.X + dest.Width, dest.Y);
            g.ScaleTransform(-1f, 1f);
            DrawOne(g, bmp, new RectangleF(0, 0, dest.Width, dest.Height), ia);
        }
        else DrawOne(g, bmp, dest, ia);
    }

    /// <summary>排障探针：真正画下去的那一刻，把 Graphics 状态和两个矩形打出来</summary>
    public static Action<string> Probe;

    static void DrawOne(Graphics g, Bitmap bmp, RectangleF r, ImageAttributes ia)
    {
        if (Probe != null)
        {
            Matrix m = g.Transform;
            Probe("src=" + bmp.Width + "x" + bmp.Height +
                  " dest=" + r.X.ToString("0.#") + "," + r.Y.ToString("0.#") + "," + r.Width.ToString("0.#") + "," + r.Height.ToString("0.#") +
                  " pageUnit=" + g.PageUnit + " pageScale=" + g.PageScale + " dpi=" + g.DpiX + "x" + g.DpiY +
                  " clip=" + g.VisibleClipBounds.X.ToString("0.#") + "," + g.VisibleClipBounds.Y.ToString("0.#") + "," +
                  g.VisibleClipBounds.Width.ToString("0.#") + "," + g.VisibleClipBounds.Height.ToString("0.#") +
                  " m=[" + m.Elements[0].ToString("0.###") + "," + m.Elements[1].ToString("0.###") + "," +
                  m.Elements[2].ToString("0.###") + "," + m.Elements[3].ToString("0.###") + "," +
                  m.Elements[4].ToString("0.###") + "," + m.Elements[5].ToString("0.###") + "]" +
                  " ia=" + (ia != null));
            Probe = null;
        }
        var src = new RectangleF(0, 0, bmp.Width, bmp.Height);
        if (ia == null) g.DrawImage(bmp, r, src, GraphicsUnit.Pixel);
        else
        {
            // 带 ImageAttributes 的重载只接受整数矩形，四舍五入即可（只在她睡着时用来压一点透明度）
            var ri = new Rectangle((int)Math.Round(r.X), (int)Math.Round(r.Y),
                                   (int)Math.Round(r.Width), (int)Math.Round(r.Height));
            g.DrawImage(bmp, ri, 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, ia);
        }
    }
}
