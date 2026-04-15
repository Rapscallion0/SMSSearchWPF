namespace SMS_Search.Utils
{
    public static class AppSettings
    {
        public static class Sections
        {
            public const string General = "GENERAL";
            public const string Results = "RESULTS";
            public const string Editor = "EDITOR";
            public const string Search = "SEARCH";
            public const string System = "SYSTEM";
            public const string Connection = "CONNECTION";
            public const string CleanSql = "CLEAN_SQL";
            public const string Launcher = "LAUNCHER";
            public const string Logging = "LOGGING";
            public const string Gs1 = "GS1";
            public const string Query = "QUERY";
            public const string Main = "MAIN"; // Used in StateManager
        }

        public static class Keys
        {
            // General
            public const string AlwaysOnTop = "ALWAYSONTOP";
            public const string ShowInTray = "SHOWINTRAY";
            public const string MainStartupLocation = "MAIN_STARTUP_LOCATION";
            public const string UnarchiveStartupLocation = "UNARCHIVE_STARTUP_LOCATION";
            public const string MainRememberSize = "MAIN_REMEMBER_SIZE";
            public const string CopyDelimiter = "COPY_DELIMITER";
            public const string CopyDelimiterCustom = "COPY_DELIMITER_CUSTOM";
            public const string ToastTimeout = "TOAST_TIMEOUT";
            public const string Eula = "EULA";
            public const string CopyCleanSql = "COPYCLEANSQL";

            // System
            public const string CheckUpdate = "CHECKUPDATE";

            // Results
            public const string ShowRowNumbers = "SHOW_ROW_NUMBERS";
            public const string HighlightMatches = "HIGHLIGHT_MATCHES";
            public const string HighlightColor = "HIGHLIGHT_COLOR";
            public const string ResizeColumns = "RESIZECOLUMNS";
            public const string DescriptionColumns = "DESCRIPTIONCOLUMNS";
            public const string AutoResizeLimit = "AUTO_RESIZE_LIMIT";
            public const string HorizontalScrollSpeed = "HORIZONTAL_SCROLL_SPEED";

            // Editor
            public const string EnableIntellisense = "ENABLE_INTELLISENSE";
            public const string AutoTriggerIntellisense = "AUTO_TRIGGER_INTELLISENSE";
            public const string IntellisenseStandard = "INTELLISENSE_STANDARD";
            public const string IntellisenseFunctional = "INTELLISENSE_FUNCTIONAL";
            public const string IntellisenseFull = "INTELLISENSE_FULL";
            public const string IntellisenseStandardAuto = "INTELLISENSE_STANDARD_AUTO";
            public const string IntellisenseFunctionalAuto = "INTELLISENSE_FUNCTIONAL_AUTO";
            public const string IntellisenseFullAuto = "INTELLISENSE_FULL_AUTO";
            public const string SqlFontFamily = "SQL_FONT_FAMILY";
            public const string SqlFontSize = "SQL_FONT_SIZE";

            // Search
            public const string DefaultTab = "DEFAULT_TAB";
            public const string DefaultTableAction = "DEFAULT_TABLE_ACTION";
            public const string SelectCustomSqlOnBuild = "SELECT_CUSTOM_SQL_ON_BUILD";
            public const string AnyMatchDefault = "ANY_MATCH_DEFAULT";

            // Connection
            public const string Server = "SERVER";
            public const string Database = "DATABASE";
            public const string WindowsAuth = "WINDOWSAUTH";
            public const string SqlUser = "SQLUSER";
            public const string SqlPassword = "SQLPASSWORD";

            // Clean SQL
            public const string BeautifySql = "BEAUTIFY_SQL";
            public const string IndentStringSpaces = "INDENT_STRING_SPACES";
            public const string ExpandCommaLists = "EXPAND_COMMA_LISTS";
            public const string ExpandBooleanExpressions = "EXPAND_BOOLEAN_EXPRESSIONS";
            public const string ExpandCaseExpressions = "EXPAND_CASE_EXPRESSIONS";
            public const string ExpandBetweenConditions = "EXPAND_BETWEEN_CONDITIONS";
            public const string ExpandInLists = "EXPAND_IN_LISTS";
            public const string BreakJoinOnSections = "BREAK_JOIN_ON_SECTIONS";
            public const string UppercaseKeywords = "UPPERCASE_KEYWORDS";
            public const string KeywordStandardization = "KEYWORD_STANDARDIZATION";
            public const string CleanSqlCount = "Count";
            // Note: Rules are dynamically generated "Rule_X_Regex", "Rule_X_Replace"

            // Launcher
            public const string StartWithWindows = "START_WITH_WINDOWS";
            public const string Hotkey = "HOTKEY";

            // Logging
            public const string Enabled = "ENABLED";
            public const string Level = "LEVEL";
            public const string Retention = "RETENTION";

            // GS1
            public const string MonitorClipboard = "MONITOR_CLIPBOARD";

            // Query
            public const string Function = "FUNCTION";
            public const string Totalizer = "TOTALIZER";

            // State (MAIN)
            public const string SearchHeight = "SEARCH_HEIGHT";
            public const string LastW = "LAST_W";
            public const string LastH = "LAST_H";
            public const string LastX = "LAST_X";
            public const string LastY = "LAST_Y";
            public const string LastTab = "LAST_TAB";
        }
    }
}