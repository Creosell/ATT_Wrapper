using ATT_Wrapper.Components;
using System;
using System.Collections.Generic;

namespace ATT_Wrapper.Parsing
    {
    public class JatlasTestParser : ILogParser
        {
        private const int MaxRenderErrors = 100;
        private readonly List<string> _renderErrors = new List<string>();
        private readonly Action<int, string> _updateRowCallback;
        private ParserState _state = ParserState.Standard;
        private bool _nextCloudHasFailed = false;

        private enum ParserState
            {
            Standard,
            AwaitingNextCloudContinuation
            }

        public JatlasTestParser(Action<int, string> updateRowCallback = null)
            {
            _updateRowCallback = updateRowCallback;
            }

        public bool ParseLine(string line, string statusFromLine,
                             Action<string, string, string> onResult,
                             Action<string> onProgress)
            {
            // State machine handling
            if (_state == ParserState.AwaitingNextCloudContinuation)
                {
                if (TryHandleNextCloudContinuation(line, onProgress))
                    return true;
                }

            // Try handlers in priority order
            if (CheckAndFlushRenderErrors(line, onResult)) return false;
            if (TryHandleRenderErrors(line)) return true;
            if (TryHandleTaskProgress(line, onProgress)) return false;
            if (TryHandleReport(line, onResult, onProgress)) return true;
            if (TryHandleStandardResult(line, onResult)) return true;
            if (TryHandleUploaders(line, statusFromLine, onResult)) return true;
            if (TryHandleNetworkErrors(line, onResult)) return true;

            return false;
            }

        // --- NEW HANDLER ---
        private bool TryHandleNetworkErrors(string line, Action<string, string, string> onResult)
            {
            // 1. Проверяем явные логи загрузчиков (WARNING или ERROR)
            var issueMatch = LogPatterns.UploaderIssue.Match(line);
            if (issueMatch.Success)
                {
                string uploaderName = issueMatch.Groups[1].Value.ToLower(); // "nextcloud"
                string errorMsg = issueMatch.Groups[2].Value;

                if (IsKnownUploader(uploaderName))
                    {
                    if (uploaderName.Contains("feishu")) uploaderName = "feishubot";
                    SendFail(uploaderName, $"SysErr: {errorMsg}", onResult);
                    return true;
                    }
                }

            // 2. Ищем хост в ошибках ConnectionPool (host='...')
            var connMatch = LogPatterns.ConnectionHostError.Match(line);
            if (connMatch.Success)
                {
                string host = connMatch.Groups[1].Value;
                return HandleHostError(host, onResult);
                }

            // 3. Ищем хост в ошибках DNS (Failed to resolve '...')
            var dnsMatch = LogPatterns.DnsResolveError.Match(line);
            if (dnsMatch.Success)
                {
                string host = dnsMatch.Groups[1].Value;
                return HandleHostError(host, onResult);
                }

            return false;
            }

        private bool HandleHostError(string host, Action<string, string, string> onResult)
            {
            string uploaderName = MapHostToUploader(host);
            if (uploaderName != null)
                {
                SendFail(uploaderName, $"Connection failed to {host}", onResult);
                return true;
                }
            return false;
            }

        private bool IsKnownUploader(string name)
            {
            if (string.IsNullOrEmpty(name)) return false;
            name = name.ToLower();
            return name.Contains("nextcloud") || name.Contains("feishu") || name.Contains("webhook");
            }

        private string MapHostToUploader(string host)
            {
            if (string.IsNullOrEmpty(host)) return null;
            host = host.ToLower();

            if (host.Contains("feishu.cn") || host.Contains("larksuite")) return "feishubot";
            if (host.Contains("calydonqc.com")) return "webhook";
            if (host.Contains("nextcloud")) return "nextcloud";

            return null;
            }

        private void SendFail(string uploaderName, string message, Action<string, string, string> onResult)
            {
            if (uploaderName.IndexOf("nextcloud", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                _nextCloudHasFailed = true;
                _state = ParserState.Standard;
                }

            string mappingKey = $"uploader:{uploaderName.ToLower()}";
            onResult?.Invoke("FAIL", message, mappingKey);
            }

        private bool TryHandleUploaders(string line, string statusFromLine, Action<string, string, string> onResult)
            {
            var match = LogPatterns.Uploader.Match(line);
            if (!match.Success) return false;

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
                    // Если NextCloud уже падал, принудительно ставим FAIL даже при успехе (например html.zip)
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

            string mappingKey = "uploader:" + name.ToLower();
            onResult?.Invoke(status, rawMsg, mappingKey);
            return true;
            }


        // --- Existing Internal Handlers (CheckAndFlushRenderErrors, etc.) ---
        // (Остальной код методов оставляешь без изменений, как в твоем файле)

        private bool CheckAndFlushRenderErrors(string line, Action<string, string, string> onResult)
            {
            if (!line.Contains("Summary: Time:") || _renderErrors.Count == 0) return false;
            foreach (var err in _renderErrors) onResult?.Invoke("ERROR", "Render: " + err, "Reports");
            _renderErrors.Clear();
            return false;
            }

        private bool TryHandleRenderErrors(string line)
            {
            var match = LogPatterns.RenderException.Match(line);
            if (!match.Success) return false;
            if (_renderErrors.Count >= MaxRenderErrors) return true;

            var errorDetails = match.Groups[1].Value.Replace("(", ": ").Replace("))", "");
            var templateCode = match.Groups[2].Value.Trim();
            _renderErrors.Add(string.Format("{0} | TPL: {1}", errorDetails, templateCode));
            return true;
            }

        private bool TryHandleTaskProgress(string line, Action<string> onProgress)
            {
            var netMatch = LogPatterns.NetworkTest.Match(line);
            if (netMatch.Success)
                {
                var prefix = netMatch.Groups[1].Value.StartsWith("Switch") ? "Switching to" : "Bitrate Test";
                onProgress?.Invoke(string.Format("Running: {0}: {1} [{2}]", prefix, netMatch.Groups[2].Value.Trim(), netMatch.Groups[3].Value));
                return true;
                }
            var taskMatch = LogPatterns.RunningTask.Match(line);
            if (taskMatch.Success)
                {
                onProgress?.Invoke(string.Format("Running: {0} [{1}]", taskMatch.Groups[1].Value.Trim(), taskMatch.Groups[2].Value));
                return true;
                }
            return false;
            }

        private bool TryHandleReport(string line, Action<string, string, string> onResult, Action<string> onProgress)
            {
            var match = LogPatterns.Report.Match(line);
            if (!match.Success) return false;
            onResult?.Invoke("PASS", string.Format("{0} report created", match.Groups[1].Value), "base report");
            onProgress?.Invoke("Uploading reports...");
            return true;
            }

        private bool TryHandleNextCloudContinuation(string line, Action<string> onProgress)
            {
            if (line.Contains(".json"))
                {
                _state = ParserState.Standard;
                _updateRowCallback?.Invoke(-1, "Nextcloud: json report");
                onProgress?.Invoke("Nextcloud: json report");
                return true;
                }
            if (line.Contains(".html"))
                {
                _state = ParserState.Standard;
                _updateRowCallback?.Invoke(-1, "Nextcloud: html report");
                onProgress?.Invoke("Nextcloud: html report");
                return true;
                }
            if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith(" "))
                {
                _state = ParserState.Standard;
                return false;
                }
            return false;
            }

        private bool TryHandleStandardResult(string line, Action<string, string, string> onResult)
            {
            var match = LogPatterns.StandardResult.Match(line);
            if (!match.Success) return false;
            onResult?.Invoke(match.Groups[1].Value.ToUpper(), match.Groups[2].Value.Trim(), null);
            return true;
            }
        }
    }