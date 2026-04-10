using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace SMS_Search.Utils
{
    public interface IConfigService
    {
        string? GetValue(string section, string key);
        void SetValue(string section, string key, string value);
        void RemoveValue(string section, string key);
        void ClearSection(string section);
        Dictionary<string, Dictionary<string, string>> GetAllSettings();
        void Save();
        void Load();
    }

    public class ConfigManager : IConfigService
    {
        private Dictionary<string, Dictionary<string, string>> _config = new();
        private readonly string _filePath;

        public ConfigManager(string filePath)
        {
            _filePath = filePath;
            Load();
        }

        public string? GetValue(string section, string key)
        {
            if (_config.TryGetValue(section, out var sectionDict))
            {
                if (sectionDict != null && sectionDict.TryGetValue(key, out var value))
                {
                    return value;
                }
            }
            return null;
        }

        public void SetValue(string section, string key, string value)
        {
            if (!_config.ContainsKey(section))
            {
                _config[section] = new Dictionary<string, string>();
            }
            _config[section][key] = value;
        }

        public void RemoveValue(string section, string key)
        {
            if (_config.TryGetValue(section, out var sectionDict))
            {
                sectionDict.Remove(key);
            }
        }

        public void ClearSection(string section)
        {
            if (_config.ContainsKey(section))
            {
                _config[section].Clear();
            }
        }

        public Dictionary<string, Dictionary<string, string>> GetAllSettings()
        {
            // Deep copy to prevent external modification
            var copy = new Dictionary<string, Dictionary<string, string>>();
            foreach (var section in _config)
            {
                copy[section.Key] = new Dictionary<string, string>(section.Value);
            }
            return copy;
        }

        public void Load()
        {
            bool isNewOrNeedsDefaults = false;
            if (File.Exists(_filePath))
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    _config = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json)
                              ?? new Dictionary<string, Dictionary<string, string>>();

                    if (_config.Count == 0)
                    {
                        isNewOrNeedsDefaults = true;
                    }
                }
                catch
                {
                    _config = new Dictionary<string, Dictionary<string, string>>();
                    isNewOrNeedsDefaults = true;
                }
            }
            else
            {
                _config = new Dictionary<string, Dictionary<string, string>>();
                isNewOrNeedsDefaults = true;
            }

            if (isNewOrNeedsDefaults && !(_filePath.EndsWith("state.json", StringComparison.OrdinalIgnoreCase)))
            {
                PopulateDefaults();
                Save();
            }
        }

        private void PopulateDefaults()
        {
            // General
            SetValue("GENERAL", "EULA", "0");
            SetValue("GENERAL", "CHECKUPDATE", "1");
            SetValue("GENERAL", "MAIN_STARTUP_LOCATION", "Last");
            SetValue("GENERAL", "UNARCHIVE_STARTUP_LOCATION", "Last");
            SetValue("GENERAL", "DEFAULT_TAB", "Function");
            SetValue("GENERAL", "DEFAULT_TABLE_ACTION", "QueryFields");
            SetValue("GENERAL", "MAIN_REMEMBER_SIZE", "0");
            SetValue("GENERAL", "COPY_DELIMITER", "TAB");
            SetValue("GENERAL", "TOAST_TIMEOUT", "5");

            // General - Grid/Results
            SetValue("GENERAL", "SHOW_ROW_NUMBERS", "1");
            SetValue("GENERAL", "HIGHLIGHT_MATCHES", "1");
            SetValue("GENERAL", "HIGHLIGHT_COLOR", "#FFFFE0");
            SetValue("GENERAL", "RESIZECOLUMNS", "1");
            SetValue("GENERAL", "DESCRIPTIONCOLUMNS", "1");
            SetValue("GENERAL", "AUTO_RESIZE_LIMIT", "5000");
            SetValue("GENERAL", "HORIZONTAL_SCROLL_SPEED", "16");

            // General - Editor
            SetValue("GENERAL", "ENABLE_INTELLISENSE", "1");
            SetValue("GENERAL", "AUTO_TRIGGER_INTELLISENSE", "1");
            SetValue("GENERAL", "INTELLISENSE_STANDARD", "1");
            SetValue("GENERAL", "INTELLISENSE_FUNCTIONAL", "1");
            SetValue("GENERAL", "INTELLISENSE_FULL", "1");
            SetValue("GENERAL", "INTELLISENSE_STANDARD_AUTO", "1");
            SetValue("GENERAL", "INTELLISENSE_FUNCTIONAL_AUTO", "1");
            SetValue("GENERAL", "INTELLISENSE_FULL_AUTO", "1");
            SetValue("GENERAL", "SQL_FONT_FAMILY", "Consolas");
            SetValue("GENERAL", "SQL_FONT_SIZE", "14");
            SetValue("GENERAL", "SELECT_CUSTOM_SQL_ON_BUILD", "1");
            SetValue("GENERAL", "ANY_MATCH_DEFAULT", "True");

            // Connection
            SetValue("CONNECTION", "WINDOWSAUTH", "True");

            // Clean SQL
            SetValue("CLEAN_SQL", "BEAUTIFY_SQL", "1");
            SetValue("CLEAN_SQL", "INDENT_STRING_SPACES", "2");
            SetValue("CLEAN_SQL", "EXPAND_COMMA_LISTS", "1");
            SetValue("CLEAN_SQL", "EXPAND_BOOLEAN_EXPRESSIONS", "1");
            SetValue("CLEAN_SQL", "EXPAND_CASE_EXPRESSIONS", "1");
            SetValue("CLEAN_SQL", "EXPAND_BETWEEN_CONDITIONS", "1");
            SetValue("CLEAN_SQL", "BREAK_JOIN_ON_SECTIONS", "0");
            SetValue("CLEAN_SQL", "UPPERCASE_KEYWORDS", "1");
            SetValue("CLEAN_SQL", "KEYWORD_STANDARDIZATION", "0");

            // Launcher
            SetValue("LAUNCHER", "START_WITH_WINDOWS", "0");

            // Logging
            SetValue("LOGGING", "ENABLED", "1");
            SetValue("LOGGING", "LEVEL", "Info");
            SetValue("LOGGING", "RETENTION", "14");

            // GS1
            SetValue("GS1", "MONITOR_CLIPBOARD", "False");

            // Query
            SetValue("QUERY", "FUNCTION", "F1063, F1064, F1051, F1050, F1081");
            SetValue("QUERY", "TOTALIZER", "F1034, F1039, F1128, F1129, F1179, F1253, F1710, F1131, F1048, F1709");
        }

        public void Save()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            var json = JsonSerializer.Serialize(_config, options);
            File.WriteAllText(_filePath, json);
        }
    }
}
