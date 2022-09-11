﻿using System.Windows;
using System.Windows.Input;

namespace Clowd.Drawing.Tools
{
    internal enum SnapMode
    {
        None = 0,
        Diagonal = 1,
        All = 2,
    }

    internal abstract class ToolBase
    {
        internal abstract ToolActionType ActionType { get; }

        protected Point LastMouseDownPt { get; set; }
        protected Point LastMouseMovePt { get; set; }

        protected readonly Cursor Cursor;
        private readonly SnapMode _snapMode;

        protected ToolBase(Cursor cursor, SnapMode snapMode = SnapMode.None)
        {
            Cursor = cursor;
            _snapMode = snapMode;
        }

        public virtual void OnMouseDown(DrawingCanvas canvas, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;
            
            canvas.CaptureMouse();
            canvas.UnselectAll();

            var pt = e.GetPosition(canvas);
            LastMouseDownPt = pt;
            OnMouseDownImpl(canvas, pt);
        }

        public virtual void OnMouseMove(DrawingCanvas canvas, MouseEventArgs e)
        {
            if (canvas.IsMouseCaptured)
            {
                var pt = e.GetPosition(canvas);

                // snap the point to a 45deg angle (maybe)
                if (_snapMode != SnapMode.None && (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.LeftShift)))
                {
                    pt = HelperFunctions.SnapPointToCommonAngle(LastMouseDownPt, pt, _snapMode == SnapMode.Diagonal);
                }

                LastMouseMovePt = pt;
                OnMouseMoveImpl(canvas, pt);
            }
        }

        public virtual void OnMouseUp(DrawingCanvas canvas, MouseButtonEventArgs e)
        {
            if (canvas.IsMouseCaptured)
            {
                OnMouseMove(canvas, e);
                canvas.ReleaseMouseCapture();
                OnMouseUpImpl(canvas);
            }
            else
            {
                AbortOperation(canvas);
            }

            canvas.Tool = ToolType.Pointer;
            canvas.Cursor = HelperFunctions.DefaultCursor;
        }

        protected virtual void OnMouseDownImpl(DrawingCanvas canvas, Point pt)
        { }

        protected virtual void OnMouseMoveImpl(DrawingCanvas canvas, Point pt)
        { }

        protected virtual void OnMouseUpImpl(DrawingCanvas canvas)
        { }

        public virtual void AbortOperation(DrawingCanvas canvas)
        { }

        public virtual void SetCursor(DrawingCanvas canvas)
        {
            canvas.Cursor = Cursor;
        }
    }
}
