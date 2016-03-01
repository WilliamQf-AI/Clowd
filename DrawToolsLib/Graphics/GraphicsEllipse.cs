using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DrawToolsLib.Graphics
{
    [Serializable]
    public class GraphicsEllipse : GraphicsRectangle
    {
        protected GraphicsEllipse()
        {
        }
        public GraphicsEllipse(DrawingCanvas canvas, Rect rect) : base(canvas, rect)
        {
        }

        public GraphicsEllipse(DrawingCanvas canvas, Rect rect, bool filled) : base(canvas, rect, filled)
        {
        }

        public GraphicsEllipse(double scale, Color objectColor, double lineWidth, Rect rect) : base(scale, objectColor, lineWidth, rect)
        {
        }

        public GraphicsEllipse(double scale, Color objectColor, double lineWidth, Rect rect, bool filled) : base(scale, objectColor, lineWidth, rect, filled)
        {
        }


        internal override void DrawRectangle(DrawingContext drawingContext)
        {
            if (drawingContext == null)
                throw new ArgumentNullException(nameof(drawingContext));

            Rect r = Bounds;
            Point center = new Point(
                (r.Left + r.Right) / 2.0,
                (r.Top + r.Bottom) / 2.0);

            double radiusX = (r.Right - r.Left) / 2.0;
            double radiusY = (r.Bottom - r.Top) / 2.0;

            var brush = new SolidColorBrush(ObjectColor);
            drawingContext.DrawEllipse(
                Filled ? brush : null,
                new Pen(brush, ActualLineWidth),
                center,
                radiusX,
                radiusY);
        }

        internal override bool Contains(Point point)
        {
            if (IsSelected)
                return Bounds.Contains(point);

            EllipseGeometry g = new EllipseGeometry(Bounds);
            return g.FillContains(point) || g.StrokeContains(new Pen(Brushes.Black, ActualLineWidth), point);
        }

        internal override bool IntersectsWith(Rect rectangle)
        {
            RectangleGeometry rg = new RectangleGeometry(rectangle);
            EllipseGeometry eg = new EllipseGeometry(Bounds);
            PathGeometry p = Geometry.Combine(rg, eg, GeometryCombineMode.Intersect, null);

            return !p.IsEmpty();
        }

        public override GraphicsBase Clone()
        {
            return new GraphicsEllipse(ActualScale, ObjectColor, LineWidth, Bounds, Filled) {ObjectId = ObjectId};
        }
    }
}