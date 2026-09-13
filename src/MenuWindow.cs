// 悬停菜单（自己画的，比系统菜单好看，也不会抢焦点）
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

class MenuWindow : LayeredWindow
{
    public class Item
    {
        public string Id;
        public string Label;
        public Item(string id, string label) { Id = id; Label = label; }
    }

    public event Action<string> ItemChosen;

    App App2;
    List<Item> items = new List<Item>();
    int hoverIndex = -1;
    float S = 1f;
    int itemH = 30;

    public MenuWindow(App app)
    {
        App2 = app;
        S = app.S;
        itemH = (int)Math.Round(30 * S);
    }

    public void ShowItems(List<Item> its, int x, int y, int w, int h)
    {
        items = its;
        S = App2.S;
        itemH = (int)Math.Round(30 * S);
        int need = itemH * items.Count + (int)Math.Round(16 * S);
        if (h < need) h = need;
        SetBounds(x, y, w, h);
        hoverIndex = -1;
        if (!Visible) Show();
        Redraw();
    }

    protected override void Render(Graphics g)
    {
        float pad = 8 * S;
        var rect = new RectangleF(1 * S, 1 * S, Width - 2 * S, Height - 2 * S);
        using (var path = RoundedRect(rect, 12 * S))
        {
            using (var b = new SolidBrush(Color.FromArgb(238, 252, 250, 252))) g.FillPath(b, path);
            using (var p = new Pen(Color.FromArgb(235, 200, 150, 180), 1.4f * S)) g.DrawPath(p, path);
        }

        using (var font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point))
        {
            for (int i = 0; i < items.Count; i++)
            {
                float y = pad + i * itemH;
                var ir = new RectangleF(pad, y + 1 * S, Width - pad * 2f, itemH - 2 * S);
                if (i == hoverIndex)
                {
                    using (var hp = RoundedRect(ir, 8 * S))
                    using (var b = new SolidBrush(Color.FromArgb(255, 255, 226, 238)))
                        g.FillPath(b, hp);
                }
                TextRenderer.DrawText(g, items[i].Label, font,
                    new Rectangle((int)ir.X + (int)(8 * S), (int)ir.Y, (int)ir.Width - (int)(8 * S), (int)ir.Height),
                    i == hoverIndex ? Color.FromArgb(255, 190, 60, 110) : Color.FromArgb(255, 66, 56, 62),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
        }
    }

    int IndexAt(Point p)
    {
        float pad = 8 * S;
        int i = (int)Math.Floor((p.Y - pad) / (float)itemH);
        if (i < 0) return -1;
        if (i >= items.Count) return -1;
        if (p.X < pad - 4 || p.X > Width - pad + 4) return -1;
        return i;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int idx = IndexAt(e.Location);
        if (idx != hoverIndex) { hoverIndex = idx; Redraw(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hoverIndex = -1;
        Redraw();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        int idx = IndexAt(e.Location);
        if (idx >= 0 && ItemChosen != null) ItemChosen(items[idx].Id);
    }
}
