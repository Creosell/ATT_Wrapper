using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ATT_Wrapper
    {
    // Обновленный интерфейс с 3-мя аргументами в onResult (status, message, groupKey)
    public interface ILogParser
        {
        bool ParseLine(string line, string statusFromLine, Action<string, string, string> onResult, Action<string> onProgress);
        }

    // 1. STANDARD TEST PARSER (Ваш обновленный код)
    public class JatlasTestParser : ILogParser
        {
        private int _lastUploaderRowIndex = -1;
        private readonly Action<int, string> _updateRowCallback;
        private readonly List<string> _renderErrors = new List<string>();

        // Regex Definitions
        private static readonly Regex RenderExRegex = new Regex(@"RenderException\((.*?)\)\s+tpl:\s+(.*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex NetworkTestRegex = new Regex(@"Running task:\s+(Switch network to|Test bitrate.*?network:)\s+([^\x1b]+)(?:.*?)(\d+/\d+s)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RunningTaskRegex = new Regex(@"Running task:\s+([^\x1b]+)(?:.*?)((?:\d+:\d+(?::\d+)?)|(?:\d+/\d+s))", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ReportRegex = new Regex(@"<Report:([^>]+)>\s+created", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ResultRegex = new Regex(@"^\s*(PASS|FAIL|ERROR)\s+(.*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex NextCloudMultilineRegex = new Regex(
            @"(?s)(NextCloud).*?(\n|\r)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex UploaderRegex = new Regex(
            @".*?<Uploader:([^>]+)>\s+(.*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public JatlasTestParser(Action<int, string> updateRowCallback)
            {
            _updateRowCallback = updateRowCallback;
            }

        public bool ParseLine(string line, string statusFromLine, Action<string, string, string> onResult, Action<string> onProgress)
            {
            if (CheckAndFlushErrors(line, onResult)) return false;
            if (TryHandleRenderErrors(line)) return true;
            if (TryHandleTaskProgress(line, onProgress)) return false;
            if (TryHandleReport(line, onResult, onProgress)) return true;
            if (TryHandleStandardResult(line, onResult)) return true;
            if (TryHandleUploaders(line, statusFromLine, onResult)) return true;

            return false;
            }

        // --- Sub-Methods ---

        private bool CheckAndFlushErrors(string line, Action<string, string, string> onResult)
            {
            if (line.Contains("Summary: Time:") && _renderErrors.Count > 0)
                {
                foreach (var err in _renderErrors)
                    {
                    // Передаем "Reports" или null, как вам удобнее для группировки ошибок
                    onResult?.Invoke("ERROR", $"Render: {err}", "Reports");
                    }
                _renderErrors.Clear();
                }
            return false;
            }

        private bool TryHandleRenderErrors(string line)
            {
            var match = RenderExRegex.Match(line);
            if (match.Success)
                {
                string errorDetails = match.Groups[1].Value.Replace("(", ": ").Replace("))", "");
                string templateCode = match.Groups[2].Value.Trim();
                _renderErrors.Add($"{errorDetails} | TPL: {templateCode}");
                return true;
                }
            return false;
            }

        private bool TryHandleTaskProgress(string line, Action<string> onProgress)
            {
            var netMatch = NetworkTestRegex.Match(line);
            if (netMatch.Success)
                {
                string opContext = netMatch.Groups[1].Value;
                string networkName = netMatch.Groups[2].Value.Trim();
                string timer = netMatch.Groups[3].Value;
                string prefix = opContext.StartsWith("Switch", StringComparison.OrdinalIgnoreCase) ? "Switching to" : "Bitrate Test";

                onProgress?.Invoke($"Running: {prefix}: {networkName} [{timer}]");
                return true;
                }

            var match = RunningTaskRegex.Match(line);
            if (match.Success)
                {
                string taskName = match.Groups[1].Value.Trim();
                string timer = match.Groups[2].Value;
                onProgress?.Invoke($"Running: {taskName} [{timer}]");
                return true;
                }
            return false;
            }

        private bool TryHandleReport(string line, Action<string, string, string> onResult, Action<string> onProgress)
            {
            var match = ReportRegex.Match(line);
            if (match.Success)
                {
                // Ключ "base report" для маппинга
                onResult?.Invoke("PASS", $"{match.Groups[1].Value} report created", "base report");
                onProgress?.Invoke("Uploading reports...");
                return true;
                }
            return false;
            }

        private bool TryHandleUploaders(string line, string statusFromLine, Action<string, string, string> onResult)
            {
            // 1. ЛОГИКА МНОГОСТРОЧНОГО NEXTCLOUD (бывший TryHandleNextCloudMultiline)
            // Если мы находимся в режиме ожидания ссылок от NextCloud
            if (_lastUploaderRowIndex == -2)
                {
                if (line.Contains(".json"))
                    {
                    _updateRowCallback?.Invoke(-1, "Nextcloud: json report");
                    _lastUploaderRowIndex = -1; // Сброс состояния
                    return true;
                    }
                if (line.Contains(".html"))
                    {
                    _updateRowCallback?.Invoke(-1, "Nextcloud: html report");
                    _lastUploaderRowIndex = -1; // Сброс состояния
                    return true;
                    }

                // Проверка условия выхода из блока: если строка не пустая и не начинается с пробела
                if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith(" "))
                    {
                    _lastUploaderRowIndex = -1;
                    // Мы не делаем return true, так как эта строка может быть началом нового Uploader'а
                    // и должна пройти проверку ниже.
                    }
                else
                    {
                    // Если это просто пустая строка или отступ без ссылок — пропускаем
                    return false;
                    }
                }

            // 2. СТАНДАРТНАЯ ЛОГИКА UPLOADER
            var match = UploaderRegex.Match(line);
            if (!match.Success) return false;

            string name = match.Groups[1].Value;
            string rawMsg = match.Groups[2].Value;

            // Определение статуса
            string status;

            if (!string.IsNullOrEmpty(statusFromLine))
                {
                status = statusFromLine;
                }
            else
                {
                status = rawMsg.IndexOf("fail", StringComparison.OrdinalIgnoreCase) >= 0 ? "FAIL" : "PASS";
                }

            string displayMessage;

            if (name.Equals("NextCloud", StringComparison.OrdinalIgnoreCase))
                {
                if (status == "FAIL")
                    {
                    displayMessage = "NextCloud: Upload failed";
                    _lastUploaderRowIndex = -1;
                    }
                else
                    {
                    // Если успех, проверяем, есть ли ссылка прямо в первой строке
                    bool hasLink = rawMsg.Contains(".json") || rawMsg.Contains(".html");

                    if (hasLink)
                        {
                        displayMessage = rawMsg.Contains(".json") ? "NextCloud: JSON" : "NextCloud: HTML";
                        _lastUploaderRowIndex = -1;
                        }
                    else
                        {
                        displayMessage = "NextCloud: Upload successful";
                        // Включаем режим ожидания следующих строк (-2)
                        _lastUploaderRowIndex = -2;
                        }
                    }
                }
            else if (name.Equals("FeishuBot", StringComparison.OrdinalIgnoreCase))
                {
                displayMessage = status == "PASS" ? "FeishuBot: Success" : "FeishuBot: Fail";
                _lastUploaderRowIndex = -1;
                }
            else if (name.Equals("Webhook", StringComparison.OrdinalIgnoreCase))
                {
                displayMessage = status == "PASS" ? "Webhook: Success" : "Webhook: Fail";
                _lastUploaderRowIndex = -1;
                }
            else
                {
                displayMessage = status == "FAIL" ? $"{name}: Operation failed" : $"{name}: Success";
                _lastUploaderRowIndex = -1;
                }

            // Формируем ключ и вызываем результат
            string mappingKey = $"uploader:{name.ToLower()}";
            onResult?.Invoke(status, rawMsg, mappingKey);

            return true;
            }

        private bool TryHandleStandardResult(string line, Action<string, string, string> onResult)
            {
            var match = ResultRegex.Match(line);
            if (match.Success)
                {
                // Группа null (определяется контроллером)
                onResult?.Invoke(match.Groups[1].Value.ToUpper(), match.Groups[2].Value.Trim(), null);
                return true;
                }
            return false;
            }
        }

    // 2. UPDATER PARSER (ИСПРАВЛЕНА СИГНАТУРА)
    public class JatlasUpdateParser : ILogParser
        {
        // Добавлен аргумент string group (Action<string, string, string>)
        public bool ParseLine(string line, string statusFromLine, Action<string, string, string> onResult, Action<string> onProgress)
            {
            // Везде добавляем null третьим аргументом, так как для Update группа не критична
            if (line.Contains("No internet")) { onProgress?.Invoke("Waiting for internet..."); return false; }
            if (line.Contains("Internet connection detected")) { onResult?.Invoke("PASS", "Internet connected", null); return true; }

            if (line.Contains("Resetting branch")) { onProgress?.Invoke("Git: Pulling..."); return false; }
            if (line.Contains("Successfully pulled")) { onResult?.Invoke("PASS", "Repository updated", null); return true; }
            if (line.Contains("Failed to pull")) { onResult?.Invoke("FAIL", "Git: Pull failed", null); return true; }
            if (line.Contains("Already up to date")) { onResult?.Invoke("PASS", "Repository has no updates", null); return true; }

            if (line.Contains("Installing dependencies")) { onProgress?.Invoke("Installing dependencies..."); return false; }
            if (line.Contains("Installing the current project")) { onResult?.Invoke("PASS", "Dependencies installed", null); return true; }
            if (line.Contains("Update finished")) { onResult?.Invoke("PASS", "Update finished", null); return true; }

            return false;
            }
        }

    // 3. AGING PARSER (ИСПРАВЛЕНА СИГНАТУРА)
    public class JatlasAgingParser : ILogParser
        {
        // Добавлен аргумент string group (Action<string, string, string>)
        public bool ParseLine(string line, string statusFromLine, Action<string, string, string> onResult, Action<string> onProgress)
            {
            var resultMatch = Regex.Match(line, @"^\s*(PASS|FAIL|ERROR)\s+(.*)", RegexOptions.IgnoreCase);
            if (resultMatch.Success)
                {
                // Третий аргумент null
                onResult?.Invoke(resultMatch.Groups[1].Value.ToUpper(), resultMatch.Groups[2].Value.Trim(), null);
                return true;
                }
            if (line.Contains("Cycle:"))
                {
                onProgress?.Invoke(line.Trim());
                return false;
                }
            return false;
            }
        }
    }