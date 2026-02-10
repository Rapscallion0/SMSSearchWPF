using System;

namespace SMS_Search.Utils
{
    public static class DateUtils
    {
        public static string ToJulian(DateTime? date)
        {
            if (date == null) return string.Empty;
            return date.Value.Year.ToString("0000") + date.Value.DayOfYear.ToString("000");
        }

        public static DateTime? FromJulian(string julianDate)
        {
            if (string.IsNullOrEmpty(julianDate) || julianDate.Length != 7)
                return null;

            try
            {
                int year = int.Parse(julianDate.Substring(0, 4));
                int dayOfYear = int.Parse(julianDate.Substring(4, 3));

                if (dayOfYear < 1 || dayOfYear > 366) return null;

                return new DateTime(year, 1, 1).AddDays(dayOfYear - 1);
            }
            catch
            {
                return null;
            }
        }
    }
}
