using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ATT_Wrapper
    {
    /// <summary>
    /// Модель данных для сопоставления строки лога с группой и именем проверки.
    /// </summary>
    public class CheckMapping
        {
        [JsonProperty("pattern")]
        public string Pattern { get; set; }

        [JsonProperty("group")]
        public string Group { get; set; }

        [JsonProperty("ufn")]
        public string Ufn { get; set; }
        }

    /// <summary>
    /// Управляет загрузкой конфигурации маппинга и поиском соответствий в логах.
    /// </summary>
    public class MappingManager
        {
        private List<CheckMapping> _mappings;

        /// <summary>
        /// Инициализирует менеджер и загружает конфигурацию из указанного файла.
        /// </summary>
        /// <param name="configPath">Путь к файлу mappings.json.</param>
        public MappingManager(string configPath)
            {
            LoadConfig(configPath);
            }

        /// <summary>
        /// Загружает и десериализует JSON-конфигурацию.
        /// В случае ошибки инициализирует пустой список, чтобы избежать падения приложения.
        /// </summary>
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
                    _mappings = new List<CheckMapping>();
                    }
                }
            catch
                {
                // В случае поврежденного JSON или ошибки доступа создаем пустой список
                _mappings = new List<CheckMapping>();
                }
            }

        /// <summary>
        /// Ищет совпадение в строке лога. Использует принцип "самого длинного совпадения":
        /// если найдено несколько паттернов, выбирается тот, который длиннее.
        /// </summary>
        /// <param name="logMessage">Анализируемая строка лога.</param>
        /// <returns>Кортеж (Группа, Имя проверки) или (null, null), если совпадение не найдено.</returns>
        public (string group, string ufn) IdentifyCheck(string logMessage)
            {
            if (_mappings == null || string.IsNullOrWhiteSpace(logMessage))
                return (null, null);

            // 1. Находим все паттерны, которые содержатся в строке
            var matches = _mappings.Where(m =>
                logMessage.IndexOf(m.Pattern, StringComparison.OrdinalIgnoreCase) >= 0
            );

            // 2. Сортируем по убыванию длины паттерна и берем самый длинный.
            // Это решает проблему перекрытия (например, "wifi mac" vs "wifi mac address").
            var bestMatch = matches.OrderByDescending(m => m.Pattern.Length).FirstOrDefault();

            if (bestMatch != null)
                {
                return (bestMatch.Group, bestMatch.Ufn);
                }

            return (null, null);
            }
        }
    }