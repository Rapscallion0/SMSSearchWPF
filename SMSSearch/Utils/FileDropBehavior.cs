using System;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace SMS_Search.Utils
{
    /// <summary>
    /// Attached behavior enabling drag-and-drop of CSV files (or folders containing Fields.csv)
    /// onto a target UIElement, bypassing inner TextBox drop interception.
    /// </summary>
    public static class FileDropBehavior
    {
        public static readonly DependencyProperty EnableCsvDropProperty =
            DependencyProperty.RegisterAttached(
                "EnableCsvDrop",
                typeof(bool),
                typeof(FileDropBehavior),
                new PropertyMetadata(false, OnEnableCsvDropChanged));

        public static readonly DependencyProperty FileDroppedCommandProperty =
            DependencyProperty.RegisterAttached(
                "FileDroppedCommand",
                typeof(ICommand),
                typeof(FileDropBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty IsDragOverProperty =
            DependencyProperty.RegisterAttached(
                "IsDragOver",
                typeof(bool),
                typeof(FileDropBehavior),
                new PropertyMetadata(false));

        public static bool GetEnableCsvDrop(DependencyObject obj) => (bool)obj.GetValue(EnableCsvDropProperty);
        public static void SetEnableCsvDrop(DependencyObject obj, bool value) => obj.SetValue(EnableCsvDropProperty, value);

        public static ICommand? GetFileDroppedCommand(DependencyObject obj) => (ICommand?)obj.GetValue(FileDroppedCommandProperty);
        public static void SetFileDroppedCommand(DependencyObject obj, ICommand? value) => obj.SetValue(FileDroppedCommandProperty, value);

        public static bool GetIsDragOver(DependencyObject obj) => (bool)obj.GetValue(IsDragOverProperty);
        public static void SetIsDragOver(DependencyObject obj, bool value) => obj.SetValue(IsDragOverProperty, value);

        private static void OnEnableCsvDropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not UIElement element) return;

            if ((bool)e.NewValue)
            {
                element.AllowDrop = true;
                element.PreviewDragOver += Element_PreviewDragOver;
                element.PreviewDragLeave += Element_PreviewDragLeave;
                element.PreviewDrop += Element_PreviewDrop;
            }
            else
            {
                element.PreviewDragOver -= Element_PreviewDragOver;
                element.PreviewDragLeave -= Element_PreviewDragLeave;
                element.PreviewDrop -= Element_PreviewDrop;
                SetIsDragOver(element, false);
            }
        }

        private static void Element_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
        {
            string? resolved = ExtractDroppedCsvPath(e.Data);
            if (sender is DependencyObject d)
                SetIsDragOver(d, resolved != null);
            e.Effects = resolved != null ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
            e.Handled = true;
        }

        private static void Element_PreviewDragLeave(object sender, System.Windows.DragEventArgs e)
        {
            if (sender is DependencyObject d)
                SetIsDragOver(d, false);
        }

        private static void Element_PreviewDrop(object sender, System.Windows.DragEventArgs e)
        {
            if (sender is DependencyObject d)
                SetIsDragOver(d, false);

            string? resolved = ExtractDroppedCsvPath(e.Data);
            if (resolved == null) return;

            if (sender is DependencyObject target)
            {
                var cmd = GetFileDroppedCommand(target);
                if (cmd != null && cmd.CanExecute(resolved))
                {
                    cmd.Execute(resolved);
                }
            }
            e.Handled = true;
        }

        private static string? ExtractDroppedCsvPath(System.Windows.IDataObject data)
        {
            if (!data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                return null;

            return ResolveDroppedCsvPath(data.GetData(System.Windows.DataFormats.FileDrop) as string[]);
        }

        public static string? ResolveDroppedCsvPath(string[]? paths)
        {
            if (paths == null || paths.Length == 0)
                return null;

            string candidate = paths[0];
            if (string.IsNullOrWhiteSpace(candidate))
                return null;

            if (File.Exists(candidate) && candidate.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                return candidate;

            if (Directory.Exists(candidate))
            {
                string fieldsCsv = Path.Combine(candidate, "Fields.csv");
                if (File.Exists(fieldsCsv))
                    return fieldsCsv;
            }

            return null;
        }
    }
}

