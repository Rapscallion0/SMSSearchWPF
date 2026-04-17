using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Utils;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SMS_Search.ViewModels
{
    public partial class UnarchiveViewModel : ObservableObject
    {
        private readonly IStateService _state;
        private readonly ILoggerService _logger;

        public event Action? RequestClose;

        public UnarchiveViewModel(IStateService state, ILoggerService logger)
        {
            _state = state;
            _logger = logger;
            LoadLocation();
        }

        [ObservableProperty]
        private double _left;

        [ObservableProperty]
        private double _top;

        [ObservableProperty]
        private bool _isCompleted;

        private void LoadLocation()
        {
            if (double.TryParse(_state.GetValue("UNARCHIVE", "LOCATIONX"), out double x))
                Left = x;
            else
                Left = 100;

            if (double.TryParse(_state.GetValue("UNARCHIVE", "LOCATIONY"), out double y))
                Top = y;
            else
                Top = 100;
        }

        public void SaveLocation(double left, double top)
        {
            _state.SetValue("UNARCHIVE", "LOCATIONX", left.ToString());
            _state.SetValue("UNARCHIVE", "LOCATIONY", top.ToString());
            _state.Save();
        }

        public async Task ProcessFiles(string[] files)
        {
            int count = 0;
            foreach (string path in files)
            {
                if (Directory.Exists(path))
                {
                    try
                    {
                        string[] subFiles = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                        foreach (string subFile in subFiles)
                        {
                            RemoveAttributes(subFile);
                            count++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error processing directory {path}", ex);
                    }
                }
                else if (File.Exists(path))
                {
                    RemoveAttributes(path);
                    count++;
                }
            }
            if (count > 0)
            {
                // Optional: Notify user or just log
                _logger.LogInfo($"Unarchived {count} files.");
                IsCompleted = true;
                await Task.Delay(4000);
                IsCompleted = false;
            }
        }

        private void RemoveAttributes(string path)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                File.SetAttributes(path, attributes & ~(FileAttributes.ReadOnly | FileAttributes.Archive));
                _logger.LogInfo($"File unarchived: {path}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error unarchiving file {path}", ex);
            }
        }

        [RelayCommand]
        private void Close()
        {
            RequestClose?.Invoke();
        }
    }
}
