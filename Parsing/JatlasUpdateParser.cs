using ATT_Wrapper.Interfaces;
using ATT_Wrapper.Models;
using System.Collections.Generic;

namespace ATT_Wrapper.Parsing
    {
    /// <summary>
    /// Парсер логов обновления (git pull / update.bat).
    /// Обрабатывает ошибки Git (сеть, блокировки, доступ) и статусы процесса обновления.
    /// </summary>
    public class JatlasUpdateParser : ILogParser
        {
        /// <summary>
        /// Анализирует строку лога обновления.
        /// </summary>
        /// <param name="line">Строка лога.</param>
        /// <param name="statusFromLine">Статус из цвета строки (не используется в апдейте).</param>
        /// <returns>Результаты разбора (PASS/FAIL/Progress).</returns>
        public IEnumerable<LogResult> Parse(string line, string statusFromLine = null)
            {
            // --- 1. Проверка специфичных ошибок Git (через LogPatterns) ---

            // Критические ошибки сети/SSH
            if (LogPatterns.GitNetworkError.IsMatch(line))
                {
                yield return LogResult.Fail($"Git Network Error: {line.Trim()}", "Update");
                yield break;
                }

            // Фатальные ошибки доступа/репозитория
            var fatalMatch = LogPatterns.GitFatalError.Match(line);
            if (fatalMatch.Success)
                {
                yield return LogResult.Fail($"Git Fatal: {fatalMatch.Groups[1].Value}", "Update");
                yield break;
                }

            // Ошибки блокировки (index.lock)
            if (LogPatterns.GitLockError.IsMatch(line))
                {
                yield return LogResult.Fail("Git Lock Error: Another git process is running", "Update");
                yield break;
                }

            // Ошибки ключей хоста
            if (LogPatterns.GitHostKeyError.IsMatch(line))
                {
                yield return LogResult.Fail("Git Auth: Host key verification failed", "Update");
                yield break;
                }

            // --- 2. Стандартная логика (Process Flow) ---

            if (line.Contains("No internet"))
                {
                yield return LogResult.Progress("Waiting for internet...");
                yield break;
                }

            if (line.Contains("Internet connection detected"))
                {
                yield return LogResult.Pass("Internet connected");
                yield break;
                }

            if (line.Contains("Resetting branch"))
                {
                yield return LogResult.Progress("Git: Pulling...");
                yield break;
                }

            if (line.Contains("Successfully pulled"))
                {
                yield return LogResult.Pass("Repository updated");
                yield break;
                }

            if (line.Contains("Failed to pull"))
                {
                yield return LogResult.Fail("Git: Pull failed");
                yield break;
                }

            if (line.Contains("Already up to date"))
                {
                yield return LogResult.Pass("Repository has no updates");
                yield break;
                }

            if (line.Contains("Installing dependencies"))
                {
                yield return LogResult.Progress("Installing dependencies...");
                yield break;
                }

            if (line.Contains("Installing the current project"))
                {
                yield return LogResult.Pass("Dependencies installed");
                yield break;
                }

            if (line.Contains("Update finished"))
                {
                yield return LogResult.Pass("Update finished");
                yield break;
                }
            }
        }
    }