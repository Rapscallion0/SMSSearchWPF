using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SMS_Search.Models.Gs1;
using SMS_Search.Utils;

namespace SMS_Search.Services.Gs1
{
    public class Gs1Repository : IGs1Repository
    {
        private const string DictionaryUrl = "https://raw.githubusercontent.com/gs1/gs1-syntax-dictionary/master/gs1-syntax-dictionary.txt";
        private readonly ILoggerService _logger;
        private List<Gs1AiDefinition>? _cachedDefinitions;

        public Gs1Repository(ILoggerService logger)
        {
            _logger = logger;
        }

        private string GetCachePath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDir, "gs1-dictionary.json");
        }

        public async Task<List<Gs1AiDefinition>> DownloadAndCacheAiDefinitionsAsync()
        {
            try
            {
                _logger.LogInfo($"Downloading GS1 syntax dictionary from {DictionaryUrl}...");
                using var client = new HttpClient();
                string content = await client.GetStringAsync(DictionaryUrl);

                var definitions = ParseDictionaryText(content);

                string json = JsonSerializer.Serialize(definitions, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(GetCachePath(), json);

                _cachedDefinitions = definitions;
                _logger.LogInfo($"Successfully downloaded, parsed, and cached {definitions.Count} AI definitions.");
                return definitions;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to download and cache GS1 syntax dictionary.", ex);
                throw;
            }
        }

        public async Task<List<Gs1AiDefinition>> GetAiDefinitionsAsync()
        {
            if (_cachedDefinitions != null) return _cachedDefinitions;

            string cachePath = GetCachePath();
            if (File.Exists(cachePath))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(cachePath);
                    _cachedDefinitions = JsonSerializer.Deserialize<List<Gs1AiDefinition>>(json);
                    if (_cachedDefinitions != null && _cachedDefinitions.Count > 0)
                    {
                        return _cachedDefinitions;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to read cached GS1 dictionary.", ex);
                }
            }

            // Fallback: Download if not cached or corrupted
            return await DownloadAndCacheAiDefinitionsAsync();
        }

        private List<Gs1AiDefinition> ParseDictionaryText(string text)
        {
            var defs = new List<Gs1AiDefinition>();
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;

                // Format: AI    Flags  Specification    Attributes   # Title
                // Example: 01   *?  N14,csum,gcppos2  ex=255,37 dlpkey=22,10,21|235  # GTIN
                var match = Regex.Match(line, @"^(\S+)\s+([\*!\?""\$%\&'\(\)\+,\-\./:;<=>@\[\\\]\^_`\{\|\}~]+)?\s+(\S+(?:\s+\[\S+\])?)\s+(.*?)\s*#\s*(.*)$");

                if (match.Success)
                {
                    string aiCode = match.Groups[1].Value.Trim();
                    string flags = match.Groups[2].Value.Trim();
                    string spec = match.Groups[3].Value.Trim();
                    string attr = match.Groups[4].Value.Trim();
                    string title = match.Groups[5].Value.Trim();

                    // If it's a range like 3100-3105, we expand it
                    if (aiCode.Contains("-"))
                    {
                        var parts = aiCode.Split('-');
                        if (parts.Length == 2 && int.TryParse(parts[0], out int start) && int.TryParse(parts[1], out int end))
                        {
                            for (int i = start; i <= end; i++)
                            {
                                defs.Add(CreateDefinition(i.ToString($"D{parts[0].Length}"), flags, spec, attr, title));
                            }
                        }
                    }
                    else
                    {
                        defs.Add(CreateDefinition(aiCode, flags, spec, attr, title));
                    }
                }
            }

            // Ensure 8110 and 8112 Databar Coupon AIs are present
            if (!defs.Exists(d => d.Ai == "8110"))
            {
                defs.Add(CreateDefinition("8110", "?", "X..70,couponcode", "", "Coupon code"));
            }
            if (!defs.Exists(d => d.Ai == "8112"))
            {
                defs.Add(CreateDefinition("8112", "?", "X..70,couponposoffer", "", "Paperless coupon format"));
            }

            return defs;
        }

        private Gs1AiDefinition CreateDefinition(string aiCode, string flags, string spec, string attr, string title)
        {
            var def = new Gs1AiDefinition
            {
                Ai = aiCode,
                Flags = flags,
                Specification = spec,
                Attributes = attr,
                Title = title,
                IsVariableLength = false
            };

            // Parse spec for DataType, MinLength, MaxLength, and IsVariableLength
            // Example spec: "N14", "X..20", "N..6", "N13,csum..."
            var typeMatch = Regex.Match(spec, @"^([A-Z])(\.\.)?(\d+)");
            if (typeMatch.Success)
            {
                def.DataType = typeMatch.Groups[1].Value;
                def.IsVariableLength = typeMatch.Groups[2].Success;
                def.MaxLength = int.Parse(typeMatch.Groups[3].Value);
                def.MinLength = def.IsVariableLength ? 1 : def.MaxLength;
            }

            return def;
        }
    }
}
