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
            SetValue(AppSettings.Sections.General, AppSettings.Keys.Eula, "0");
            SetValue(AppSettings.Sections.General, AppSettings.Keys.MainStartupLocation, "Last");
            SetValue(AppSettings.Sections.General, AppSettings.Keys.UnarchiveStartupLocation, "Last");
            SetValue(AppSettings.Sections.General, AppSettings.Keys.MainRememberSize, "0");
            SetValue(AppSettings.Sections.General, AppSettings.Keys.CopyDelimiter, "TAB");
            SetValue(AppSettings.Sections.General, AppSettings.Keys.ToastTimeout, "5");

            // System
            SetValue(AppSettings.Sections.System, AppSettings.Keys.CheckUpdate, "1");

            // Results
            SetValue(AppSettings.Sections.Results, AppSettings.Keys.ShowRowNumbers, "1");
            SetValue(AppSettings.Sections.Results, AppSettings.Keys.HighlightMatches, "1");
            SetValue(AppSettings.Sections.Results, AppSettings.Keys.HighlightColor, "#FFFFE0");
            SetValue(AppSettings.Sections.Results, AppSettings.Keys.ResizeColumns, "1");
            SetValue(AppSettings.Sections.Results, AppSettings.Keys.DescriptionColumns, "1");
            SetValue(AppSettings.Sections.Results, AppSettings.Keys.AutoResizeLimit, "5000");
            SetValue(AppSettings.Sections.Results, AppSettings.Keys.HorizontalScrollSpeed, "16");

            // Editor
            SetValue(AppSettings.Sections.Editor, AppSettings.Keys.EnableIntellisense, "1");
            SetValue(AppSettings.Sections.Editor, AppSettings.Keys.AutoTriggerIntellisense, "1");
            SetValue(AppSettings.Sections.Editor, AppSettings.Keys.IntellisenseStandard, "1");
            SetValue(AppSettings.Sections.Editor, AppSettings.Keys.IntellisenseFunctional, "1");
            SetValue(AppSettings.Sections.Editor, AppSettings.Keys.IntellisenseFull, "1");
            SetValue(AppSettings.Sections.Editor, AppSettings.Keys.IntellisenseStandardAuto, "1");
            SetValue(AppSettings.Sections.Editor, AppSettings.Keys.IntellisenseFunctionalAuto, "1");
            SetValue(AppSettings.Sections.Editor, AppSettings.Keys.IntellisenseFullAuto, "1");
            SetValue(AppSettings.Sections.Editor, AppSettings.Keys.SqlFontFamily, "Consolas");
            SetValue(AppSettings.Sections.Editor, AppSettings.Keys.SqlFontSize, "14");

            // Search
            SetValue(AppSettings.Sections.Search, AppSettings.Keys.SelectCustomSqlOnBuild, "1");
            SetValue(AppSettings.Sections.Search, AppSettings.Keys.AnyMatchDefault, "True");
            SetValue(AppSettings.Sections.Search, AppSettings.Keys.DefaultTab, "Function");
            SetValue(AppSettings.Sections.Search, AppSettings.Keys.DefaultTableAction, "QueryFields");

            // Connection
            SetValue(AppSettings.Sections.Connection, AppSettings.Keys.WindowsAuth, "True");

            // Clean SQL
            SetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.BeautifySql, "1");
            SetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.IndentStringSpaces, "2");
            SetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandCommaLists, "1");
            SetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandBooleanExpressions, "1");
            SetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandCaseExpressions, "1");
            SetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandBetweenConditions, "1");
            SetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.BreakJoinOnSections, "0");
            SetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.UppercaseKeywords, "1");
            SetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.KeywordStandardization, "0");

            // Launcher
            SetValue(AppSettings.Sections.Launcher, AppSettings.Keys.StartWithWindows, "0");

            // Logging
            SetValue(AppSettings.Sections.Logging, AppSettings.Keys.Enabled, "1");
            SetValue(AppSettings.Sections.Logging, AppSettings.Keys.Level, "Info");
            SetValue(AppSettings.Sections.Logging, AppSettings.Keys.Retention, "14");

            // GS1
            SetValue(AppSettings.Sections.Gs1, AppSettings.Keys.MonitorClipboard, "False");

            // Query
            SetValue(AppSettings.Sections.Query, AppSettings.Keys.Function, "F1063, F1064, F1051, F1050, F1081");
            SetValue(AppSettings.Sections.Query, AppSettings.Keys.Totalizer, "F1034, F1039, F1128, F1129, F1179, F1253, F1710, F1131, F1048, F1709");
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
