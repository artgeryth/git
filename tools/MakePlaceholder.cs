// 生成「占位形象」：一只纯代码画出来的原创小团子（三视图 + 头像）
// 为什么需要它：仓库里不能带第三方角色立绘，但公开发布又必须"下载即可运行"，
// 所以配一套完全原创的默认形象（随 MIT 一起授权）。想换成自己的角色，把三视图丢进
// content\sheet\ 再跑 tools\make-sprites.exe 即可。
// 编译：csc /target:exe /out:tools\make-placeholder.exe tools\MakePlaceholder.cs /reference:System.Drawing.dll
// 用法：make-placeholder <输出目录>          例如 content\placeholder
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

static class MakePlaceholder
{
    const int SS = 4;              // 超采样倍数：先画大图再缩小，边缘才平滑
    const int OutW = 320, OutH = 460;

    // 配色
    static readonly Color Line = Color.FromArgb(255, 226, 152, 184);
    static readonly Color BodyTop = Color.FromArgb(255, 255, 253, 254);
    static readonly Color BodyBottom = Color.FromArgb(255, 255, 236, 244);
    static readonly Color Blush = Color.FromArgb(190, 255, 168, 197);
    static readonly Color Dark = Color.FromArgb(255, 122, 85, 102);
    static readonly Color Bow = Color.FromArgb(255, 255, 158, 196);
    static readonly Color BowDark = Color.FromArgb(255, 236, 120, 168);

    [STAThread]
    static void Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : "content/placeholder";
        Directory.CreateDirectory(outDir);
        foreach (string pose in new string[] { "front", "side", "back" })
            Render(pose, Path.Combine(outDir, "pet-" + pose + ".png"));
        MakeAvatar(Path.Combine(outDir, "pet-front.png"), Path.Combine(outDir, "pet-avatar.png"));
        Console.WriteLine("占位形象已生成到 " + outDir + "（front/side/back + avatar）");
    }

    static void Render(string pose, string path)
    {
        int W = OutW * SS, H = OutH * SS;
        using (var big = new Bitmap(W, H, PixelFormat.Format32bppArgb))
        {
            big.SetResolution(96f, 96f);
            using (var g = Graphics.FromImage(big))
            {
                g.PageUnit = GraphicsUnit.Pixel;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Body(g, pose, W, H);
            }
            using (var small = new Bitmap(OutW, OutH, PixelFormat.Format32bppArgb))
            {
                small.SetResolution(96f, 96f);
                using (var g2 = Graphics.FromImage(small))
                {
                    g2.PageUnit = GraphicsUnit.Pixel;
                    g2.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g2.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g2.DrawImage(big, new Rectangle(0, 0, OutW, OutH));
                }
                small.Save(path, ImageFormat.Png);
            }
        }
    }

    static void Body(Graphics g, string pose, int W, int H)
    {
        float cx = W * 0.5f;
        bool side = pose == "side", back = pose == "back";

        // ---- 参数 ----
        float bodyW = W * (side ? 0.66f : 0.80f);
        float bodyH = H * 0.58f;
        float bodyCY = H * 0.68f;
        float bodyCX = cx + (side ? W * 0.03f : 0f);
        float headW = W * (side ? 0.62f : 0.74f);
        float headH = H * 0.42f;
        float headCY = H * 0.35f;
        float headCX = cx + (side ? W * 0.05f : 0f);

        const float K = 0.5523f;   // 圆的贝塞尔近似系数

        // ---- 脚（先画，从身体底下露出来）----
        float footW = W * 0.22f, footH = H * 0.06f;
        using (var b = new SolidBrush(BodyBottom))
        using (var p = new Pen(Line, W * 0.013f))
        {
            float[] offs = side ? new float[] { -0.02f, 0.18f } : new float[] { -0.20f, 0.20f };
            foreach (float off in offs)
            {
                var r = new RectangleF(bodyCX + off * bodyW - footW / 2f, bodyCY + bodyH * 0.42f, footW, footH);
                g.FillEllipse(b, r); g.DrawEllipse(p, r);
            }
        }

        // ---- 一团到底的轮廓：头顶 → 右肩 → 右腰 → 底 → 左腰 → 左肩 → 回头顶 ----
        // （头、身用一个封闭路径画出来，才不会在连接处留下接缝）
        float topY = headCY - headH / 2f;
        float rhx = headCX + headW / 2f, rhy = headCY;
        float rbx = bodyCX + bodyW / 2f, rby = bodyCY;
        float botY = bodyCY + bodyH / 2f;
        float lbx = bodyCX - bodyW / 2f;
        float lhx = headCX - headW / 2f;

        using (var path = new GraphicsPath())
        {
            path.StartFigure();
            // 1) 头顶 → 右耳侧
            path.AddBezier(new PointF(headCX, topY),
                new PointF(headCX + headW / 2f * K, topY),
                new PointF(rhx, rhy - headH / 2f * K),
                new PointF(rhx, rhy));
            // 2) 右耳侧 → 右腰（脖子过渡）
            path.AddBezier(new PointF(rhx, rhy),
                new PointF(rhx, rhy + (rby - rhy) * 0.55f),
                new PointF(rbx - (rbx - rhx) * 0.55f, rby - (rby - rhy) * 0.55f),
                new PointF(rbx, rby));
            // 3) 右腰 → 底
            path.AddBezier(new PointF(rbx, rby),
                new PointF(rbx, rby + bodyH / 2f * K),
                new PointF(bodyCX + bodyW / 2f * K, botY),
                new PointF(bodyCX, botY));
            // 4) 底 → 左腰
            path.AddBezier(new PointF(bodyCX, botY),
                new PointF(bodyCX - bodyW / 2f * K, botY),
                new PointF(lbx, rby + bodyH / 2f * K),
                new PointF(lbx, rby));
            // 5) 左腰 → 左耳侧
            path.AddBezier(new PointF(lbx, rby),
                new PointF(lbx + (lbx - lhx) * 0.55f, rby - (rby - rhy) * 0.55f),
                new PointF(lhx, rhy + (rby - rhy) * 0.55f),
                new PointF(lhx, rhy));
            // 6) 左耳侧 → 头顶
            path.AddBezier(new PointF(lhx, rhy),
                new PointF(lhx, rhy - headH / 2f * K),
                new PointF(headCX - headW / 2f * K, topY),
                new PointF(headCX, topY));
            path.CloseFigure();

            var bounds = new RectangleF(bodyCX - bodyW / 2f, topY, bodyW, botY - topY);
            using (var br = new LinearGradientBrush(bounds, BodyTop, BodyBottom, 90f)) g.FillPath(br, path);
            using (var p = new Pen(Line, W * 0.015f)) g.DrawPath(p, path);
        }

        // ---- 小短手（画在身体之后，露在两侧）----
        float armW = W * 0.19f, armH = H * 0.12f;
        using (var b = new SolidBrush(BodyTop))
        using (var p = new Pen(Line, W * 0.013f))
        {
            var left = new RectangleF(bodyCX - bodyW * 0.52f, bodyCY - bodyH * 0.10f, armW, armH);
            g.FillEllipse(b, left); g.DrawEllipse(p, left);
            if (!side)
            {
                var right = new RectangleF(bodyCX + bodyW * 0.52f - armW, bodyCY - bodyH * 0.08f, armW, armH);
                g.FillEllipse(b, right); g.DrawEllipse(p, right);
            }
            else
            {
                var front = new RectangleF(bodyCX - bodyW * 0.34f, bodyCY + bodyH * 0.02f, armW * 0.9f, armH * 0.9f);
                g.FillEllipse(b, front); g.DrawEllipse(p, front);
            }
        }

        // ---- 头顶呆毛 ----
        using (var p = new Pen(Line, W * 0.014f))
        {
            p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
            float bx = headCX + (side ? headW * 0.12f : 0f);
            float by = topY + headH * 0.03f;
            using (var path = new GraphicsPath())
            {
                path.AddBezier(bx - W * 0.02f, by, bx + W * 0.05f, by - H * 0.10f,
                               bx + W * 0.11f, by - H * 0.15f, bx + W * 0.03f, by - H * 0.185f);
                g.DrawPath(p, path);
            }
        }

        // ---- 侧边蝴蝶结 ----
        {
            float sx = headCX + headW * 0.44f;
            float sy = headCY - headH * 0.10f;
            float s = W * 0.14f;
            using (var b = new SolidBrush(Bow))
            using (var bd = new SolidBrush(BowDark))
            using (var p = new Pen(Line, W * 0.010f))
            {
                var l = new[] { new PointF(sx, sy), new PointF(sx - s, sy - s * 0.66f), new PointF(sx - s * 0.95f, sy + s * 0.66f) };
                var r = new[] { new PointF(sx, sy), new PointF(sx + s, sy - s * 0.66f), new PointF(sx + s * 0.95f, sy + s * 0.66f) };
                g.FillPolygon(b, l); g.DrawPolygon(p, l);
                g.FillPolygon(bd, r); g.DrawPolygon(p, r);
                g.FillEllipse(bd, sx - s * 0.19f, sy - s * 0.19f, s * 0.38f, s * 0.38f);
            }
        }

        // ---- 表情（背面不画）----
        if (!back)
        {
            float eyeY = headCY + headH * 0.10f;
            float eyeDX = headW * (side ? 0.12f : 0.21f);
            float eyeW = headW * 0.24f, eyeH = headH * 0.22f;
            using (var p = new Pen(Dark, W * 0.019f))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                foreach (float sx in (side ? new float[] { 1f } : new float[] { -1f, 1f }))
                {
                    float x = headCX + sx * eyeDX;
                    using (var path = new GraphicsPath())
                    {
                        // 弯弯的笑眼
                        path.AddArc(x - eyeW / 2f, eyeY - eyeH * 0.35f, eyeW, eyeH, 200f, 140f);
                        g.DrawPath(p, path);
                    }
                }
            }
            // 腮红
            using (var b = new SolidBrush(Blush))
            {
                float by = eyeY + headH * 0.20f, bw = headW * 0.21f, bh = headH * 0.13f;
                foreach (float sx in (side ? new float[] { 0.52f } : new float[] { -0.58f, 0.58f }))
                    g.FillEllipse(b, headCX + sx * headW * 0.5f - bw / 2f + (side ? headW * 0.05f : 0f), by, bw, bh);
            }
            // 小小的微笑
            using (var p = new Pen(Dark, W * 0.015f))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                float mx = headCX + (side ? headW * 0.06f : 0f);
                float my = eyeY + headH * 0.26f;
                float mw = headW * 0.16f, mh = headH * 0.12f;
                using (var path = new GraphicsPath())
                {
                    path.AddArc(mx - mw / 2f, my - mh / 2f, mw, mh, 25f, 130f);
                    g.DrawPath(p, path);
                }
            }
        }
    }

    /// <summary>圆形头像：取正面图的头部</summary>
    static void MakeAvatar(string frontPath, string avatarPath)
    {
        using (var f = new Bitmap(frontPath))
        {
            int r = (int)(f.Width * 0.46f);
            int cx = f.Width / 2, cy = (int)(f.Height * 0.36f);
            var rect = new Rectangle(Math.Max(0, cx - r), Math.Max(0, cy - r), r * 2, r * 2);
            using (var av = new Bitmap(r * 2, r * 2, PixelFormat.Format32bppArgb))
            {
                av.SetResolution(96f, 96f);
                using (var g = Graphics.FromImage(av))
                {
                    g.PageUnit = GraphicsUnit.Pixel;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var path = new GraphicsPath())
                    {
                        path.AddEllipse(0, 0, r * 2 - 1, r * 2 - 1);
                        g.SetClip(path);
                        g.DrawImage(f, new Rectangle(0, 0, r * 2, r * 2), rect, GraphicsUnit.Pixel);
                    }
                }
                av.Save(avatarPath, ImageFormat.Png);
            }
        }
    }
}
