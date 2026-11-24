using System;

namespace SMS_Search.Services
{
    public enum TrayIconState
    {
        Background,
        Minimized,
        Open
    }

    public interface ITrayIconService : IDisposable
    {
        bool IsVisible { get; }

        void Show(TrayIconState state, string? hotkeyDisplay = null);
        void UpdateState(TrayIconState state, string? hotkeyDisplay = null);
        void Hide();

        event Action? OpenRequested;
        event Action? OpenGs1ToolkitRequested;
        event Action? OpenUnarchiveRequested;
        event Action? OpenEncryptionUtilityRequested;
        event Action? OpenSettingsRequested;
        event Action? ExitRequested;
    }
}
