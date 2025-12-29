using ATT_Wrapper.Components;
using Serilog;
using System;
using System.Collections.Generic;

namespace ATT_Wrapper.Parsing
    {
    /// <summary>
    /// Парсер логов выполнения тестов Jatlas.
    /// Обрабатывает результаты тестов, состояние загрузчиков (Uploaders),
    /// сетевые ошибки и прогресс выполнения задач.
    /// </summary>
    public class JatlasTestParser : ILogParser
        {
        private ParserState _state = ParserState.Standard;
        private bool _nextCloudHasFailed = false;

        private enum ParserState
            {
            Standard,
            AwaitingNextCloudContinuation
            }

        /// <summary>
        /// Инициализирует новый экземпляр парсера.
        /// </summary>
        public JatlasTestParser()
            {
            }

        /// <summary>
        /// Анализирует строку лога и возвращает поток найденных событий.
        /// </summary>
        /// <param name="line">Строка лога.</param>
        /// <param name="statusFromLine">Статус, извлеченный из цвета строки (если есть).</param>
        /// <returns>Перечисление результатов разбора.</returns>
        public IEnumerable<LogResult> Parse(string line, string statusFromLine = null)
            {
            if (_state == ParserState.AwaitingNextCloudContinuation)
                {
                var continuation = HandleNextCloudContinuation(line);
                if (continuation != null)
                    {
                    yield return continuation;
                    yield break;
                    }
                }

            var progress = ParseTaskProgress(line);
            if (progress != null)
                {
                yield return progress;
                }

            var reports = ParseReport(line);
            if (reports != null)
                {
                foreach (var r in reports) yield return r;
                yield break;
                }

            var standard = ParseStandardResult(line);
            if (standard != null)
                {
                yield return standard;
                yield break;
                }

            var uploader = ParseUploader(line, statusFromLine);
            if (uploader != null)
                {
                yield return uploader;
                yield break;
                }

            var netError = ParseNetworkError(line);
            if (netError != null)
                {
                yield return netError;
                yield break;
                }
            }

        /// <summary>
        /// Обрабатывает ошибки сети, DNS и специфические ошибки загрузчиков.
        /// </summary>
        private LogResult ParseNetworkError(string line)
            {
            var issueMatch = LogPatterns.UploaderIssue.Match(line);
            if (issueMatch.Success)
                {
                string uploaderName = issueMatch.Groups[1].Value.ToLower();
                string errorMsg = issueMatch.Groups[2].Value;

                if (IsKnownUploader(uploaderName))
                    {
                    if (uploaderName.Contains("feishu")) uploaderName = "feishubot";

                    Log.Warning($"Detected uploader issue: {uploaderName} - {errorMsg}");
                    return HandleFailLogic(uploaderName, $"SysErr: {errorMsg}");
                    }
                }

            var connMatch = LogPatterns.ConnectionHostError.Match(line);
            if (connMatch.Success)
                {
                return HandleHostError(connMatch.Groups[1].Value);
                }

            var dnsMatch = LogPatterns.DnsResolveError.Match(line);
            if (dnsMatch.Success)
                {
                return HandleHostError(dnsMatch.Groups[1].Value);
                }

            return null;
            }

        /// <summary>
        /// Формирует ошибку на основе хоста, к которому не удалось подключиться.
        /// </summary>
        private LogResult HandleHostError(string host)
            {
            string uploaderName = MapHostToUploader(host);
            if (uploaderName != null)
                {
                Log.Warning($"Detected host error for: {host} -> {uploaderName}");
                return HandleFailLogic(uploaderName, $"Connection failed to {host}");
                }
            return null;
            }

        /// <summary>
        /// Генерирует результат FAIL для указанного загрузчика и обновляет внутреннее состояние.
        /// </summary>
        private LogResult HandleFailLogic(string uploaderName, string message)
            {
            if (uploaderName.IndexOf("nextcloud", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                _nextCloudHasFailed = true;
                _state = ParserState.Standard;
                }
            return LogResult.Fail(message, $"uploader:{uploaderName.ToLower()}");
            }

        /// <summary>
        /// Парсит строки, связанные с работой Uploader-ов (NextCloud, Feishu и т.д.).
        /// </summary>
        private LogResult ParseUploader(string line, string statusFromLine)
            {
            var match = LogPatterns.Uploader.Match(line);
            if (!match.Success) return null;

            var name = match.Groups[1].Value;
            var rawMsg = match.Groups[2].Value;

            string status = !string.IsNullOrEmpty(statusFromLine)
                ? statusFromLine
                : ( rawMsg.IndexOf("fail", StringComparison.OrdinalIgnoreCase) >= 0 ? "FAIL" : "PASS" );

            if (name.Equals("NextCloud", StringComparison.OrdinalIgnoreCase))
                {
                if (status == "FAIL")
                    {
                    _nextCloudHasFailed = true;
                    _state = ParserState.Standard;
                    }
                else
                    {
                    if (_nextCloudHasFailed)
                        {
                        status = "FAIL";
                        }

                    if (status != "FAIL")
                        {
                        var hasLink = rawMsg.Contains(".json") || rawMsg.Contains(".html");
                        _state = hasLink ? ParserState.Standard : ParserState.AwaitingNextCloudContinuation;
                        }
                    }
                }

            Log.Information($"Uploader parsed: {name} [{status}]");
            var level = MapStatusToLevel(status);
            return new LogResult(level, rawMsg, "uploader:" + name.ToLower());
            }

        /// <summary>
        /// Извлекает информацию о прогрессе выполнения текущей задачи или теста сети.
        /// </summary>
        private LogResult ParseTaskProgress(string line)
            {
            var netMatch = LogPatterns.NetworkTest.Match(line);
            if (netMatch.Success)
                {
                var prefix = netMatch.Groups[1].Value.StartsWith("Switch") ? "Switching to" : "Bitrate Test";
                return LogResult.Progress($"{prefix}: {netMatch.Groups[2].Value.Trim()} [{netMatch.Groups[3].Value}]");
                }

            var taskMatch = LogPatterns.RunningTask.Match(line);
            if (taskMatch.Success)
                {
                return LogResult.Progress($"Running: {taskMatch.Groups[1].Value.Trim()} [{taskMatch.Groups[2].Value}]");
                }
            return null;
            }

        /// <summary>
        /// Определяет момент создания отчета (Report created).
        /// </summary>
        private IEnumerable<LogResult> ParseReport(string line)
            {
            var match = LogPatterns.Report.Match(line);
            if (!match.Success) return null;

            Log.Information($"Report created: {match.Groups[1].Value}");

            return new List<LogResult>
            {
                LogResult.Pass($"{match.Groups[1].Value} report created", "base report"),
                LogResult.Progress("Uploading reports...")
            };
            }

        /// <summary>
        /// Обрабатывает продолжение вывода NextCloud (многострочные логи с ссылками).
        /// </summary>
        private LogResult HandleNextCloudContinuation(string line)
            {
            if (line.Contains(".json"))
                {
                _state = ParserState.Standard;
                return LogResult.Pass("Nextcloud: json report", "uploader:nextcloud");
                }
            if (line.Contains(".html"))
                {
                _state = ParserState.Standard;
                return LogResult.Pass("Nextcloud: html report", "uploader:nextcloud");
                }

            if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith(" "))
                {
                _state = ParserState.Standard;
                return null;
                }

            return null;
            }

        /// <summary>
        /// Парсит стандартные результаты тестов (PASS/FAIL/ERROR).
        /// </summary>
        private LogResult ParseStandardResult(string line)
            {
            var match = LogPatterns.StandardResult.Match(line);
            if (!match.Success) return null;

            var status = match.Groups[1].Value.ToUpper();
            var msg = match.Groups[2].Value.Trim();

            Log.Debug($"Standard result: {status} - {msg}");
            return new LogResult(MapStatusToLevel(status), msg);
            }

        /// <summary>
        /// Преобразует строковый статус в LogLevel.
        /// </summary>
        private LogLevel MapStatusToLevel(string status)
            {
            switch (status?.ToUpper())
                {
                case "PASS": return LogLevel.Pass;
                case "FAIL": return LogLevel.Fail;
                case "ERROR": return LogLevel.Error;
                default: return LogLevel.Pass;
                }
            }

        /// <summary>
        /// Проверяет, является ли имя загрузчика известным системе.
        /// </summary>
        private bool IsKnownUploader(string name)
            {
            if (string.IsNullOrEmpty(name)) return false;
            name = name.ToLower();
            return name.Contains("nextcloud") || name.Contains("feishu") || name.Contains("webhook");
            }

        /// <summary>
        /// Сопоставляет хост (из ошибки соединения) с конкретным загрузчиком.
        /// </summary>
        private string MapHostToUploader(string host)
            {
            if (string.IsNullOrEmpty(host)) return null;
            host = host.ToLower();

            if (host.Contains("feishu.cn") || host.Contains("larksuite")) return "feishubot";
            if (host.Contains("calydonqc.com")) return "webhook";
            if (host.Contains("nextcloud")) return "nextcloud";

            return null;
            }
        }
    }