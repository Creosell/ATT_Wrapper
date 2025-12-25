using ATT_Wrapper.Components;
using ATT_Wrapper.Parsing;
using Serilog;
using System;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ATT_Wrapper.Services
    {
    public class ConsoleOutputHandler : IDisposable
        {
        private readonly ILogParser _parser;
        private readonly ResultsGridController _gridController;
        private readonly Action<string> _statusCallback;
        private readonly Action _enterCallback;
        private readonly ReportStatusManager _statusManager;

        private readonly StringBuilder _lineBuffer = new StringBuilder();
        private readonly object _bufferLock = new object();

        // Regex definitions
        private static readonly Regex AnsiSplitRegex = new Regex(@"(\x1B\[[0-9;?]*[ -/]*[@-~])", RegexOptions.Compiled);
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
            ResultsGridController gridController,
            ReportStatusManager statusManager,
            Action<string> statusCallback,
            Action enterCallback)
            {
            _parser = parser;
            _gridController = gridController;
            _statusManager = statusManager;
            _statusCallback = statusCallback;
            _enterCallback = enterCallback;

            Log.Information("Initialized.");
            }

        public void ProcessLine(string rawChunk, RichTextBox rtbLog)
            {
            if (string.IsNullOrEmpty(rawChunk)) return;

            StatusUpdate(rawChunk);

            string linesToPrint = string.Empty;

            lock (_bufferLock)
                {
                _lineBuffer.Append(rawChunk);

                if (_lineBuffer.Length > 50000)
                    {
                    Log.Warning("Buffer overflow. Force flush.");
                    linesToPrint = _lineBuffer.ToString() + "\n";
                    _lineBuffer.Clear();
                    }
                else
                    {
                    linesToPrint = ExtractCompleteLinesLocked();
                    }

                CheckBufferForPauseLocked();
                }

            if (!string.IsNullOrEmpty(linesToPrint))
                {
                UpdateUi(linesToPrint, rtbLog);
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

                    // 1. Получаем чистую строку (с обработкой \r)
                    string collapsedLine = GetFinalLineState(rawLine);

                    // 2. Пытаемся понять статус по цвету ДО очистки (для оффлайн ошибок)
                    string statusFromLineColor = TryGetStatusFromColor(rawLine);

                    if (!string.IsNullOrEmpty(collapsedLine))
                        {
                        sbFinalOutput.Append(collapsedLine).Append('\n');
                        // 3. Парсим и отправляем в таблицу/статусы
                        ParseCleanLine(collapsedLine, statusFromLineColor);
                        }

                    lastNewlineIndex = i;
                    }
                }

            if (lastNewlineIndex != -1)
                {
                _lineBuffer.Remove(0, lastNewlineIndex + 1);
                }

            return sbFinalOutput.ToString();
            }

        // === ИСПРАВЛЕННЫЙ МЕТОД ПАРСИНГА ===
        private void ParseCleanLine(string lineWithColors, string statusFromLineColor)
            {
            // Удаляем цвета, чтобы парсер мог прочитать текст
            string cleanLine = AnsiAllRegex.Replace(lineWithColors, "").Trim();

            if (!string.IsNullOrWhiteSpace(cleanLine))
                {
                try
                    {
                    // Вызываем парсер с 4 аргументами, как требует новый интерфейс
                    _parser.ParseLine(
                        cleanLine,
                        statusFromLineColor,

                        // Callback onResult (Сюда приходят результаты)
                        (status, msg, group) =>
                        {
                            // Логика: если есть группа (uploader/report) -> в статус бар
                            // Если группы нет (null) -> в таблицу (DGV)

                            bool isStatusUpdate = !string.IsNullOrEmpty(group) &&
                                                 ( group.StartsWith("uploader:") || group == "base report" );

                            if (isStatusUpdate)
                                {
                                _statusManager?.UpdateStatus(group, status);
                                }
                            else
                                {
                                // ЭТОЙ СТРОКИ НЕ БЫЛО ИЛИ ОНА БЫЛА НЕПРАВИЛЬНОЙ -> ПОЭТОМУ DGV НЕ ЗАПОЛНЯЛСЯ
                                _gridController.HandleLogMessage(status, msg);
                                }
                        },

                        // Callback onProgress
                        (progMsg) => _statusCallback?.Invoke(progMsg)
                    );
                    }
                catch (Exception ex)
                    {
                    Log.Error($"Error parsing line: {ex.Message}");
                    }
                }
            }

        private string TryGetStatusFromColor(string line)
            {
            if (line.Contains("[31m")) return "FAIL";
            if (line.Contains("[32m")) return "PASS";
            return null;
            }

        // ... Остальные методы (GetFinalLineState, StatusUpdate, UpdateUi и т.д.) оставляем как есть ...

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

        private void UpdateUi(string finalText, RichTextBox rtbLog)
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
            if (string.IsNullOrEmpty(textToAppend)) return;

            if (rtbLog.InvokeRequired)
                rtbLog.BeginInvoke(new Action(() => AppendTextToRichTextBox(rtbLog, textToAppend)));
            else
                AppendTextToRichTextBox(rtbLog, textToAppend);
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

        private string SanitizeForLog(string input)
            {
            // (Оставьте вашу реализацию или можно удалить, если не используется для отладки)
            if (string.IsNullOrEmpty(input)) return input;
            return input.Replace("\x1b", "[ESC]");
            }

        private void AppendTextToRichTextBox(RichTextBox box, string text)
            {
            try
                {
                string[] parts = AnsiSplitRegex.Split(text);
                Color currentForeColor = box.SelectionColor.Name == "0" ? box.ForeColor : box.SelectionColor;
                Color currentBackColor = box.SelectionBackColor.Name == "0" ? box.BackColor : box.SelectionBackColor;
                FontStyle currentStyle = box.SelectionFont?.Style ?? FontStyle.Regular;
                box.SuspendLayout();
                foreach (string part in parts)
                    {
                    if (string.IsNullOrEmpty(part)) continue;
                    if (part.StartsWith("\x1B[")) ApplyAnsiCode(box, part, ref currentForeColor, ref currentBackColor, ref currentStyle);
                    else
                        {
                        box.SelectionColor = currentForeColor;
                        box.SelectionBackColor = currentBackColor;
                        using (var currentFont = box.SelectionFont)
                            {
                            box.SelectionFont = new Font(box.Font.FontFamily, box.Font.Size, currentStyle);
                            }
                        box.AppendText(part);
                        }
                    }
                box.ResumeLayout();
                box.ScrollToCaret();
                }
            catch (Exception ex) { Log.Error(ex, "Error appending to UI."); }
            }

        private void ApplyAnsiCode(RichTextBox box, string ansiSeq, ref Color fg, ref Color bg, ref FontStyle style)
            {
            var match = Regex.Match(ansiSeq, @"\[([0-9;]*)([a-zA-Z])");
            if (!match.Success || match.Groups[2].Value != "m") return;
            string paramString = match.Groups[1].Value;
            string[] codes = string.IsNullOrEmpty(paramString) ? new[] { "0" } : paramString.Split(';');
            foreach (var codeStr in codes)
                {
                if (int.TryParse(codeStr, out int code))
                    {
                    if (code == 0) { fg = box.ForeColor; bg = box.BackColor; style = FontStyle.Regular; }
                    else if (code == 1) style |= FontStyle.Bold;
                    else if (code == 3) style |= FontStyle.Italic;
                    else if (code == 4) style |= FontStyle.Underline;
                    else if (code == 22) style &= ~FontStyle.Bold;
                    else if (code >= 30 && code <= 37) fg = GetAnsiColor(code - 30, false);
                    else if (code == 39) fg = box.ForeColor;
                    else if (code >= 40 && code <= 47) bg = GetAnsiColor(code - 40, false);
                    else if (code == 49) bg = box.BackColor;
                    else if (code >= 90 && code <= 97) fg = GetAnsiColor(code - 90, true);
                    else if (code >= 100 && code <= 107) bg = GetAnsiColor(code - 100, true);
                    }
                }
            }

        private Color GetAnsiColor(int code, bool bright)
            {
            switch (code)
                {
                case 0: return bright ? Color.FromArgb(118, 118, 118) : Color.Black;
                case 1: return bright ? Color.FromArgb(231, 72, 86) : Color.FromArgb(197, 15, 31);
                case 2: return bright ? Color.FromArgb(22, 198, 12) : Color.FromArgb(19, 161, 14);
                case 3: return bright ? Color.FromArgb(249, 241, 165) : Color.FromArgb(193, 156, 0);
                case 4: return bright ? Color.FromArgb(59, 120, 255) : Color.FromArgb(65, 105, 225);
                case 5: return bright ? Color.FromArgb(180, 0, 158) : Color.FromArgb(136, 23, 152);
                case 6: return bright ? Color.FromArgb(97, 214, 214) : Color.FromArgb(58, 150, 221);
                case 7: return bright ? Color.White : Color.FromArgb(204, 204, 204);
                default: return Color.White;
                }
            }

        public void Dispose()
            {
            Log.Information("Disposing resources.");
            _lineBuffer.Clear();
            }
        }
    }