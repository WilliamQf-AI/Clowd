using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;

namespace DrawToolsLib.Graphics
{
    [Serializable]
    public class GraphicsImage : GraphicsRectangle
    {
        public string FileName
        {
            get { return _fileName; }
            set
            {
                if (value == _fileName) return;
                _fileName = value;
                OnPropertyChanged();
            }
        }

        private string _fileName;

        [XmlIgnore]
        private BitmapSource _imageBacking;
        [XmlIgnore]
        private BitmapSource _imageCache
        {
            get
            {
                if (_imageBacking == null)
                    _imageBacking = BitmapFrame.Create(
                        new Uri(_fileName, UriKind.Absolute),
                        BitmapCreateOptions.None,
                        BitmapCacheOption.OnLoad);
                return _imageBacking;
            }
        }

        [XmlIgnore]
        private ScaleTransform _transform = new ScaleTransform(1, 1);

        protected GraphicsImage()
        {
            Effect = null;
        }
        public GraphicsImage(DrawingCanvas canvas, Rect rect, string filePath)
           : this(canvas.ObjectColor, canvas.LineWidth, rect, filePath)
        {
        }

        public GraphicsImage(Color objectColor, double lineWidth, Rect rect, string filePath)
            : base(objectColor, lineWidth, rect)
        {
            _fileName = filePath;
            Effect = null;
            if (!File.Exists(_fileName))
                throw new FileNotFoundException(_fileName);
        }

        internal override void DrawRectangle(DrawingContext drawingContext)
        {
            if (drawingContext == null)
                throw new ArgumentNullException(nameof(drawingContext));

            Rect r = UnrotatedBounds;
            if (_imageCache.PixelWidth == (int)Math.Round(r.Width, 3) && _imageCache.PixelHeight == (int)Math.Round(r.Height, 3) && Angle == 0)
            {
                // If the image is still at the original size and zero rotation, round the rectangle position to whole pixels to avoid blurring.
                r.X = Math.Round(r.X);
                r.Y = Math.Round(r.Y);
            }

            var centerX = r.Left + (r.Width / 2);
            var centerY = r.Top + (r.Height / 2);

            // push current flip transform
            _transform.CenterX = centerX;
            _transform.CenterY = centerY;
            drawingContext.PushTransform(_transform);

            // push any resizing/rendering transform (will be added to current transform later)
            if (Right <= Left)
                drawingContext.PushTransform(new ScaleTransform(-1, 1, centerX, centerY));
            if (Bottom <= Top)
                drawingContext.PushTransform(new ScaleTransform(1, -1, centerX, centerY));

            drawingContext.DrawImage(_imageCache, r);

            if (Right <= Left || Bottom <= Top)
                drawingContext.Pop();

            drawingContext.Pop();
        }

        internal override void Normalize()
        {
            if (Right <= Left)
                _transform.ScaleX = _transform.ScaleX / -1;
            if (Bottom <= Top)
                _transform.ScaleY = _transform.ScaleY / -1;

            base.Normalize();
        }

        public override GraphicsBase Clone()
        {
            return new GraphicsImage(ObjectColor, LineWidth, Bounds, FileName) { ObjectId = ObjectId };
        }
    }
}
