using SMS_Search.Services;

namespace SMS_Search.Utils
{
    public static class StartupConditionEvaluator
    {
        public static bool CanStart(IConfigService configService, IFieldDictionaryService fieldDictionaryService)
        {
            string server = configService.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.Server) ?? "";
            string database = configService.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.Database) ?? "";

            bool hasDb = !string.IsNullOrWhiteSpace(server) && !string.IsNullOrWhiteSpace(database);
            bool hasCsv = fieldDictionaryService.IsCsvActive() && fieldDictionaryService.GetStatus().IsCsvAvailable;

            return hasDb || hasCsv;
        }
    }
}

