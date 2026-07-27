using horof.Models;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using Microsoft.Maui.Graphics;

namespace horof.Controls;

/// <summary>
/// Draws the game canvas: diagonal orange/green frame around the hex cluster, then the letter tiles.
/// </summary>
public class HexBoardDrawable : IDrawable
{
    private const float Sqrt3 = 1.7320508f;

    public IReadOnlyList<HexCell>? Cells { get; set; }
    public int? SelectedIndex { get; set; }
    public bool CanSelect { get; set; } = true;

    public void InvalidateLayout()
    {
        _lastLayoutRect = default;
        _centers.Clear();
    }

    private float _hexSize = 36f;
    private RectF _lastLayoutRect;
    /// <summary>Bounding box of the outermost hexes (used as the inner edge of the colored frame).</summary>
    private RectF _gridBounds;
    private readonly Dictionary<int, PointF> _centers = new();

    private static readonly Color FrameOrange = Color.FromArgb("#F57C00");
    private static readonly Color FrameGreen = Color.FromArgb("#2E7D32");
    private static readonly Color GridCenter = Color.FromArgb("#F3F6F0");

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Cells is null || Cells.Count == 0)
            return;

        // Compute hex centers + outer bounds of the cluster (centered in the canvas).
        EnsureLayout(dirtyRect);
        canvas.Antialias = true;

        // 1) Background: four triangular color regions + light fill under the grid.
        DrawDiagonalCornerFrame(canvas, dirtyRect);

        // 2) Foreground: each letter hex (fill, border, Arabic character).
        foreach (var cell in Cells)
        {
            if (!_centers.TryGetValue(cell.Index, out var center))
                continue;

            var path = CreateFlatTopHexPath(center, _hexSize);

            // Cell fill: yellow if selected, team color if owned, otherwise off-white.
            canvas.FillColor = GetFillColor(cell);
            canvas.FillPath(path);
            canvas.StrokeColor = Color.FromArgb("#000000");
            canvas.StrokeSize = 1.5f;
            canvas.DrawPath(path);

            // Letter centered inside the hex.
            canvas.FontColor = Color.FromArgb("#1A237E");
            canvas.FontSize = _hexSize * 0.72f;
            canvas.DrawString(
                cell.Letter.ToString(),
                center.X,
                center.Y,
                HorizontalAlignment.Center);
        }
    }

    public bool HitTest(PointF point, RectF layoutRect, out int hexIndex)
    {
        hexIndex = -1;
        if (Cells is null || Cells.Count == 0)
            return false;

        EnsureLayout(layoutRect);

        // Prefer lower/later cells when hit areas could overlap slightly.
        foreach (var cell in Cells.OrderByDescending(c => c.Row).ThenByDescending(c => c.Col))
        {
            if (!_centers.TryGetValue(cell.Index, out var center))
                continue;

            if (PointInFlatTopHex(point, center, _hexSize * 0.92f))
            {
                hexIndex = cell.Index;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Lays out a flat-top honeycomb, then centers the whole cluster in <paramref name="rect"/>.
    /// Sets <see cref="_centers"/> and <see cref="_gridBounds"/>.
    /// </summary>
    private void EnsureLayout(RectF rect)
    {
        if (Cells is null || Cells.Count == 0)
            return;

        if (Math.Abs(rect.Width - _lastLayoutRect.Width) < 0.5f
            && Math.Abs(rect.Height - _lastLayoutRect.Height) < 0.5f
            && _centers.Count == Cells.Count)
            return;

        _lastLayoutRect = rect;
        _centers.Clear();

        // Flat-top hex: width = √3·r, vertical step between rows = 1.5·r.
        var hexWidth = Sqrt3;
        var sizeFromWidth = rect.Width / ((BoardLayout.Cols - 0.5f) * hexWidth);
        var sizeFromHeight = rect.Height / (2f + (BoardLayout.Rows - 1) * 1.5f);
        _hexSize = MathF.Min(sizeFromWidth, sizeFromHeight) * 0.9f;

        var stepX = Sqrt3 * _hexSize;
        var stepY = 1.5f * _hexSize;

        // Local positions before centering (odd rows staggered half a column).
        var localCenters = new List<PointF>(Cells.Count);
        foreach (var cell in Cells)
        {
            var stagger = (cell.Row & 1) == 1 ? stepX / 2f : 0f;
            var cx = cell.Col * stepX + stagger + _hexSize;
            var cy = _hexSize + cell.Row * stepY;
            localCenters.Add(new PointF(cx, cy));
        }

        // Outer AABB of all hex vertices (approximate with circumradius).
        var minX = localCenters.Min(p => p.X - _hexSize);
        var maxX = localCenters.Max(p => p.X + _hexSize);
        var minY = localCenters.Min(p => p.Y - _hexSize);
        var maxY = localCenters.Max(p => p.Y + _hexSize);

        var gridW = maxX - minX;
        var gridH = maxY - minY;
        var offsetX = rect.X + (rect.Width - gridW) / 2f - minX;
        var offsetY = rect.Y + (rect.Height - gridH) / 2f - minY;

        for (var i = 0; i < Cells.Count; i++)
        {
            var cell = Cells[i];
            var local = localCenters[i];
            _centers[cell.Index] = new PointF(local.X + offsetX, local.Y + offsetY);
        }

        // Inner rectangle that the colored frame diagonals meet (outermost hex corners).
        _gridBounds = new RectF(
            minX + offsetX,
            minY + offsetY,
            maxX - minX,
            maxY - minY);
    }
    private static List<PointF> GetFlatTopHexPoints(PointF center, float radius)
    {
        var points = new List<PointF>(6);

        for (int i = 0; i < 6; i++)
        {
            float angle = MathF.PI / 180f * (60 * i - 30);

            float x = center.X + radius * MathF.Cos(angle);
            float y = center.Y + radius * MathF.Sin(angle);

            points.Add(new PointF(x, y));
        }

        return points;
    }
    /// <summary>
    /// Draws the four triangular color regions that frame the hex cluster:
    /// orange on top/bottom edges, green on left/right edges.
    /// Each region runs from a canvas edge to the matching side of <see cref="_gridBounds"/>.
    /// </summary>
    private void DrawDiagonalCornerFrame(ICanvas canvas, RectF canvasRect)
    {
        if (_gridBounds.Width <= 0 || _gridBounds.Height <= 0)
            return;
        _centers.TryGetValue(Cells[0].Index, out var center);
        var tlhexPoints = GetFlatTopHexPoints(center, _hexSize);
        var tl = tlhexPoints[4];      // top-left of hex cluster


        _centers.TryGetValue(Cells[4].Index, out center);
        var trhexPoints = GetFlatTopHexPoints(center, _hexSize);
        var tr = trhexPoints[0];     // top-right of hex cluster


        _centers.TryGetValue(Cells[24].Index, out center);
        var brhexPoints = GetFlatTopHexPoints(center, _hexSize);
        var br = brhexPoints[1];  // bottom-right of hex cluster


        _centers.TryGetValue(Cells[20].Index, out center);
        var blhexPoints = GetFlatTopHexPoints(center, _hexSize);
        var bl = blhexPoints[3];   // bottom-left of hex cluster

        // --- Center panel under the hexes (light fill inside the frame) ---
        canvas.FillColor = FrameGreen;
        canvas.FillRectangle(_gridBounds);

        // --- TOP: orange region (canvas top edge → top edge of hex cluster) ---
        canvas.FillColor = FrameOrange;
        FillQuad(canvas,
            canvasRect.Left, canvasRect.Top,     // canvas top-left
            canvasRect.Right, canvasRect.Top,    // canvas top-right
            tr.X, tr.Y,                          // hex cluster top-right
            tl.X, tl.Y);                         // hex cluster top-left

        // --- BOTTOM: orange region (canvas bottom edge → bottom edge of hex cluster) ---
        FillQuad(canvas,
            canvasRect.Left, canvasRect.Bottom,  // canvas bottom-left
            canvasRect.Right, canvasRect.Bottom, // canvas bottom-right
            br.X, br.Y,                          // hex cluster bottom-right
            bl.X, bl.Y);                         // hex cluster bottom-left

        // --- LEFT: green region (canvas left edge → left edge of hex cluster) ---
        canvas.FillColor = FrameGreen;
        FillQuad(canvas,
            canvasRect.Left, canvasRect.Top,     // canvas top-left
            tl.X, tl.Y,                          // hex cluster top-left
            bl.X, bl.Y,                          // hex cluster bottom-left
            canvasRect.Left, canvasRect.Bottom); // canvas bottom-left

        // --- RIGHT: green region (canvas right edge → right edge of hex cluster) ---
        FillQuad(canvas,
            canvasRect.Right, canvasRect.Top,    // canvas top-right
            tr.X, tr.Y,                          // hex cluster top-right
            br.X, br.Y,                          // hex cluster bottom-right
            canvasRect.Right, canvasRect.Bottom); // canvas bottom-right
    }

    /// <summary>Fills a four-point polygon (used for each colored frame region).</summary>
    private static void FillQuad(
        ICanvas canvas,
        float x1, float y1,
        float x2, float y2,
        float x3, float y3,
        float x4, float y4)
    {
        var path = new PathF();
        path.MoveTo(x1, y1);
        path.LineTo(x2, y2);
        path.LineTo(x3, y3);
        path.LineTo(x4, y4);
        path.Close();
        canvas.FillPath(path);
    }

    /// <summary>Builds a flat-top hexagon path around <paramref name="center"/>.</summary>
    private static PathF CreateFlatTopHexPath(PointF center, float radius)
    {
        var path = new PathF();
        for (var i = 0; i < 6; i++)
        {
            var angle = MathF.PI / 180f * (60 * i - 30);
            var px = center.X + radius * MathF.Cos(angle);
            var py = center.Y + radius * MathF.Sin(angle);
            if (i == 0)
                path.MoveTo(px, py);
            else
                path.LineTo(px, py);
        }

        path.Close();
        return path;
    }

    private static bool PointInFlatTopHex(PointF p, PointF center, float radius)
    {
        var dx = MathF.Abs(p.X - center.X);
        var dy = MathF.Abs(p.Y - center.Y);
        var halfW = radius * (Sqrt3 / 2f);
        if (dx > halfW || dy > radius)
            return false;

        return halfW - dx >= dy / Sqrt3;
    }

    private Color GetFillColor(HexCell cell)
    {
        // Selected letter → yellow (#FBFD3C)
        if (SelectedIndex == cell.Index)
            return Color.FromArgb("#FBFD3C");

        // Claimed by green / orange team
        if (cell.Owner == Team.Green)
            return FrameGreen;
        if (cell.Owner == Team.Orange)
            return FrameOrange;

        // Unclaimed
        return Color.FromArgb("#FAFAFA");
    }
}
