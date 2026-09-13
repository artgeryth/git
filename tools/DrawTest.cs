// 隔离测试：直接调用 src\Sprite.cs 里的绘制代码，把立绘画进一个 540x639 的位图
// 用来区分「是 Sprite.Draw 画错了」还是「PetWindow 里的坐标/变换不对」
// 编译：csc /target:exe /out:tools\drawtest.exe tools\DrawTest.cs src\Sprite.cs /reference:System.Drawing.dll
// 用法：drawtest.exe <立绘png> <输出png> [画布宽 高 目标x 目标y 目标宽 目标高]
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

static class DrawTest
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine("用法: drawtest <立绘png> <输出png> [W H x y w h]"); return; }
        string spritePath = args[0], outPath = args[1];
        Sprite.Probe = delegate (string s) { Console.WriteLine("  [探针] " + s); };
        int W = args.Length > 2 ? int.Parse(args[2]) : 540;
        int H = args.Length > 3 ? int.Parse(args[3]) : 639;
        int x = args.Length > 4 ? int.Parse(args[4]) : 137;
        int y = args.Length > 5 ? int.Parse(args[5]) : 230;
        int w = args.Length > 6 ? int.Parse(args[6]) : 266;
        int h = args.Length > 7 ? int.Parse(args[7]) : 390;

        using (var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb))
        {
            bmp.SetResolution(96f, 96f);
            using (var g = Graphics.FromImage(bmp))
            {
                g.PageUnit = GraphicsUnit.Pixel;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                using (var img = new Bitmap(spritePath))
                {
                    Console.WriteLine("立绘 " + img.Width + "x" + img.Height +
                                      "  ->  目标 " + w + "x" + h + " @ (" + x + "," + y + ")  画布 " + W + "x" + H +
                                      "  dpi=" + g.DpiX + " pageUnit=" + g.PageUnit);
                    Sprite.Draw(g, img, new RectangleF(x, y, w, h), false, 1f);
                }
            }
            bmp.Save(outPath, ImageFormat.Png);
        }
        Console.WriteLine("已写出 " + outPath);
    }
}
