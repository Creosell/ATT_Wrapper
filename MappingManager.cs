using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json; // Требуется NuGet: Newtonsoft.Json

namespace ATT_Wrapper
    {
    public class CheckMapping
        {
        [JsonProperty("pattern")]
        public string Pattern { get; set; }

        [JsonProperty("group")]
        public string Group { get; set; }

        [JsonProperty("ufn")]
        public string Ufn { get; set; }
        }

    public class MappingManager
        {
        private List<CheckMapping> _mappings;

        public MappingManager(string configPath)
            {
            LoadConfig(configPath);
            }

        private void LoadConfig(string path)
            {
            try
                {
                if (File.Exists(path))
                    {
                    string json = File.ReadAllText(path);
                    _mappings = JsonConvert.DeserializeObject<List<CheckMapping>>(json);
                    }
                else
                    {
                    // Fallback, если файла нет
                    _mappings = new List<CheckMapping>();
                    }
                }
            catch
                {
                _mappings = new List<CheckMapping>();
                }
            }

        public (string group, string ufn) IdentifyCheck(string logMessage)
            {
            if (_mappings == null || string.IsNullOrWhiteSpace(logMessage))
                return (null, null);

            var match = _mappings.FirstOrDefault(m =>
                Regex.IsMatch(logMessage, m.Pattern, RegexOptions.IgnoreCase) ||
                logMessage.IndexOf(m.Pattern, StringComparison.OrdinalIgnoreCase) >= 0);

            if (match != null)
                {
                return (match.Group, match.Ufn);
                }

            return (null, null); // Не найдено
            }
        }
    }