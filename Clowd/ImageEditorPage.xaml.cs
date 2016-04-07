﻿using Clowd.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Clowd.Interop.Gdi32;
using DrawToolsLib.Graphics;
using ScreenVersusWpf;

namespace Clowd
{
    [PropertyChanged.ImplementPropertyChanged]
    public partial class ImageEditorPage : TemplatedControl
    {
        public override string Title => "Editor";
        public bool ShowActionLabels { get; set; } = true;

        private DrawToolsLib.ToolType? _shiftPanPreviousTool = null; // null means we're not in a shift-pan
        private string _imagePath;
        private Size _imageSize;

        public ImageEditorPage(string initImagePath)
        {
            InitializeComponent();
            drawingCanvas.SetResourceReference(DrawToolsLib.DrawingCanvas.HandleColorProperty, "AccentColor");
            drawingCanvas.ObjectColor = Colors.Red;
            drawingCanvas.LineWidth = 2;
            this.Loaded += ImageEditorPage_Loaded;
            _imagePath = initImagePath;

            if (!String.IsNullOrEmpty(_imagePath) && File.Exists(_imagePath))
            {
                double width, height;
                using (var stream = new FileStream(_imagePath, FileMode.Open, FileAccess.Read))
                {
                    var bitmapFrame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                    width = ScreenTools.ScreenToWpf(bitmapFrame.PixelWidth);
                    height = ScreenTools.ScreenToWpf(bitmapFrame.PixelHeight);
                    _imageSize = new Size(width, height);
                }
                var graphic = new GraphicsImage(drawingCanvas, new Rect(0, 0, width, height), _imagePath);
                drawingCanvas.AddGraphic(graphic);
            }
        }

        private void MoveCommandsToChrome()
        {
            var window = TemplatedWindow.GetWindow(this);
            if (window is MahApps.Metro.Controls.MetroWindow)
            {
                window.Title = "";
                var metro = window as MahApps.Metro.Controls.MetroWindow;
                var left = new MahApps.Metro.Controls.WindowCommands();
                var right = new MahApps.Metro.Controls.WindowCommands();
                rootGrid.Children.Remove(actionBar);
                rootGrid.Children.Remove(toolBar);
                left.Items.Add(actionBar);
                right.Items.Add(toolBar);
                metro.LeftWindowCommands = left;
                metro.RightWindowCommands = right;
                rootGrid.RowDefinitions[0].Height = new GridLength(0);
            }
        }
        private bool VerifyArtworkExists()
        {
            var b = drawingCanvas.GetArtworkBounds();
            if (b.Height < 10 || b.Width < 10)
            {
                //TODO: Show an error saying that there is nothing on the canvas.
                return false;
            }
            return true;
        }
        private DrawingVisual GetRenderedVisual()
        {
            var bounds = drawingCanvas.GetArtworkBounds();

            DrawingVisual vs = new DrawingVisual();
            DrawingContext dc = vs.RenderOpen();

            var transform = new TranslateTransform(Math.Floor(-bounds.Left), Math.Floor(-bounds.Top));
            dc.PushTransform(transform);

            dc.DrawRectangle(Brushes.White, null, bounds);
            drawingCanvas.Draw(dc);

            dc.Close();

            return vs;
        }
        private RenderTargetBitmap GetRenderedBitmap()
        {
            var drawingVisual = GetRenderedVisual();
            RenderTargetBitmap bmp = new RenderTargetBitmap(
                (int)ScreenTools.WpfToScreen(drawingVisual.ContentBounds.Width),
                (int)ScreenTools.WpfToScreen(drawingVisual.ContentBounds.Height),
                ScreenTools.DpiZoom,
                ScreenTools.DpiZoom,
                PixelFormats.Pbgra32);
            bmp.Render(drawingVisual);
            return bmp;
        }
        private PngBitmapEncoder GetRenderedPng()
        {
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(GetRenderedBitmap()));
            return enc;
        }

        protected override async void OnActivated(Window wnd)
        {
            var padding = App.Current.Settings.EditorSettings.CapturePadding;
            bool fit = TemplatedWindow.SizeToContent(wnd, new Size(_imageSize.Width + (padding * 2),
                _imageSize.Height + actionRow.Height.Value + (padding * 2)));

            // just doing this to force a thread context switch.
            // by the time we get back on to the UI thread the window will be done resizing.
            await Task.Delay(10);

            if (fit)
                ZoomActual_Clicked(null, null);
            else
                ZoomFit_Clicked(null, null);
        }

        private void ImageEditorPage_Loaded(object sender, RoutedEventArgs e)
        {
            // you need to focus a button, or some other control that holds keyboard focus.
            // if you don't do this, input bindings / keyboard shortcuts won't work.
            Keyboard.Focus(uploadButton);

            ZoomFit_Clicked(null, null);
        }

        private void Font_Clicked(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FontDialog dlg = new System.Windows.Forms.FontDialog();
            float wfSize = (float)ScreenTools.ScreenToWpf(drawingCanvas.TextFontSize) / 96 * 72;
            System.Drawing.FontStyle wfStyle;
            if (drawingCanvas.TextFontStyle == FontStyles.Italic)
                wfStyle = System.Drawing.FontStyle.Italic;
            else
                wfStyle = System.Drawing.FontStyle.Regular;
            dlg.Font = new System.Drawing.Font(drawingCanvas.TextFontFamilyName, wfSize, wfStyle);
            dlg.FontMustExist = true;
            dlg.MaxSize = 64;
            dlg.MinSize = 8;
            dlg.ShowColor = false;
            dlg.ShowEffects = false;
            dlg.ShowHelp = false;
            dlg.AllowVerticalFonts = false;
            dlg.AllowVectorFonts = true;
            dlg.AllowScriptChange = false;
            if (dlg.ShowDialog(Window.GetWindow(this)) == System.Windows.Forms.DialogResult.OK)
            {
                drawingCanvas.TextFontFamilyName = dlg.Font.FontFamily.GetName(0);
                drawingCanvas.TextFontSize = ScreenTools.WpfToScreen(dlg.Font.Size / 72 * 96);
                switch (dlg.Font.Style)
                {
                    case System.Drawing.FontStyle.Italic:
                        drawingCanvas.TextFontStyle = FontStyles.Italic;
                        break;
                    default:
                        drawingCanvas.TextFontStyle = FontStyles.Normal;
                        break;
                }
            }
        }

        private void Brush_Clicked(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.ColorDialog dlg = new System.Windows.Forms.ColorDialog();
            dlg.AnyColor = true;
            dlg.FullOpen = true;
            dlg.ShowHelp = false;
            var initial = drawingCanvas.ObjectColor;
            dlg.Color = System.Drawing.Color.FromArgb(initial.A, initial.R, initial.G, initial.B);
            if (dlg.ShowDialog(Window.GetWindow(this)) == System.Windows.Forms.DialogResult.OK)
            {
                var final = dlg.Color;
                drawingCanvas.ObjectColor = Color.FromArgb(final.A, final.R, final.G, final.B);
            }
        }

        private void ZoomFit_Clicked(object sender, RoutedEventArgs e)
        {
            drawingCanvas.ZoomPanFit();
        }

        private void ZoomActual_Clicked(object sender, RoutedEventArgs e)
        {
            drawingCanvas.ZoomPanActualSize();
        }

        private void PrintCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (!VerifyArtworkExists())
                return;
            PrintDialog dlg = new PrintDialog();
            var image = GetRenderedVisual();
            if (dlg.ShowDialog().GetValueOrDefault() != true)
            {
                return;
            }
            dlg.PrintVisual(image, "Graphics");
        }

        private void CloseCommand(object sender, ExecutedRoutedEventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }

        private void SaveCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (!VerifyArtworkExists())
                return;
            string directory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string defaultName = "screenshot";
            string extension = ".png";
            // generate unique file name (screenshot1.png, screenshot2.png etc)
            if (File.Exists(System.IO.Path.Combine(directory, $"{defaultName}{extension}")))
            {
                int i = 1;
                while (File.Exists(System.IO.Path.Combine(directory, $"{defaultName}{i}{extension}")))
                {
                    i++;
                }
                defaultName = defaultName + i.ToString();
            }
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = defaultName; // Default file name
            dlg.DefaultExt = extension; // Default file extension
            dlg.Filter = $"Images ({extension})|*{extension}"; // Filter files by extension
            dlg.OverwritePrompt = true;
            dlg.InitialDirectory = directory;

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();
            // Process save file dialog box results
            string filename = "";
            if (result == true)
            {
                // Save document
                filename = dlg.FileName;
            }
            else return;

            if (File.Exists(filename))
                File.Delete(filename);
            using (var fs = new FileStream(filename, FileMode.Create, FileAccess.Write))
            {
                GetRenderedPng().Save(fs);
            }
        }

        private void UndoCommand(object sender, ExecutedRoutedEventArgs e)
        {
            drawingCanvas.Undo();
        }

        private void RedoCommand(object sender, ExecutedRoutedEventArgs e)
        {
            drawingCanvas.Redo();
        }

        private void CopyCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (!VerifyArtworkExists())
                return;

            drawingCanvas.Copy();
            ClipboardEx.AddImage(GetRenderedBitmap());
        }

        private void DeleteCommand(object sender, ExecutedRoutedEventArgs e)
        {
            drawingCanvas.Delete();
        }

        private void UploadCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (!VerifyArtworkExists())
                return;
            using (var ms = new MemoryStream())
            {
                GetRenderedPng().Save(ms);
                ms.Position = 0;
                byte[] b;
                using (BinaryReader br = new BinaryReader(ms))
                {
                    b = br.ReadBytes(Convert.ToInt32(ms.Length));
                }
                var task = UploadManager.Upload(b, "clowd-default.png");
            }
        }

        private void SelectToolCommand(object sender, ExecutedRoutedEventArgs e)
        {
            var tool = (DrawToolsLib.ToolType)Enum.Parse(typeof(DrawToolsLib.ToolType), (string)e.Parameter);
            drawingCanvas.Tool = tool;
        }

        private void PasteCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (drawingCanvas.Paste())
                return;

            var path = System.IO.Path.GetTempFileName() + ".png";
            var img = ClipboardEx.GetImage();
            if (img == null)
                return;
            img.Save(path, ImageFormat.Png);
            var width = ScreenTools.ScreenToWpf(img.PixelWidth);
            var height = ScreenTools.ScreenToWpf(img.PixelHeight);
            var graphic = new GraphicsImage(drawingCanvas, new Rect(
                drawingCanvas.WorldOffset.X - (width / 2),
                drawingCanvas.WorldOffset.Y - (height / 2),
                width, height), path);
            drawingCanvas.AddGraphic(graphic);
        }

        private void rootGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox)
                return;
            if ((e.Key == Key.LeftShift || e.Key == Key.RightShift) && _shiftPanPreviousTool == null)
            {
                _shiftPanPreviousTool = drawingCanvas.Tool;
                drawingCanvas.Tool = DrawToolsLib.ToolType.None;
                shiftIndicator.Background = (Brush)App.Current.Resources["AccentColorBrush"];
                shiftIndicator.Opacity = 1;
            }
        }

        private void rootGrid_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.LeftShift || e.Key == Key.RightShift) && _shiftPanPreviousTool != null)
            {
                drawingCanvas.Tool = _shiftPanPreviousTool.Value;
                _shiftPanPreviousTool = null;
                shiftIndicator.Background = new SolidColorBrush(Color.FromRgb(112, 112, 112));
                shiftIndicator.Opacity = 0.8;
            }
        }
    }
}
