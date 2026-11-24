using System;
using System.Drawing;
using System.Windows.Forms;
using SMS_Search.Utils;

namespace SMS_Search.Services
{
    public sealed class TrayIconService : ITrayIconService
    {
        public const int MaxTooltipLength = 63;

        private readonly ILoggerService _logger;
        private NotifyIcon? _notifyIcon;
        private ContextMenuStrip? _contextMenu;
        private ToolStripMenuItem? _openItem;
        private ToolStripMenuItem? _encryptionItem;
        private Icon? _icon;
        private Font? _boldFont;
        private bool _disposed;

        public bool IsVisible => _notifyIcon?.Visible ?? false;

        public event Action? OpenRequested;
        public event Action? OpenGs1ToolkitRequested;
        public event Action? OpenUnarchiveRequested;
        public event Action? OpenEncryptionUtilityRequested;
        public event Action? OpenSettingsRequested;
        public event Action? ExitRequested;

        public TrayIconService(ILoggerService logger)
        {
            _logger = logger;
        }

        public static string BuildTooltipText(TrayIconState state, string? hotkeyDisplay)
        {
            bool hasHotkey = !string.IsNullOrWhiteSpace(hotkeyDisplay);
            string text = state switch
            {
                TrayIconState.Background => hasHotkey
                    ? $"SMS Search — Background ({hotkeyDisplay})"
                    : "SMS Search — Running in tray",
                TrayIconState.Minimized => hasHotkey
                    ? $"SMS Search — Minimized ({hotkeyDisplay})"
                    : "SMS Search (Minimized)",
                TrayIconState.Open => hasHotkey
                    ? $"SMS Search ({hotkeyDisplay})"
                    : "SMS Search",
                _ => "SMS Search"
            };

            if (text.Length > MaxTooltipLength)
            {
                text = text.Substring(0, MaxTooltipLength - 3) + "...";
            }

            return text;
        }

        public static string BuildOpenMenuText(string? hotkeyDisplay)
        {
            if (!string.IsNullOrWhiteSpace(hotkeyDisplay))
            {
                return $"Open SMS Search ({hotkeyDisplay})";
            }
            return "Open SMS Search";
        }

        public static bool ShouldShowEncryptionUtility(Keys modifiers)
        {
            return (modifiers & (Keys.Control | Keys.Shift)) == (Keys.Control | Keys.Shift);
        }

        public void Show(TrayIconState state, string? hotkeyDisplay = null)
        {
            if (_disposed) return;

            EnsureInitialized();
            UpdateState(state, hotkeyDisplay);

            if (_notifyIcon != null && !_notifyIcon.Visible)
            {
                _notifyIcon.Visible = true;
            }
        }

        public void UpdateState(TrayIconState state, string? hotkeyDisplay = null)
        {
            if (_disposed) return;
            if (_notifyIcon == null) return;

            _notifyIcon.Text = BuildTooltipText(state, hotkeyDisplay);

            if (_openItem != null)
            {
                _openItem.Text = BuildOpenMenuText(hotkeyDisplay);
            }
        }

        public void Hide()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
            }
        }

        private void EnsureInitialized()
        {
            if (_notifyIcon != null) return;

            _icon = LoadTrayIcon();
            _contextMenu = new ContextMenuStrip();
            _contextMenu.Opening += ContextMenu_Opening;

            _openItem = new ToolStripMenuItem("Open SMS Search");
            _boldFont = new Font(_openItem.Font, FontStyle.Bold);
            _openItem.Font = _boldFont;
            _openItem.Click += (_, _) => OpenRequested?.Invoke();

            var toolsMenu = new ToolStripMenuItem("Tools");
            var gs1Item = new ToolStripMenuItem("GS1 Toolkit");
            gs1Item.Click += (_, _) => OpenGs1ToolkitRequested?.Invoke();
            var unarchiveItem = new ToolStripMenuItem("Unarchive Utility");
            unarchiveItem.Click += (_, _) => OpenUnarchiveRequested?.Invoke();
            _encryptionItem = new ToolStripMenuItem("Encryption Utility")
            {
                Visible = false
            };
            _encryptionItem.Click += (_, _) => OpenEncryptionUtilityRequested?.Invoke();

            toolsMenu.DropDownItems.Add(gs1Item);
            toolsMenu.DropDownItems.Add(unarchiveItem);
            toolsMenu.DropDownItems.Add(_encryptionItem);

            var settingsItem = new ToolStripMenuItem("Settings...");
            settingsItem.Click += (_, _) => OpenSettingsRequested?.Invoke();

            var exitItem = new ToolStripMenuItem("Exit SMS Search");
            exitItem.Click += (_, _) => ExitRequested?.Invoke();

            _contextMenu.Items.Add(_openItem);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(toolsMenu);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(settingsItem);
            _contextMenu.Items.Add(exitItem);

            _notifyIcon = new NotifyIcon
            {
                Icon = _icon,
                ContextMenuStrip = _contextMenu,
                Visible = false
            };
            _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke();
        }

        private void ContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_encryptionItem != null)
            {
                _encryptionItem.Visible = ShouldShowEncryptionUtility(Control.ModifierKeys);
            }
        }

        private Icon LoadTrayIcon()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/Resources/SMS Search.ico");
                var streamInfo = System.Windows.Application.GetResourceStream(uri);
                if (streamInfo != null)
                {
                    using var stream = streamInfo.Stream;
                    return new Icon(stream);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not load embedded tray icon resource: {Error}", ex.Message);
            }

            try
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    var extracted = Icon.ExtractAssociatedIcon(exePath);
                    if (extracted != null)
                    {
                        return extracted;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not extract executable icon for tray: {Error}", ex.Message);
            }

            return (Icon)SystemIcons.Application.Clone();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_contextMenu != null)
            {
                _contextMenu.Opening -= ContextMenu_Opening;
            }

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            _contextMenu?.Dispose();
            _contextMenu = null;

            _boldFont?.Dispose();
            _boldFont = null;

            _icon?.Dispose();
            _icon = null;
        }
    }
}
