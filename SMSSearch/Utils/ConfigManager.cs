using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace SMS_Search.Utils
{
    public interface IConfigService
    {
        string GetValue(string section, string key);
        void SetValue(string section, string key, string value);
        void Save();
        void Load();
    }

    public class ConfigManager : IConfigService
    {
        private Dictionary<string, Dictionary<string, string>> _config;
        private readonly string _filePath;

        public ConfigManager(string filePath)
        {
            _filePath = filePath;
            Load();
        }

        public string GetValue(string section, string key)
        {
            if (_config.TryGetValue(section, out var sectionDict))
            {
                if (sectionDict.TryGetValue(key, out var value))
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

        public void Load()
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    _config = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json)
                              ?? new Dictionary<string, Dictionary<string, string>>();
                }
                catch
                {
                    _config = new Dictionary<string, Dictionary<string, string>>();
                }
            }
            else
            {
                _config = new Dictionary<string, Dictionary<string, string>>();
            }
        }

        public void Save()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_config, options);
            File.WriteAllText(_filePath, json);
        }
    }
}
