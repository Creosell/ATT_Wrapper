using ATT_Wrapper.Interfaces;
using ATT_Wrapper.Models;
using ATT_Wrapper.Parsing;
using Serilog;
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace ATT_Wrapper.Services
    {
    /// <summary>
    /// Логика обработки вывода консоли: буферизация, очистка текста, парсинг.
    /// Не зависит от WinForms.
    /// </summary>
    public class ConsoleOutputHandler : IDisposable
        {
        private readonly ILogParser _parser;
        private readonly IResultsGridController _gridController;
        private readonly IReportStatusManager _statusManager;
        private readonly IConsoleRenderer _renderer; // <--- Абстракция отрисовки

        private readonly Action<string> _statusCallback;
        private readonly Action _enterCallback;

        private readonly StringBuilder _lineBuffer = new StringBuilder();
        private readonly object _bufferLock = new object();

        // Регулярки для ОЧИСТКИ и ЛОГИКИ (оставляем тут)
        private static readonly Regex AnsiAllRegex = new Regex(@"(\x1B\[[\x30-\x3F]*[\x20-\x2F]*[\x40-\x7E]|\x1B\][^\x07\x1B]*(\x07|\x1B\\)|\x1B[PX^_].*?(\x07|\x1B\\))", RegexOptions.Compiled);
        private static readonly Regex PausePromptRegex = new Regex(@"(Press any key|Press Enter|any key to continue)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex UiCleanerRegex = new Regex(@"Press any key to continue( \. \. \.)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex LineResetRegex = new Regex(@"(\r|\x1B\[\d+;\d+[Hf]|\x1B\[\d+[Hf])", RegexOptions.Compiled);
        private static readonly Regex WindowTitleRegex = new Regex(@"\x1B\]0;.*?\x07", RegexOptions.Compiled);
        private static readonly Regex CursorForwardRegex = new Regex(@"\x1B\[(\d*)C", RegexOptions.Compiled);
        private static readonly Regex RunningTaskRegex = new Regex(@"Running task:\s+([^\x1b]+)(?:.*?)(\d+:\d+(?::\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex NetworkTestRegex = new Regex(@"Running task:\s+(Switch network to|Test bitrate.*?network:)\s+([^\x1b]+)(?:.*?)(\d+/\d+s)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public ConsoleOutputHandler(
            ILogParser parser,
            IResultsGridController gridController,
            IReportStatusManager statusManager,
            IConsoleRenderer renderer, // <--- Получаем рендерер через конструктор
            Action<string> statusCallback,
            Action enterCallback)
            {
            _parser = parser;
            _gridController = gridController;
            _statusManager = statusManager;
            _renderer = renderer;
            _statusCallback = statusCallback;
            _enterCallback = enterCallback;
            }

        // Метод больше не принимает RichTextBox!
        public void ProcessLine(string rawChunk)
            {
            if (string.IsNullOrEmpty(rawChunk)) return;

            StatusUpdate(rawChunk);

            string linesToRender = string.Empty;

            lock (_bufferLock)
                {
                _lineBuffer.Append(rawChunk);

                if (_lineBuffer.Length > 50000)
                    {
                    linesToRender = _lineBuffer.ToString() + "\n";
                    _lineBuffer.Clear();
                    }
                else
                    {
                    linesToRender = ExtractCompleteLinesLocked();
                    }

                CheckBufferForPauseLocked();
                }

            if (!string.IsNullOrEmpty(linesToRender))
                {
                ProcessAndRenderText(linesToRender);
                }
            }

        private string ExtractCompleteLinesLocked()
            {
            string currentBuffer = _lineBuffer.ToString();
            if (currentBuffer.IndexOf('\n') == -1) return null;

            var sbFinalOutput = new StringBuilder();
            int lastNewlineIndex = -1;

            for (int i = 0; i < currentBuffer.Length; i++)
                {
                if (currentBuffer[i] == '\n')
                    {
                    int startIndex = lastNewlineIndex + 1;
                    int length = i - startIndex;
                    string rawLine = currentBuffer.Substring(startIndex, length);

                    string collapsedLine = GetFinalLineState(rawLine);
                    string statusFromLineColor = TryGetStatusFromColor(rawLine);

                    if (!string.IsNullOrEmpty(collapsedLine))
                        {
                        sbFinalOutput.Append(collapsedLine).Append('\n');
                        ParseCleanLine(collapsedLine, statusFromLineColor);
                        }

                    lastNewlineIndex = i;
                    }
                }

            if (lastNewlineIndex != -1)
                _lineBuffer.Remove(0, lastNewlineIndex + 1);

            return sbFinalOutput.ToString();
            }

        private void ParseCleanLine(string lineWithColors, string statusFromLineColor)
            {
            string cleanLine = AnsiAllRegex.Replace(lineWithColors, "").Trim();
            if (string.IsNullOrWhiteSpace(cleanLine)) return;

            try
                {
                var results = _parser.Parse(cleanLine, statusFromLineColor);
                foreach (var result in results)
                    {
                    if (result.Level == LogLevel.Progress)
                        {
                        _statusCallback?.Invoke(result.Message);
                        continue;
                        }

                    if (result.GroupKey == "base report") continue;

                    string statusStr = result.Level.ToString().ToUpper();
                    bool isStatusUpdate = !string.IsNullOrEmpty(result.GroupKey) &&
                                          result.GroupKey.StartsWith("uploader:");

                    if (isStatusUpdate)
                        {
                        string uploaderName = result.GroupKey.Replace("uploader:", "");
                        _statusManager?.UpdateStatus(uploaderName, statusStr);
                        }
                    else
                        {
                        if (result.Level == LogLevel.Error && result.Message.Contains("RenderException")) continue;
                        _gridController.HandleLogMessage(statusStr, result.Message);
                        }
                    }
                }
            catch (Exception ex)
                {
                Log.Error($"Error parsing line: {ex.Message}");
                }
            }

        private void ProcessAndRenderText(string finalText)
            {
            var sbUi = new StringBuilder();
            var lines = finalText.Split('\n');

            foreach (var line in lines)
                {
                if (line == "" && lines[lines.Length - 1] == line) continue;

                string cleanContent = AnsiAllRegex.Replace(line, "").Trim();
                if (cleanContent.StartsWith("Running task", StringComparison.OrdinalIgnoreCase)) continue;
                if (cleanContent.Contains("]0;") && cleanContent.Contains("cmd.exe") && cleanContent.Length < 50) continue;

                string lineForUi = UiCleanerRegex.Replace(line, "");
                lineForUi = Regex.Replace(lineForUi, @"\x1B\[\d+(;\d+)?[Hf]", "");
                lineForUi = ReplaceCursorMovementWithSpaces(lineForUi);
                lineForUi = lineForUi.Replace("\x1B[K", "");
                lineForUi = WindowTitleRegex.Replace(lineForUi, "");

                string cleanedForCheck = AnsiAllRegex.Replace(lineForUi, "");
                if (string.IsNullOrWhiteSpace(cleanedForCheck) && !string.IsNullOrWhiteSpace(AnsiAllRegex.Replace(line, ""))) continue;

                sbUi.Append(lineForUi).Append('\n');
                }

            string textToAppend = sbUi.ToString();
            if (!string.IsNullOrEmpty(textToAppend))
                {
                // Вызываем интерфейс рендерера!
                _renderer.AppendText(textToAppend);
                }
            }

        // --- Вспомогательные методы (StatusUpdate, GetFinalLineState, и т.д.) ---
        // (Они остаются такими же, как были, так как не зависят от RichTextBox)

        private string TryGetStatusFromColor(string line)
            {
            if (line.Contains("[31m")) return "FAIL";
            if (line.Contains("[32m")) return "PASS";
            return null;
            }

        private void StatusUpdate(string rawChunk)
            {
            var match = RunningTaskRegex.Match(rawChunk);
            if (match.Success)
                {
                _statusCallback?.Invoke($"Running: {match.Groups[1].Value.Trim()} {match.Groups[2].Value}");
                }
            var netMatch = NetworkTestRegex.Match(rawChunk);
            if (netMatch.Success)
                {
                string prefix = netMatch.Groups[1].Value.StartsWith("Switch", StringComparison.OrdinalIgnoreCase) ? "Switching to" : "Bitrate Test";
                _statusCallback?.Invoke($"Running: {prefix}: {netMatch.Groups[2].Value.Trim()} {netMatch.Groups[3].Value}");
                }
            }

        private string GetFinalLineState(string rawLine)
            {
            if (string.IsNullOrEmpty(rawLine)) return rawLine;
            string trimmed = rawLine.TrimEnd('\r');
            if (!LineResetRegex.IsMatch(trimmed)) return trimmed;

            string[] frames = LineResetRegex.Split(trimmed);
            StringBuilder ansiPrefix = new StringBuilder();
            string finalContent = null;

            for (int i = frames.Length - 1; i >= 0; i--)
                {
                string frame = frames[i];
                if (string.IsNullOrEmpty(frame)) continue;
                if (finalContent == null && !IsJustAnsiOrEmpty(frame)) finalContent = frame;
                else if (finalContent != null)
                    {
                    var ansiCodes = ExtractAnsiCodes(frame);
                    if (!string.IsNullOrEmpty(ansiCodes)) ansiPrefix.Insert(0, ansiCodes);
                    }
                }
            if (finalContent != null && ansiPrefix.Length > 0) return ansiPrefix.ToString() + finalContent;
            return finalContent ?? trimmed;
            }

        private string ExtractAnsiCodes(string text)
            {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var matches = AnsiAllRegex.Matches(text);
            if (matches.Count == 0) return string.Empty;
            StringBuilder sb = new StringBuilder();
            foreach (Match match in matches) sb.Append(match.Value);
            return sb.ToString();
            }

        private bool IsJustAnsiOrEmpty(string text)
            {
            if (string.IsNullOrEmpty(text)) return true;
            string clean = AnsiAllRegex.Replace(text, "").Replace("\r", "").Trim();
            return string.IsNullOrEmpty(clean);
            }

        private void CheckBufferForPauseLocked()
            {
            if (_lineBuffer.Length == 0) return;
            string cleanBuffer = AnsiAllRegex.Replace(_lineBuffer.ToString(), "");
            if (PausePromptRegex.IsMatch(cleanBuffer))
                {
                _lineBuffer.Clear();
                _statusCallback?.Invoke("Auto-continuing...");
                _enterCallback?.Invoke();
                }
            }

        private string ReplaceCursorMovementWithSpaces(string text)
            {
            return CursorForwardRegex.Replace(text, match =>
            {
                string numStr = match.Groups[1].Value;
                int count = string.IsNullOrEmpty(numStr) ? 1 : int.Parse(numStr);
                if (count > 10) return "";
                return new string(' ', count);
            });
            }

        public void Dispose()
            {
            _lineBuffer.Clear();
            }
        }
    }