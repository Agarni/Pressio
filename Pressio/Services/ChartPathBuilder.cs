using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace Pressio.Services;

public static class ChartPathBuilder
{
    public static Geometry BuildSmooth(IReadOnlyList<Point> points)
    {
        var geometry = new StreamGeometry();
        if (points.Count == 0) return geometry;

        using var ctx = geometry.Open();
        if (points.Count == 1)
        {
            ctx.BeginFigure(points[0], false);
            ctx.LineTo(points[0]);
            return geometry;
        }

        ctx.BeginFigure(points[0], false);
        for (var i = 0; i < points.Count - 1; i++)
        {
            var p0 = points[Math.Max(0, i - 1)];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = points[Math.Min(points.Count - 1, i + 2)];
            var c1 = new Point(p1.X + (p2.X - p0.X) / 6d, p1.Y + (p2.Y - p0.Y) / 6d);
            var c2 = new Point(p2.X - (p3.X - p1.X) / 6d, p2.Y - (p3.Y - p1.Y) / 6d);
            ctx.CubicBezierTo(c1, c2, p2);
        }
        return geometry;
    }
}
