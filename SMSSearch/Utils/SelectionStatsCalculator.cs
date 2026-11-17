using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SMS_Search.Utils
{
    public sealed record SelectionStatsResult(
        int SelectedCount,
        int DistinctCount,
        int NumericCount,
        decimal? Sum,
        decimal? Average,
        decimal? Min,
        decimal? Max,
        string SummaryText);

    public sealed record ValueFrequencyItem(
        string Value,
        int Count,
        double Percentage)
    {
        public string PercentageText => $"{Percentage:0.0}%";
    }

    public sealed record DetailedSelectionStats(
        int SelectedCount,
        int NonEmptyCount,
        int NullOrEmptyCount,
        int DistinctCount,
        int NumericCount,
        decimal? Sum,
        decimal? Average,
        decimal? Median,
        decimal? Min,
        decimal? Max,
        decimal? Range,
        double? StdDev,
        int ZeroCount,
        int NegativeCount,
        IReadOnlyList<ValueFrequencyItem> TopFrequencies);

    public static class SelectionStatsCalculator
    {
        public static SelectionStatsResult Calculate(IEnumerable<object?> values)
        {
            int selectedCount = 0;
            var distinct = new HashSet<string>(StringComparer.Ordinal);
            int numericCount = 0;
            decimal sum = 0m;
            decimal min = decimal.MaxValue;
            decimal max = decimal.MinValue;

            foreach (var val in values)
            {
                selectedCount++;
                string text = (val == null || val == DBNull.Value) ? string.Empty : val.ToString() ?? string.Empty;
                distinct.Add(text);

                if (TryToDecimal(val, out decimal num))
                {
                    numericCount++;
                    sum += num;
                    if (num < min) min = num;
                    if (num > max) max = num;
                }
            }

            if (selectedCount < 2)
            {
                return new SelectionStatsResult(selectedCount, distinct.Count, numericCount, null, null, null, null, string.Empty);
            }

            string summary = $"Selected: {selectedCount:N0} | Distinct: {distinct.Count:N0}";
            if (numericCount > 0)
            {
                decimal avg = sum / numericCount;
                summary += $" | Sum: {FormatNumber(sum)} | Avg: {FormatNumber(avg)} | Min: {FormatNumber(min)} | Max: {FormatNumber(max)}";
                return new SelectionStatsResult(selectedCount, distinct.Count, numericCount, sum, avg, min, max, summary);
            }

            return new SelectionStatsResult(selectedCount, distinct.Count, 0, null, null, null, null, summary);
        }

        public static DetailedSelectionStats CalculateDetailed(IEnumerable<object?> values, int topN = 10)
        {
            int selectedCount = 0;
            int nullOrEmptyCount = 0;
            var freq = new Dictionary<string, int>(StringComparer.Ordinal);
            var nums = new List<decimal>();
            decimal sum = 0m;
            int zeroCount = 0;
            int negativeCount = 0;

            foreach (var val in values)
            {
                selectedCount++;
                string text = (val == null || val == DBNull.Value) ? string.Empty : val.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(text))
                {
                    nullOrEmptyCount++;
                }

                string displayKey = string.IsNullOrEmpty(text) ? "(empty)" : text;
                freq.TryGetValue(displayKey, out int currentCount);
                freq[displayKey] = currentCount + 1;

                if (TryToDecimal(val, out decimal num))
                {
                    nums.Add(num);
                    sum += num;
                    if (num == 0m) zeroCount++;
                    if (num < 0m) negativeCount++;
                }
            }

            int nonEmptyCount = selectedCount - nullOrEmptyCount;
            int distinctCount = freq.Count;

            IReadOnlyList<ValueFrequencyItem> topFrequencies = selectedCount == 0
                ? Array.Empty<ValueFrequencyItem>()
                : freq
                    .OrderByDescending(kv => kv.Value)
                    .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                    .Take(topN)
                    .Select(kv => new ValueFrequencyItem(kv.Key, kv.Value, (kv.Value * 100.0) / selectedCount))
                    .ToList();

            if (nums.Count == 0)
            {
                return new DetailedSelectionStats(
                    selectedCount, nonEmptyCount, nullOrEmptyCount, distinctCount,
                    0, null, null, null, null, null, null, null, 0, 0,
                    topFrequencies);
            }

            nums.Sort();
            decimal min = nums[0];
            decimal max = nums[^1];
            decimal range = max - min;
            decimal avg = sum / nums.Count;
            decimal median = (nums.Count % 2 == 1)
                ? nums[nums.Count / 2]
                : (nums[(nums.Count / 2) - 1] + nums[nums.Count / 2]) / 2m;

            double avgDbl = (double)avg;
            double variance = 0;
            foreach (var n in nums)
            {
                double diff = (double)n - avgDbl;
                variance += diff * diff;
            }
            double stdDev = Math.Sqrt(variance / nums.Count);

            return new DetailedSelectionStats(
                selectedCount, nonEmptyCount, nullOrEmptyCount, distinctCount,
                nums.Count, sum, avg, median, min, max, range, stdDev, zeroCount, negativeCount,
                topFrequencies);
        }

        private static bool TryToDecimal(object? val, out decimal result)
        {
            result = 0m;
            if (val == null || val == DBNull.Value || val is bool)
            {
                return false;
            }

            switch (val)
            {
                case decimal d:
                    result = d;
                    return true;
                case byte b:
                    result = b;
                    return true;
                case sbyte sb:
                    result = sb;
                    return true;
                case short s:
                    result = s;
                    return true;
                case ushort us:
                    result = us;
                    return true;
                case int i:
                    result = i;
                    return true;
                case uint ui:
                    result = ui;
                    return true;
                case long l:
                    result = l;
                    return true;
                case ulong ul:
                    result = ul;
                    return true;
                case float f when !float.IsNaN(f) && !float.IsInfinity(f):
                    result = (decimal)f;
                    return true;
                case double dbl when !double.IsNaN(dbl) && !double.IsInfinity(dbl):
                    result = (decimal)dbl;
                    return true;
                case string str when !string.IsNullOrWhiteSpace(str):
                    return decimal.TryParse(str.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out result)
                        || decimal.TryParse(str.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out result);
                default:
                    return false;
            }
        }

        public static string FormatNumber(decimal value)
        {
            return value == decimal.Truncate(value)
                ? value.ToString("N0", CultureInfo.CurrentCulture)
                : value.ToString("N2", CultureInfo.CurrentCulture);
        }
    }
}
