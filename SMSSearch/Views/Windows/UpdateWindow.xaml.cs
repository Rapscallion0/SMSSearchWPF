using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;
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
            PopulateChangelog(currentVersion, updateInfo.Version ?? "Unknown", updateInfo.Changelog ?? string.Empty);
        }

        private void PopulateChangelog(string currentVersion, string newVersion, string changelog)
        {
            ChangelogText.Inlines.Clear();
            ChangelogText.Inlines.Add(new Run($"Current Version: {currentVersion}\nNew Version: {newVersion}\n\nChangelog:\n"));

            string[] lines = changelog.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // Regex to match the suffix " by @Username in https://..."
            var suffixRegex = new Regex(@"\s+by\s+@[\w\-]+\s+in\s+https://github\.com/\S+", RegexOptions.IgnoreCase);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    ChangelogText.Inlines.Add(new Run("\n"));
                    continue;
                }

                string processedLine = line;

                // Replace leading * with bullet
                if (processedLine.TrimStart().StartsWith("* "))
                {
                    processedLine = Regex.Replace(processedLine, @"^(\s*)\*\s+", "$1• ");
                }

                // Check for "Full Changelog: {URL}"
                var fullChangelogMatch = Regex.Match(processedLine, @"^(\s*\*\*Full Changelog\*\*:\s*)(https://\S+)$", RegexOptions.IgnoreCase);
                if (fullChangelogMatch.Success)
                {
                    ChangelogText.Inlines.Add(new Run(fullChangelogMatch.Groups[1].Value));

                    Hyperlink link = new Hyperlink(new Run(fullChangelogMatch.Groups[2].Value))
                    {
                        NavigateUri = new Uri(fullChangelogMatch.Groups[2].Value)
                    };
                    link.RequestNavigate += Hyperlink_RequestNavigate;
                    ChangelogText.Inlines.Add(link);
                    ChangelogText.Inlines.Add(new Run("\n"));
                    continue;
                }

                // Remove suffix
                processedLine = suffixRegex.Replace(processedLine, "");

                ChangelogText.Inlines.Add(new Run(processedLine + "\n"));
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
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
