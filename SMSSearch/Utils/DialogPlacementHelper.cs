#nullable enable
using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace SMS_Search.Utils
{
    public static class DialogPlacementHelper
    {
        public static void ApplyOwnerAndPlacement(Window dialog, Window? fallbackOwner = null)
        {
            var app = System.Windows.Application.Current;
            var mainWin = fallbackOwner ?? app?.MainWindow;

            Window? bestOwner = null;

            if (app != null)
            {
                var candidates = app.Windows
                    .OfType<Window>()
                    .Where(w => !ReferenceEquals(w, dialog) &&
                                w.IsVisible &&
                                w.WindowState != WindowState.Minimized &&
                                w.GetType().Name != "ToastWindow" &&
                                w.ActualWidth >= 220 &&
                                w.ActualHeight >= 120)
                    .ToList();

                bestOwner = candidates.FirstOrDefault(w => w.IsActive);

                if (bestOwner == null)
                {
                    bool mainIsShrunk = mainWin == null ||
                                        !mainWin.IsVisible ||
                                        mainWin.WindowState == WindowState.Minimized ||
                                        mainWin.ActualWidth < 400 ||
                                        mainWin.ActualHeight < 250;

                    if (mainIsShrunk)
                    {
                        bestOwner = candidates
                            .Where(w => !ReferenceEquals(w, mainWin))
                            .OrderByDescending(w => w.ActualWidth * w.ActualHeight)
                            .FirstOrDefault();
                    }
                    else
                    {
                        bestOwner = mainWin;
                    }
                }
            }

            if (bestOwner != null)
            {
                dialog.Owner = bestOwner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                return;
            }

            // Fallback: center on the monitor under the cursor
            dialog.WindowStartupLocation = WindowStartupLocation.Manual;
            dialog.Loaded += (_, _) =>
            {
                var workArea = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
                var source = PresentationSource.FromVisual(dialog);
                double scaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                double scaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
                if (scaleX <= 0) scaleX = 1.0;
                if (scaleY <= 0) scaleY = 1.0;

                double areaLeft = workArea.Left / scaleX;
                double areaTop = workArea.Top / scaleY;
                double areaW = workArea.Width / scaleX;
                double areaH = workArea.Height / scaleY;

                dialog.Left = areaLeft + Math.Max(0, (areaW - dialog.ActualWidth) / 2);
                dialog.Top = areaTop + Math.Max(0, (areaH - dialog.ActualHeight) / 2);
            };
        }
    }
}

