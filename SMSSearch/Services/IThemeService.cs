using SMS_Search.Data;

namespace SMS_Search.Services
{
    /// <summary>
    /// Manages application-wide Light/Dark/System theme switching.
    /// </summary>
    public interface IThemeService
    {
        AppThemeMode CurrentMode { get; }
        bool IsDarkTheme { get; }
        void Initialize();
        void ApplyTheme(AppThemeMode mode);
    }
}

