using System;
using System.Windows;
using SMS_Search.Data;
using System.Drawing; // Point, Rectangle

namespace SMS_Search.Utils
{
    public static class WindowPositioner
    {
        public static void ApplyStartupLocation(Window window, StartupLocationMode mode, double? lastX, double? lastY)
        {
            // Set to Manual to respect Left/Top
            window.WindowStartupLocation = WindowStartupLocation.Manual;

            // Estimate dimensions if not loaded
            double w = window.ActualWidth > 0 ? window.ActualWidth : (double.IsNaN(window.Width) ? 800 : window.Width);
            double h = window.ActualHeight > 0 ? window.ActualHeight : (double.IsNaN(window.Height) ? 600 : window.Height);

            // Get DPI scale
            double dpiX = 1.0;
            double dpiY = 1.0;

            try
            {
                var source = PresentationSource.FromVisual(window);
                if (source?.CompositionTarget != null)
                {
                    dpiX = source.CompositionTarget.TransformToDevice.M11;
                    dpiY = source.CompositionTarget.TransformToDevice.M22;
                }
                else
                {
                    using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    {
                        dpiX = g.DpiX / 96.0;
                        dpiY = g.DpiY / 96.0;
                    }
                }
            }
            catch
            {
                // Fallback to 1.0
            }

            switch (mode)
            {
                case StartupLocationMode.Primary:
                    window.Left = (SystemParameters.WorkArea.Width - w) / 2 + SystemParameters.WorkArea.Left;
                    window.Top = (SystemParameters.WorkArea.Height - h) / 2 + SystemParameters.WorkArea.Top;
                    break;

                case StartupLocationMode.Active:
                    try
                    {
                        var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
                        var area = screen.WorkingArea;

                        // Convert pixels to DIPs
                        double areaX = area.X / dpiX;
                        double areaY = area.Y / dpiY;
                        double areaW = area.Width / dpiX;
                        double areaH = area.Height / dpiY;

                        window.Left = areaX + (areaW - w) / 2;
                        window.Top = areaY + (areaH - h) / 2;
                    }
                    catch
                    {
                        // Fallback
                        goto case StartupLocationMode.Primary;
                    }
                    break;

                case StartupLocationMode.Cursor:
                    try
                    {
                        var cursor = System.Windows.Forms.Cursor.Position;
                        window.Left = (cursor.X / dpiX) - (w / 2);
                        window.Top = (cursor.Y / dpiY) - (h / 2);
                    }
                    catch
                    {
                         goto case StartupLocationMode.Primary;
                    }
                    break;

                case StartupLocationMode.Last:
                    if (lastX.HasValue && lastY.HasValue)
                    {
                        try
                        {
                            // Check visibility
                            double centerX = lastX.Value + w / 2;
                            double centerY = lastY.Value + h / 2;

                            int pCenterX = (int)(centerX * dpiX);
                            int pCenterY = (int)(centerY * dpiY);

                            var s = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(pCenterX, pCenterY));

                            // Check if the center point is actually inside the screen bounds
                            if (s.Bounds.Contains(pCenterX, pCenterY))
                            {
                                window.Left = lastX.Value;
                                window.Top = lastY.Value;
                                return;
                            }
                        }
                        catch { }
                    }
                    // Fallback
                    ApplyStartupLocation(window, StartupLocationMode.Primary, null, null);
                    break;
            }
        }
    }
}
