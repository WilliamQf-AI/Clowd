using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;



namespace DrawToolsLib
{
    /// <summary>
    ///  Arrow graphics object.
    /// </summary>
    public class GraphicsArrow : GraphicsBase
    {
        #region Class Members

        protected Point lineStart;
        protected Point lineEnd;

        #endregion Class Members

        #region Constructors

        public GraphicsArrow(Point start, Point end, double lineWidth, Color objectColor, double actualScale)
        {
            this.lineStart = start;
            this.lineEnd = end;
            this.graphicsLineWidth = lineWidth;
            this.graphicsObjectColor = objectColor;
            this.graphicsActualScale = actualScale;

            //RefreshDrawng();
        }

        public GraphicsArrow()
            :
            this(new Point(0.0, 0.0), new Point(100.0, 100.0), 1.0, Colors.Black, 1.0)
        {
        }

        #endregion Constructors

        #region Properties

        public Point Start
        {
            get { return lineStart; }
            set { lineStart = value; }
        }

        public Point End
        {
            get { return lineEnd; }
            set { lineEnd = value; }
        }

        #endregion Properties

        #region Overrides

        /// <summary>
        /// Draw object
        /// </summary>
        public override void Draw(DrawingContext drawingContext)
        {
            if (drawingContext == null)
            {
                throw new ArgumentNullException("drawingContext");
            }

            var tipLength = ActualLineWidth * 8;
            var lineVector = lineEnd - lineStart;
            var lineLength = lineVector.Length;
            lineVector.Normalize();

            tipLength = Math.Min(lineLength / 3, tipLength);
            lineLength -= tipLength / 2;
            if (lineLength > 0)
            {
                drawingContext.DrawLine(
                    new Pen(new SolidColorBrush(ObjectColor), ActualLineWidth),
                    lineStart,
                    lineStart + lineLength * lineVector);
            }

            var rotate = Matrix.Identity;
            rotate.Rotate(165);
            var pt1 = lineEnd + rotate.Transform(lineVector * tipLength);
            rotate.Rotate(-165 * 2);
            var pt2 = lineEnd + rotate.Transform(lineVector * tipLength);
            drawingContext.DrawGeometry(new SolidColorBrush(ObjectColor), new Pen(new SolidColorBrush(ObjectColor), 1),
                new PathGeometry(new[] { new PathFigure(lineEnd, new[] { new LineSegment(pt2, true), new LineSegment(pt1, true) }, true) }));

            base.Draw(drawingContext);
    }

    /// <summary>
    /// Test whether object contains point
    /// </summary>
    public override bool Contains(Point point)
    {
        LineGeometry g = new LineGeometry(lineStart, lineEnd);

        return g.StrokeContains(new Pen(Brushes.Black, LineHitTestWidth), point);
    }

    /// <summary>
    /// XML serialization support
    /// </summary>
    /// <returns></returns>
    public override PropertiesGraphicsBase CreateSerializedObject()
    {
        return new PropertiesGraphicsArrow(this);
    }

    /// <summary>
    /// Get number of handles
    /// </summary>
    public override int HandleCount
    {
        get
        {
            return 2;
        }
    }

    /// <summary>
    /// Get handle point by 1-based number
    /// </summary>
    public override Point GetHandle(int handleNumber)
    {
        if (handleNumber == 1)
            return lineStart;
        else
            return lineEnd;
    }

    /// <summary>
    /// Hit test.
    /// Return value: -1 - no hit
    ///                0 - hit anywhere
    ///                > 1 - handle number
    /// </summary>
    public override int MakeHitTest(Point point)
    {
        if (IsSelected)
        {
            for (int i = 1; i <= HandleCount; i++)
            {
                if (GetHandleRectangle(i).Contains(point))
                    return i;
            }
        }

        if (Contains(point))
            return 0;

        return -1;
    }


    /// <summary>
    /// Test whether object intersects with rectangle
    /// </summary>
    public override bool IntersectsWith(Rect rectangle)
    {
        RectangleGeometry rg = new RectangleGeometry(rectangle);

        LineGeometry lg = new LineGeometry(lineStart, lineEnd);
        PathGeometry widen = lg.GetWidenedPathGeometry(new Pen(Brushes.Black, LineHitTestWidth));

        PathGeometry p = Geometry.Combine(rg, widen, GeometryCombineMode.Intersect, null);

        return (!p.IsEmpty());
    }

    /// <summary>
    /// Get cursor for the handle
    /// </summary>
    public override Cursor GetHandleCursor(int handleNumber)
    {
        switch (handleNumber)
        {
            case 1:
            case 2:
                return Cursors.SizeAll;
            default:
                return HelperFunctions.DefaultCursor;
        }
    }

    /// <summary>
    /// Move handle to new point (resizing)
    /// </summary>
    public override void MoveHandleTo(Point point, int handleNumber)
    {
        if (handleNumber == 1)
            lineStart = point;
        else
            lineEnd = point;

        RefreshDrawing();
    }

    /// <summary>
    /// Move object
    /// </summary>
    public override void Move(double deltaX, double deltaY)
    {
        lineStart.X += deltaX;
        lineStart.Y += deltaY;

        lineEnd.X += deltaX;
        lineEnd.Y += deltaY;

        RefreshDrawing();
    }


    #endregion Overrides
}
}
