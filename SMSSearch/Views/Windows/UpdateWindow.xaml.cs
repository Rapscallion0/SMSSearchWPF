using System;
using System.Reflection;
using System.Windows;
using SMS_Search.Utils;

namespace SMS_Search.Views.Windows
{
    public partial class UpdateWindow : Window
    {
        private readonly UpdateInfo _updateInfo;
        private readonly UpdateChecker _updateChecker;

        public UpdateWindow(UpdateInfo updateInfo, UpdateChecker updateChecker)
        {
            InitializeComponent();
            _updateInfo = updateInfo;
            _updateChecker = updateChecker;

            string currentVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "Unknown";
            ChangelogText.Text = $"Current Version: {currentVersion}\nNew Version: {updateInfo.Version}\n\nChangelog:\n{updateInfo.Changelog}";
        }

        private async void YesButton_Click(object sender, RoutedEventArgs e)
        {
            ActionPanel.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Visible;

            var progressReporter = new Progress<double>(value =>
            {
                UpdateProgressBar.Value = value;
            });

            var statusReporter = new Progress<string>(status =>
            {
                StatusTextBlock.Text = status;
            });

            await _updateChecker.DownloadAndInstallUpdateAsync(_updateInfo, progressReporter, statusReporter);
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
