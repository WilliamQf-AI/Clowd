﻿using Clowd.Interop;
using Clowd.Interop.Gdi32;
using NReco.VideoConverter;
using PropertyChanged;
using ScreenVersusWpf;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Clowd.Utilities
{
    public static class ScreenUtil
    {
        public static Bitmap Capture(ScreenRect? bounds = null, bool captureCursor = false)
        {
            Rectangle rect = (bounds ?? ScreenTools.VirtualScreen.Bounds).ToSystem();

            var bitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(rect.X, rect.Y, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
                if (captureCursor)
                {
                    DrawCursor(g, new System.Drawing.Point(rect.X, rect.Y));
                }
            }
            return bitmap;
        }

        public static Bitmap CaptureActiveWindow(bool captureCursor = false)
        {
            var foreground = USER32.GetForegroundWindow();
            var bounds = USER32EX.GetTrueWindowBounds(foreground);
            return Capture(ScreenRect.FromSystem(bounds), captureCursor);
        }

        private static void DrawCursor(Graphics g, Point origin)
        {
            CURSORINFO cursorInfo;
            cursorInfo.cbSize = Marshal.SizeOf(typeof(CURSORINFO));
            if (USER32.GetCursorInfo(out cursorInfo) && cursorInfo.flags == 0x00000001 /*CURSOR_SHOWING*/)
            {
                var hicon = USER32.CopyIcon(cursorInfo.hCursor);
                ICONINFO iconInfo;
                int iconX, iconY;
                if (USER32.GetIconInfo(hicon, out iconInfo))
                {
                    iconX = cursorInfo.ptScreenPos.x - ((int)iconInfo.xHotspot) - origin.X;
                    iconY = cursorInfo.ptScreenPos.y - ((int)iconInfo.yHotspot) - origin.Y;

                    // Is this a color cursor or a monochrome one?
                    if (iconInfo.hbmColor != IntPtr.Zero)
                    {
                        using (Icon curIcon = Icon.FromHandle(hicon))
                        using (Bitmap curBitmap = curIcon.ToBitmap())
                            g.DrawImage(curBitmap, iconX, iconY);
                    }
                    else
                    {
                        //According to the ICONINFO documentation, monochrome cursors (such as I-Beam cursor):
                        //The top half of the mask bitmap is the AND mask, and the bottom half of the mask bitmap is the XOR bitmap. 
                        //When Windows draws the I-Beam cursor, the top half of this bitmap is first drawn over the desktop with an AND raster operation. 
                        //The bottom half of the bitmap is then drawn over top with an XOR raster operation. 
                        //Onscreen, The cursor should will appear as the inverse of the content behind it.
#warning This cursor should appear as the inverse of the content behind, but is currently being rendered completely white regardless of background.
                        using (Bitmap maskBitmap = Bitmap.FromHbitmap(iconInfo.hbmMask))
                        {
                            Graphics desktopGraphics = Graphics.FromHwnd(USER32.GetDesktopWindow());
                            IntPtr desktopHdc = desktopGraphics.GetHdc();
                            IntPtr maskHdc = GDI32.CreateCompatibleDC(desktopHdc);
                            IntPtr oldPtr = GDI32.SelectObject(maskHdc, maskBitmap.GetHbitmap());

                            var resultHdc = g.GetHdc();
                            var size = maskBitmap.Width;
                            GDI32.BitBlt(resultHdc, iconX, iconY, size, size, maskHdc, 0, 0, TernaryRasterOperations.SRCAND);
                            GDI32.BitBlt(resultHdc, iconX, iconY, size, size, maskHdc, 0, size, TernaryRasterOperations.SRCINVERT);
                            g.ReleaseHdc(resultHdc);

                            IntPtr newPtr = GDI32.SelectObject(maskHdc, oldPtr);
                            GDI32.DeleteObject(newPtr);
                            GDI32.DeleteDC(maskHdc);

                            desktopGraphics.ReleaseHdc(desktopHdc);
                        }
                    }
                    USER32.DestroyIcon(hicon);
                }
            }
        }
    }
}
