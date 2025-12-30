using ATT_Wrapper.Models;
using System.Collections.Generic;

namespace ATT_Wrapper.Interfaces
    {
    public interface ILogParser
        {
        /// <summary>
        /// Парсит строку лога и возвращает коллекцию событий.
        /// Возвращает пустую коллекцию, если строка не содержит полезной информации.
        /// </summary>
        /// <param name="line">Очищенная строка лога</param>
        /// <param name="statusFromLine">Статус, определенный по цвету строки (опционально)</param>
        /// <returns>Поток результатов (Progress, Result, Error и т.д.)</returns>
        IEnumerable<LogResult> Parse(string line, string statusFromLine = null);
        }
    }