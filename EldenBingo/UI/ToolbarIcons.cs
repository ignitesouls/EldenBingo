using System.Drawing.Drawing2D;

namespace EldenBingo.UI;

internal static class ToolbarIcons
{
    private static readonly Color Ink = Color.FromArgb(36, 32, 29);
    // Muted fills and dark outlines sit alongside the existing game-style icons.
    internal static Image QuickSettings { get; } = create(drawPalette);
    internal static Image Focus { get; } = create(drawFocus);
    internal static Image Search { get; } = create(drawSearch);

    private static Bitmap create(Action<Graphics> draw)
    {
        var image = new Bitmap(40, 40);
        using var graphics = Graphics.FromImage(image);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        draw(graphics);
        return image;
    }

    private static void drawPalette(Graphics g)
    {
        using var shape = new GraphicsPath();
        shape.AddBezier(33, 27, 40, 17, 31, 4, 20, 4);
        shape.AddBezier(20, 4, 8, 3, 2, 11, 3, 22);
        shape.AddBezier(3, 22, 4, 32, 14, 38, 24, 34);
        shape.AddBezier(24, 34, 30, 33, 24, 28, 28, 27);
        shape.AddBezier(28, 27, 30, 26, 31, 29, 33, 27);
        using var fill = new LinearGradientBrush(new Rectangle(3, 3, 34, 34), Color.FromArgb(213, 189, 137), Color.FromArgb(128, 94, 56), 75);
        using var outline = new Pen(Ink, 2);
        g.FillPath(fill, shape); g.DrawPath(outline, shape);
        var dots = new[] { (10, 10, Color.FromArgb(138, 55, 45)), (21, 8, Color.FromArgb(77, 104, 139)),
            (6, 20, Color.FromArgb(91, 117, 65)), (17, 22, Color.FromArgb(122, 80, 132)) };
        foreach (var (x, y, color) in dots)
        {
            using var paint = new SolidBrush(color);
            g.FillEllipse(paint, x, y, 7, 7); g.DrawEllipse(outline, x, y, 7, 7);
        }
    }

    private static void drawFocus(Graphics g)
    {
        using var outline = new Pen(Ink, 2);
        using var board = new SolidBrush(Color.FromArgb(88, 83, 73));
        g.FillRectangle(board, 7, 7, 26, 26); g.DrawRectangle(outline, 7, 7, 26, 26);
        for (var y = 0; y < 3; y++)
            for (var x = 0; x < 3; x++)
            {
                using var tile = new SolidBrush(x == 1 && y == 1 ? Color.FromArgb(213, 182, 99) : Color.FromArgb(161, 168, 161));
                g.FillRectangle(tile, 9 + x * 8, 9 + y * 8, 6, 6);
            }
        using var dark = new Pen(Ink, 4) { LineJoin = LineJoin.Round };
        using var gold = new Pen(Color.FromArgb(173, 144, 83), 2) { LineJoin = LineJoin.Round };
        var corners = new[] { new[] { new Point(10, 2), new Point(2, 2), new Point(2, 10) },
            new[] { new Point(30, 2), new Point(38, 2), new Point(38, 10) },
            new[] { new Point(2, 30), new Point(2, 38), new Point(10, 38) },
            new[] { new Point(30, 38), new Point(38, 38), new Point(38, 30) } };
        foreach (var corner in corners) { g.DrawLines(dark, corner); g.DrawLines(gold, corner); }
    }

    private static void drawSearch(Graphics g)
    {
        using var outline = new Pen(Ink, 8) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var grip = new Pen(Color.FromArgb(130, 101, 65), 4) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(outline, 25, 26, 34, 36); g.DrawLine(grip, 25, 26, 34, 36);
        using var glass = new LinearGradientBrush(new Rectangle(4, 3, 26, 26), Color.FromArgb(184, 207, 210), Color.FromArgb(80, 110, 124), 60);
        using var rim = new Pen(Color.FromArgb(172, 145, 91), 3);
        using var edge = new Pen(Ink, 2);
        g.FillEllipse(glass, 4, 3, 26, 26); g.DrawEllipse(rim, 5, 4, 24, 24); g.DrawEllipse(edge, 3, 2, 28, 28);
        using var highlight = new Pen(Color.FromArgb(180, 238, 241, 230), 2) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawArc(highlight, 9, 8, 15, 15, 190, 85);
    }
}
