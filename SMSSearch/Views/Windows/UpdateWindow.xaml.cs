using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SMS_Search.Utils;
using System.Windows.Documents;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Windows.Navigation;

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

            var headerText = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
            headerText.Inlines.Add(new Run("Current Version: ") { FontWeight = FontWeights.Bold });
            headerText.Inlines.Add(new Run($"{currentVersion}\n"));
            headerText.Inlines.Add(new Run("New Version: ") { FontWeight = FontWeights.Bold });
            headerText.Inlines.Add(new Run($"{updateInfo.Version}\n\n"));
            headerText.Inlines.Add(new Run("Changelog:") { FontWeight = FontWeights.Bold });
            ChangelogPanel.Children.Add(headerText);

            if (!string.IsNullOrEmpty(updateInfo.Changelog))
            {
                string changelog = updateInfo.Changelog;

                // Remove the "by @user in https://..." string
                changelog = Regex.Replace(changelog, @" by @\S+ in https://\S+", "");

                ParseAndAddMarkdown(changelog, ChangelogPanel);
            }
        }

        private void ParseAndAddMarkdown(string markdown, StackPanel panel)
        {
            string[] lines = markdown.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            TextBlock? currentTextBlock = null;

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();

                if (string.IsNullOrEmpty(trimmedLine))
                {
                    currentTextBlock = null;
                    continue;
                }

                double fontSize = 12;
                FontWeight fontWeight = FontWeights.Normal;
                Thickness margin = new Thickness(0, 2, 0, 2);

                if (trimmedLine.StartsWith("#"))
                {
                    int headerLevel = 0;
                    while (headerLevel < trimmedLine.Length && trimmedLine[headerLevel] == '#')
                    {
                        headerLevel++;
                    }
                    trimmedLine = trimmedLine.Substring(headerLevel).Trim();
                    fontWeight = FontWeights.Bold;
                    fontSize = Math.Max(12, 24 - (headerLevel * 2));
                    margin = new Thickness(0, 10, 0, 5);
                    currentTextBlock = null; // Headers start a new block
                }
                else if (trimmedLine.StartsWith("- ") || trimmedLine.StartsWith("* "))
                {
                    trimmedLine = "• " + trimmedLine.Substring(2).Trim();
                    margin = new Thickness(15, 2, 0, 2);
                    currentTextBlock = null; // List items start a new block for simplicity
                }

                if (currentTextBlock == null)
                {
                    currentTextBlock = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = fontSize,
                        FontWeight = fontWeight,
                        Margin = margin
                    };
                    panel.Children.Add(currentTextBlock);
                }
                else
                {
                    currentTextBlock.Inlines.Add(new Run("\n"));
                }

                ParseInlineMarkdown(trimmedLine, currentTextBlock);

                // Ensure text right after header does not inherit header styles if it's supposed to be normal paragraph
                if (fontSize > 12 || fontWeight == FontWeights.Bold)
                {
                     currentTextBlock = null;
                }
            }
        }

        private void ParseInlineMarkdown(string text, TextBlock textBlock)
        {
            // Split by regex for bold, italic, underline, and links
            var parts = Regex.Split(text, @"(\*\*[^*]+\*\*|\*[^*]+\*|__[^_]+__|<u>[^<]+</u>|\[[^\]]+\]\([^)]+\)|https?://[^\s]+)");

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                if (part.StartsWith("**") && part.EndsWith("**") && part.Length > 4)
                {
                    textBlock.Inlines.Add(new Run(part.Substring(2, part.Length - 4)) { FontWeight = FontWeights.Bold });
                }
                else if (part.StartsWith("*") && part.EndsWith("*") && part.Length > 2 && !part.StartsWith("**"))
                {
                    textBlock.Inlines.Add(new Run(part.Substring(1, part.Length - 2)) { FontStyle = FontStyles.Italic });
                }
                else if ((part.StartsWith("__") && part.EndsWith("__") && part.Length > 4))
                {
                    var run = new Run(part.Substring(2, part.Length - 4));
                    run.TextDecorations.Add(TextDecorations.Underline);
                    textBlock.Inlines.Add(run);
                }
                else if ((part.StartsWith("<u>") && part.EndsWith("</u>") && part.Length > 7))
                {
                    var run = new Run(part.Substring(3, part.Length - 7));
                    run.TextDecorations.Add(TextDecorations.Underline);
                    textBlock.Inlines.Add(run);
                }
                else if (Regex.IsMatch(part, @"^\[.+\]\(.+\)$"))
                {
                    var match = Regex.Match(part, @"^\[(.+)\]\((.+)\)$");
                    var link = new Hyperlink(new Run(match.Groups[1].Value)) { NavigateUri = new Uri(match.Groups[2].Value) };
                    link.RequestNavigate += (s, e) => {
                         if (e.Uri.Scheme == Uri.UriSchemeHttp || e.Uri.Scheme == Uri.UriSchemeHttps)
                         {
                             Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                         }
                    };
                    textBlock.Inlines.Add(link);
                }
                else if (Regex.IsMatch(part, @"^https?://[^\s]+$"))
                {
                    var link = new Hyperlink(new Run(part)) { NavigateUri = new Uri(part) };
                    link.RequestNavigate += (s, e) => {
                         if (e.Uri.Scheme == Uri.UriSchemeHttp || e.Uri.Scheme == Uri.UriSchemeHttps)
                         {
                             Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                         }
                    };
                    textBlock.Inlines.Add(link);
                }
                else
                {
                    textBlock.Inlines.Add(new Run(part));
                }
            }
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