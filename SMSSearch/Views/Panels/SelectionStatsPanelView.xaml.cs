using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using SMS_Search.Services;
using SMS_Search.Utils;

namespace SMS_Search.Views.Panels
{
    public partial class SelectionStatsPanelView : System.Windows.Controls.UserControl
    {
        private DetailedSelectionStats? _currentStats;

        public SelectionStatsPanelView()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                WeakReferenceMessenger.Default.UnregisterAll(this);
                WeakReferenceMessenger.Default.Register<SelectionStatsUpdatedMessage>(this, (r, m) =>
                {
                    Dispatcher.InvokeAsync(() => UpdateStats(m.Value));
                });
            };
            Unloaded += (_, _) => WeakReferenceMessenger.Default.UnregisterAll(this);
        }

        public void UpdateStats(DetailedSelectionStats stats)
        {
            _currentStats = stats;

            TxtSelected.Text = stats.SelectedCount.ToString("N0", CultureInfo.CurrentCulture);
            TxtNonEmpty.Text = stats.NonEmptyCount.ToString("N0", CultureInfo.CurrentCulture);
            TxtNullEmpty.Text = stats.NullOrEmptyCount.ToString("N0", CultureInfo.CurrentCulture);
            TxtDistinct.Text = stats.DistinctCount.ToString("N0", CultureInfo.CurrentCulture);

            if (stats.NumericCount > 0)
            {
                NumericGroupBox.Visibility = Visibility.Visible;
                TxtNumericCount.Text = stats.NumericCount.ToString("N0", CultureInfo.CurrentCulture);
                TxtSum.Text = stats.Sum.HasValue ? SelectionStatsCalculator.FormatNumber(stats.Sum.Value) : "-";
                TxtAvg.Text = stats.Average.HasValue ? SelectionStatsCalculator.FormatNumber(stats.Average.Value) : "-";
                TxtMedian.Text = stats.Median.HasValue ? SelectionStatsCalculator.FormatNumber(stats.Median.Value) : "-";
                TxtStdDev.Text = stats.StdDev.HasValue ? stats.StdDev.Value.ToString("N2", CultureInfo.CurrentCulture) : "-";
                TxtMin.Text = stats.Min.HasValue ? SelectionStatsCalculator.FormatNumber(stats.Min.Value) : "-";
                TxtMax.Text = stats.Max.HasValue ? SelectionStatsCalculator.FormatNumber(stats.Max.Value) : "-";
                TxtRange.Text = stats.Range.HasValue ? SelectionStatsCalculator.FormatNumber(stats.Range.Value) : "-";
                TxtZeros.Text = stats.ZeroCount.ToString("N0", CultureInfo.CurrentCulture);
                TxtNegatives.Text = stats.NegativeCount.ToString("N0", CultureInfo.CurrentCulture);
            }
            else
            {
                NumericGroupBox.Visibility = Visibility.Collapsed;
            }

            FreqGrid.ItemsSource = stats.TopFrequencies;
        }

        private void CopySummary_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStats == null) return;

            var sb = new StringBuilder();
            sb.AppendLine($"Selected: {_currentStats.SelectedCount:N0}");
            sb.AppendLine($"Non-Empty: {_currentStats.NonEmptyCount:N0}");
            sb.AppendLine($"Null/Empty: {_currentStats.NullOrEmptyCount:N0}");
            sb.AppendLine($"Distinct: {_currentStats.DistinctCount:N0}");

            if (_currentStats.NumericCount > 0)
            {
                sb.AppendLine($"Numeric Count: {_currentStats.NumericCount:N0}");
                sb.AppendLine($"Sum: {TxtSum.Text}");
                sb.AppendLine($"Average: {TxtAvg.Text}");
                sb.AppendLine($"Median: {TxtMedian.Text}");
                sb.AppendLine($"Std Dev: {TxtStdDev.Text}");
                sb.AppendLine($"Min: {TxtMin.Text}");
                sb.AppendLine($"Max: {TxtMax.Text}");
                sb.AppendLine($"Range: {TxtRange.Text}");
                sb.AppendLine($"Zeros: {TxtZeros.Text}");
                sb.AppendLine($"Negatives: {TxtNegatives.Text}");
            }

            var services = (System.Windows.Application.Current as App)?.Services;
            services?.GetService<IClipboardService>()?.SetText(sb.ToString().TrimEnd());
            services?.GetService<IDialogService>()?.ShowToast("Selection summary copied", "Selection Stats", ToastType.Success);
        }

        private void CopyFrequencies_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStats == null || _currentStats.TopFrequencies.Count == 0) return;

            var headers = new[] { "Value", "Count", "%" };
            var rows = _currentStats.TopFrequencies
                .Select(f => (IReadOnlyList<string?>)new[] { f.Value, f.Count.ToString("N0", CultureInfo.CurrentCulture), f.PercentageText })
                .ToList();

            var services = (System.Windows.Application.Current as App)?.Services;
            services?.GetService<IClipboardService>()?.SetText(MarkdownTableFormatter.Format(headers, rows));
            services?.GetService<IDialogService>()?.ShowToast("Top frequencies copied as Markdown", "Selection Stats", ToastType.Success);
        }
    }
}

