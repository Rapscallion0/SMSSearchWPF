#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using AvalonDock;

namespace SMS_Search.Utils
{
    public readonly record struct SnapRect(int Left, int Top, int Right, int Bottom)
    {
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    /// <summary>
    /// Provides smooth Win32 WM_MOVING magnetic edge-snapping for undocked AvalonDock floating windows.
    /// </summary>
    public static class FloatingWindowSnapBehavior
    {
        private const int WM_MOVING = 0x0216;
        private const double SnapThresholdDips = 12.0;

        public static readonly DependencyProperty EnableWindowSnapProperty =
            DependencyProperty.RegisterAttached(
                "EnableWindowSnap",
                typeof(bool),
                typeof(FloatingWindowSnapBehavior),
                new PropertyMetadata(false, OnEnableWindowSnapChanged));

        public static bool GetEnableWindowSnap(DependencyObject obj) =>
            (bool)obj.GetValue(EnableWindowSnapProperty);

        public static void SetEnableWindowSnap(DependencyObject obj, bool value) =>
            obj.SetValue(EnableWindowSnapProperty, value);

        private sealed class ManagerState
        {
            public readonly HashSet<Window> HookedWindows = new();
            public readonly Dictionary<Window, HwndSourceHook> Hooks = new();
        }

        private static readonly Dictionary<DockingManager, ManagerState> _states = new();

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

        private static void OnEnableWindowSnapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DockingManager manager) return;

            if ((bool)e.NewValue)
            {
                if (!_states.ContainsKey(manager))
                {
                    _states[manager] = new ManagerState();
                    manager.LayoutUpdated += Manager_LayoutUpdated;
                    manager.Unloaded += Manager_Unloaded;
                }
                AttachToFloatingWindows(manager);
            }
            else
            {
                DetachAll(manager);
            }
        }

        private static void Manager_LayoutUpdated(object? sender, EventArgs e)
        {
            if (sender is DockingManager manager)
            {
                AttachToFloatingWindows(manager);
            }
        }

        private static void Manager_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is DockingManager manager)
            {
                DetachAll(manager);
            }
        }

        private static void DetachAll(DockingManager manager)
        {
            manager.LayoutUpdated -= Manager_LayoutUpdated;
            manager.Unloaded -= Manager_Unloaded;

            if (_states.TryGetValue(manager, out var state))
            {
                foreach (var kvp in state.Hooks)
                {
                    if (PresentationSource.FromVisual(kvp.Key) is HwndSource src)
                    {
                        src.RemoveHook(kvp.Value);
                    }
                }
                state.Hooks.Clear();
                state.HookedWindows.Clear();
                _states.Remove(manager);
            }
        }

        private static void AttachToFloatingWindows(DockingManager manager)
        {
            if (!_states.TryGetValue(manager, out var state)) return;

            foreach (var fw in manager.FloatingWindows)
            {
                if (state.HookedWindows.Contains(fw)) continue;
                state.HookedWindows.Add(fw);

                void TryAddHook()
                {
                    var handle = new WindowInteropHelper(fw).Handle;
                    if (handle == IntPtr.Zero) return;
                    if (HwndSource.FromHwnd(handle) is not HwndSource src) return;

                    if (state.Hooks.ContainsKey(fw)) return;
                    HwndSourceHook hook = (IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                        WindowProc(manager, fw, hwnd, msg, lParam);
                    state.Hooks[fw] = hook;
                    src.AddHook(hook);
                }

                fw.Closed += (_, _) =>
                {
                    if (state.Hooks.TryGetValue(fw, out var hook))
                    {
                        if (PresentationSource.FromVisual(fw) is HwndSource src)
                        {
                            src.RemoveHook(hook);
                        }
                        state.Hooks.Remove(fw);
                    }
                    state.HookedWindows.Remove(fw);
                };

                if (new WindowInteropHelper(fw).Handle != IntPtr.Zero)
                {
                    TryAddHook();
                }
                else
                {
                    fw.SourceInitialized += (_, _) => TryAddHook();
                }
            }
        }

        private static IntPtr WindowProc(DockingManager manager, Window draggedWindow, IntPtr hwnd, int msg, IntPtr lParam)
        {
            if (msg != WM_MOVING || lParam == IntPtr.Zero) return IntPtr.Zero;
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) return IntPtr.Zero;

            var native = Marshal.PtrToStructure<NativeRect>(lParam);
            var candidate = new SnapRect(native.Left, native.Top, native.Right, native.Bottom);

            var targets = new List<SnapRect>();

            foreach (var fw in manager.FloatingWindows)
            {
                if (ReferenceEquals(fw, draggedWindow) || !fw.IsVisible) continue;
                var otherHandle = new WindowInteropHelper(fw).Handle;
                if (otherHandle != IntPtr.Zero && GetWindowRect(otherHandle, out var r))
                {
                    targets.Add(new SnapRect(r.Left, r.Top, r.Right, r.Bottom));
                }
            }

            var mainWindow = Window.GetWindow(manager);
            if (mainWindow != null && mainWindow.IsVisible)
            {
                var mainHandle = new WindowInteropHelper(mainWindow).Handle;
                if (mainHandle != IntPtr.Zero && GetWindowRect(mainHandle, out var mr))
                {
                    targets.Add(new SnapRect(mr.Left, mr.Top, mr.Right, mr.Bottom));
                }
            }

            var workArea = System.Windows.Forms.Screen.FromHandle(hwnd).WorkingArea;
            targets.Add(new SnapRect(workArea.Left, workArea.Top, workArea.Right, workArea.Bottom));

            double dpiScale = VisualTreeHelper.GetDpi(draggedWindow).DpiScaleX;
            int thresholdPx = (int)Math.Round(SnapThresholdDips * (dpiScale > 0 ? dpiScale : 1.0));

            var snapped = ComputeSnappedRect(candidate, targets, thresholdPx);
            if (snapped != candidate)
            {
                var updated = new NativeRect
                {
                    Left = snapped.Left,
                    Top = snapped.Top,
                    Right = snapped.Right,
                    Bottom = snapped.Bottom
                };
                Marshal.StructureToPtr(updated, lParam, false);
                return new IntPtr(1);
            }

            return IntPtr.Zero;
        }

        public static SnapRect ComputeSnappedRect(SnapRect candidate, IReadOnlyList<SnapRect> targets, int thresholdPx)
        {
            if (targets == null || targets.Count == 0 || thresholdPx <= 0)
            {
                return candidate;
            }

            int width = candidate.Width;
            int height = candidate.Height;

            int bestDx = 0;
            int bestDistX = thresholdPx + 1;
            int bestDy = 0;
            int bestDistY = thresholdPx + 1;

            foreach (var t in targets)
            {
                if (RangesOverlapOrTouch(candidate.Top, candidate.Bottom, t.Top, t.Bottom, thresholdPx))
                {
                    ConsiderDelta(t.Right - candidate.Left, ref bestDx, ref bestDistX);   // Left -> Target Right
                    ConsiderDelta(t.Left - candidate.Right, ref bestDx, ref bestDistX);   // Right -> Target Left
                    ConsiderDelta(t.Left - candidate.Left, ref bestDx, ref bestDistX);    // Left -> Target Left
                    ConsiderDelta(t.Right - candidate.Right, ref bestDx, ref bestDistX);  // Right -> Target Right
                }

                if (RangesOverlapOrTouch(candidate.Left, candidate.Right, t.Left, t.Right, thresholdPx))
                {
                    ConsiderDelta(t.Bottom - candidate.Top, ref bestDy, ref bestDistY);   // Top -> Target Bottom
                    ConsiderDelta(t.Top - candidate.Bottom, ref bestDy, ref bestDistY);   // Bottom -> Target Top
                    ConsiderDelta(t.Top - candidate.Top, ref bestDy, ref bestDistY);      // Top -> Target Top
                    ConsiderDelta(t.Bottom - candidate.Bottom, ref bestDy, ref bestDistY);// Bottom -> Target Bottom
                }
            }

            int newLeft = candidate.Left + bestDx;
            int newTop = candidate.Top + bestDy;
            return new SnapRect(newLeft, newTop, newLeft + width, newTop + height);
        }

        private static bool RangesOverlapOrTouch(int aStart, int aEnd, int bStart, int bEnd, int margin) =>
            aStart <= bEnd + margin && aEnd >= bStart - margin;

        private static void ConsiderDelta(int delta, ref int bestDelta, ref int bestDist)
        {
            int dist = Math.Abs(delta);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestDelta = delta;
            }
        }
    }
}

