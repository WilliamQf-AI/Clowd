﻿using System;
using System.Collections.Generic;
using System.Linq;
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

namespace Clowd.Capture
{
    [PropertyChanged.ImplementPropertyChanged]
    public partial class ImageEditorPage : UserControl
    {
        public ImageSource ScreenImage { get; set; }
        public bool ShowActionLabels { get; set; } = true;
        public ImageEditorPage(ImageSource image)
        {
            InitializeComponent();
            ScreenImage = image;
            drawingCanvas.SetResourceReference(DrawToolsLib.DrawingCanvas.HandleColorProperty, "AccentColor");
            drawingCanvas.ObjectColor = Colors.Red;
            drawingCanvas.LineWidth = 2;
            this.Loaded += ImageEditorPage_Loaded;
            //http://www.1001fonts.com/honey-script-font.html
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

        private void ImageEditorPage_Loaded(object sender, RoutedEventArgs e)
        {
            zoomControl.PreviewMouseLeftButtonDown += ZoomControl_PreviewMouseLeftButtonDown;
            zoomControl.PreviewMouseLeftButtonUp += ZoomControl_PreviewMouseLeftButtonUp;
            ZoomFit_Clicked(null, null);

            // you need to focus a button, or some other control that holds focus within the usercontrol.
            // if you don't do this, input bindings / keyboard shortcuts won't work.
            Keyboard.Focus(uploadButton);
        }
        private void ZoomControl_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (zoomControl.Panning)
            {
                zoomControl.StopPanning();
                e.Handled = true;
                drawingCanvas.Tool = DrawToolsLib.ToolType.Pointer;
            }
        }
        private void ZoomControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (drawingCanvas.Tool == DrawToolsLib.ToolType.Pointer)
            {
                if (drawingCanvas.HitTestGraphics(e.GetPosition(drawingCanvas)) < 0)
                {
                    drawingCanvas.Tool = DrawToolsLib.ToolType.None;
                    zoomControl.StartPanning();
                    e.Handled = true;
                    drawingCanvas.UnselectAll();
                }
            }
        }

        private void Font_Clicked(object sender, RoutedEventArgs e)
        {

        }
        private void Brush_Clicked(object sender, RoutedEventArgs e)
        {
            Microsoft.Samples.CustomControls.ColorPickerDialog cPicker = new Microsoft.Samples.CustomControls.ColorPickerDialog();

            cPicker.StartingColor = drawingCanvas.ObjectColor;
            cPicker.Owner = Window.GetWindow(this);

            bool? dialogResult = cPicker.ShowDialog();
            if (dialogResult != null && (bool)dialogResult == true)
            {
                drawingCanvas.ObjectColor = cPicker.SelectedColor;
            }
        }
        private void ZoomFit_Clicked(object sender, RoutedEventArgs e)
        {
            var rect = zoomControl.GetActualContentRect();
            var scaleX = zoomControl.ActualWidth / rect.Width;
            var scaleY = zoomControl.ActualHeight / rect.Height;
            var scale = Math.Min(scaleX, scaleY);
            zoomControl.ContentScale = scale;
            var x = zoomControl.ActualWidth / 2 - rect.Width * scale / 2;
            var y = zoomControl.ActualHeight / 2 - rect.Height * scale / 2;
            zoomControl.ContentOffset = new Point(x, y);
        }
        private void ZoomActual_Clicked(object sender, RoutedEventArgs e)
        {
            var pt = zoomControl.ContentOffset;
            var originRect = zoomControl.GetRenderedContentRect();
            var originCenter = new Point(originRect.Width / 2, originRect.Height / 2);
            zoomControl.ContentScale = 1;
            var newRect = zoomControl.GetRenderedContentRect();
            var newCenter = new Point(newRect.Width / 2, newRect.Height / 2);

            var offsetX = originCenter.X - newCenter.X;
            var offsetY = originCenter.Y - newCenter.Y;
            pt.Offset(offsetX, offsetY);
            zoomControl.ContentOffset = pt;
        }

        private void PrintCommand(object sender, ExecutedRoutedEventArgs e)
        {

        }
        private void CloseCommand(object sender, ExecutedRoutedEventArgs e)
        {
            Window.GetWindow(this).Close();
        }
        private void SaveCommand(object sender, ExecutedRoutedEventArgs e)
        {

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

        }
        private void DeleteCommand(object sender, ExecutedRoutedEventArgs e)
        {
            drawingCanvas.Delete();
        }
        private void UploadCommand(object sender, ExecutedRoutedEventArgs e)
        {
            MessageBox.Show("upload");
        }
        private void SelectToolCommand(object sender, ExecutedRoutedEventArgs e)
        {
            var tool = (DrawToolsLib.ToolType)Enum.Parse(typeof(DrawToolsLib.ToolType), (string)e.Parameter);
            drawingCanvas.Tool = tool;
        }
    }
}