using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace KnxTools.Geometry
{
    /// Плоский многоугольник по вершинам полилинии. Дуги — хордами.
    public class Polygon2d
    {
        public List<Point2d> Points { get; } = new List<Point2d>();
        public bool IsClosed { get; private set; }
        private double _minX, _minY, _maxX, _maxY;

        private Polygon2d(IEnumerable<Point2d> pts)
        {
            Points.AddRange(pts);
            // если контур завершён возвратом в первую точку — убираем дубль
            if (Points.Count > 3 && Points[0].GetDistanceTo(Points[Points.Count - 1]) < 1e-6)
                Points.RemoveAt(Points.Count - 1);

            _minX = _minY = double.MaxValue; _maxX = _maxY = double.MinValue;
            foreach (var p in Points)
            {
                if (p.X < _minX) _minX = p.X; if (p.X > _maxX) _maxX = p.X;
                if (p.Y < _minY) _minY = p.Y; if (p.Y > _maxY) _maxY = p.Y;
            }
        }

        public static Polygon2d FromPolyline(Polyline pl, Matrix3d transform)
        {
            var pts = new List<Point2d>();
            for (int i = 0; i < pl.NumberOfVertices; i++)
            {
                var p = pl.GetPoint3dAt(i).TransformBy(transform);
                pts.Add(new Point2d(p.X, p.Y));
            }
            return new Polygon2d(pts) { IsClosed = pl.Closed };
        }

        /// Любой тип полилинии: LWPOLYLINE, старая 2D POLYLINE, 3DPOLY. Иначе null.
        public static Polygon2d FromEntity(Entity ent, Transaction tr, Matrix3d transform)
        {
            if (ent is Polyline lw) return FromPolyline(lw, transform);

            var pts = new List<Point2d>();
            bool closed = false;
            if (ent is Polyline2d p2)
            {
                closed = p2.Closed;
                foreach (ObjectId vid in p2)
                {
                    var v = tr.GetObject(vid, OpenMode.ForRead) as Vertex2d;
                    if (v == null) continue;
                    var p = v.Position.TransformBy(transform);
                    pts.Add(new Point2d(p.X, p.Y));
                }
            }
            else if (ent is Polyline3d p3)
            {
                closed = p3.Closed;
                foreach (ObjectId vid in p3)
                {
                    var v = tr.GetObject(vid, OpenMode.ForRead) as PolylineVertex3d;
                    if (v == null) continue;
                    var p = v.Position.TransformBy(transform);
                    pts.Add(new Point2d(p.X, p.Y));
                }
            }
            else return null;

            return new Polygon2d(pts) { IsClosed = closed };
        }

        public static Polygon2d FromExtents(Extents3d e)
        {
            var pts = new[]
            {
                new Point2d(e.MinPoint.X, e.MinPoint.Y), new Point2d(e.MaxPoint.X, e.MinPoint.Y),
                new Point2d(e.MaxPoint.X, e.MaxPoint.Y), new Point2d(e.MinPoint.X, e.MaxPoint.Y)
            };
            return new Polygon2d(pts) { IsClosed = true };
        }

        public bool Contains(Point3d p) => Contains(new Point2d(p.X, p.Y));

        /// Метод луча: считаем пересечения рёбер горизонтальным лучом из точки.
        public bool Contains(Point2d p)
        {
            int n = Points.Count;
            if (n < 3) return false;
            if (p.X < _minX || p.X > _maxX || p.Y < _minY || p.Y > _maxY) return false;

            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var a = Points[i]; var b = Points[j];
                if ((a.Y > p.Y) != (b.Y > p.Y))
                {
                    double x = (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X;
                    if (p.X < x) inside = !inside;
                }
            }
            return inside;
        }

        public override string ToString() => $"{Points.Count} вершин, [{_minX:F0};{_minY:F0}]–[{_maxX:F0};{_maxY:F0}]";
    }
}