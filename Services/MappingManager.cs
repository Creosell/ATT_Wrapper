using ATT_Wrapper.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ATT_Wrapper.Services
    {
    /// <summary>
    /// Управляет загрузкой конфигурации маппинга и поиском соответствий в логах.
    /// Реализован как Singleton (Lazy) с предварительной компиляцией регулярных выражений.
    /// </summary>
    public class MappingManager
        {
        // Внутренняя структура для хранения скомпилированной регулярки и данных маппинга
        private class CachedMapping
            {
            public Regex Regex { get; set; }
            public CheckMapping Data { get; set; }
            }

        private List<CachedMapping> _cachedMappings;

        // Путь к конфигу по умолчанию
        private static readonly string DefaultConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mappings.json");

        // Ленивая инициализация Singleton-а
        private static readonly Lazy<MappingManager> _lazyInstance =
            new Lazy<MappingManager>(() => new MappingManager(DefaultConfigPath));

        /// <summary>
        /// Глобальный экземпляр менеджера.
        /// При первом обращении загрузит конфиг из mappings.json.
        /// </summary>
        public static MappingManager Instance => _lazyInstance.Value;

        /// <summary>
        /// Приватный конструктор для Singleton.
        /// </summary>
        private MappingManager(string configPath)
            {
            LoadConfig(configPath);
            }

        // Оставляем возможность создать локальный экземпляр с другим путем, если нужно для тестов
        public MappingManager(string configPath, bool isTest = false)
            {
            _ = isTest;
            LoadConfig(configPath);
            }

        /// <summary>
        /// Загружает JSON и компилирует регулярные выражения один раз.
        /// </summary>
        private void LoadConfig(string path)
            {
            _cachedMappings = new List<CachedMapping>();
            List<CheckMapping> rawMappings = null;

            try
                {
                if (File.Exists(path))
                    {
                    string json = File.ReadAllText(path);
                    rawMappings = JsonConvert.DeserializeObject<List<CheckMapping>>(json);
                    }
                }
            catch
                {
                // Логирование ошибки можно добавить здесь
                }

            // Если загрузка не удалась или файл пуст, инициализируем пустым списком
            if (rawMappings == null) return;

            // Пре-компиляция регулярок
            foreach (var mapping in rawMappings)
                {
                if (!string.IsNullOrWhiteSpace(mapping.Pattern))
                    {
                    try
                        {
                        // RegexOptions.Compiled ускоряет выполнение, но замедляет запуск (идеально для Singleton)
                        var regex = new Regex(mapping.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

                        _cachedMappings.Add(new CachedMapping
                            {
                            Regex = regex,
                            Data = mapping
                            });
                        }
                    catch
                        {
                        // Игнорируем некорректные регулярки в конфиге
                        }
                    }
                }
            }

        /// <summary>
        /// Ищет совпадение в строке лога, используя заранее скомпилированные регулярки.
        /// </summary>
        public (string group, string ufn) IdentifyCheck(string logMessage)
            {
            if (_cachedMappings == null || _cachedMappings.Count == 0 || string.IsNullOrWhiteSpace(logMessage))
                return (null, null);

            // 1. Проходим по кэшированному списку
            var bestCandidate = _cachedMappings
                .Select(m => new
                    {
                    Cached = m,
                    Match = m.Regex.Match(logMessage) // Используем готовую регулярку
                    })
                .Where(x => x.Match.Success)
                // 2. Сортируем по длине паттерна (самый специфичный выигрывает)
                .OrderByDescending(x => x.Cached.Data.Pattern.Length)
                .FirstOrDefault();

            if (bestCandidate != null)
                {
                // 3. Формируем итоговое имя с подстановкой групп ($1, $2...)
                string dynamicUfn = bestCandidate.Match.Result(bestCandidate.Cached.Data.Ufn);
                return (bestCandidate.Cached.Data.Group, dynamicUfn);
                }

            return (null, null);
            }
        }
    }