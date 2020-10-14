﻿using System;
using System.Windows;
using System.Windows.Media;

namespace DrawToolsLib.Graphics
{
    [Serializable]
    public class GraphicArrow : GraphicLine
    {
        protected GraphicArrow()
        {
        }
        public GraphicArrow(DrawingCanvas canvas, Point start, Point end)
            : base(canvas, start, end)
        {
        }

        public GraphicArrow(Color objectColor, double lineWidth, Point start, Point end)
            : base(objectColor, lineWidth, start, end)
        {
        }

        protected override Geometry GetLineGeometry()
        {
            var tipLength = LineWidth * 8;
            var lineVector = LineEnd - LineStart;
            var lineLength = lineVector.Length;
            lineVector.Normalize();

            PathGeometry line = null;

            tipLength = Math.Min(lineLength / 3, tipLength);
            lineLength -= tipLength / 2;
            if (lineLength > 0)
            {
                var tmpLine = new LineGeometry(LineStart, LineStart + lineLength * lineVector);
                line = tmpLine.GetWidenedPathGeometry(new Pen(null, LineWidth));
            }

            const int tipAngle = 165;

            var rotate = Matrix.Identity;
            rotate.Rotate(tipAngle);
            var pt1 = LineEnd + rotate.Transform(lineVector * tipLength);
            rotate.Rotate(-tipAngle * 2);
            var pt2 = LineEnd + rotate.Transform(lineVector * tipLength);

            var arrow = new PathGeometry(new[] { new PathFigure(LineEnd, new[] { new LineSegment(pt2, true), new LineSegment(pt1, true) }, true) });

            return line == null ? (Geometry)arrow : new CombinedGeometry(GeometryCombineMode.Union, line, arrow);
        }

        public override GraphicBase Clone()
        {
            return new GraphicArrow(ObjectColor, LineWidth, LineStart, LineEnd) { ObjectId = ObjectId };
        }
    }
}
