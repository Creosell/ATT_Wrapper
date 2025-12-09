using System;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ATT_Wrapper.Components;
using Serilog;

namespace ATT_Wrapper.Services
    {
    public class ConsoleOutputHandler : IDisposable
        {
        private readonly ILogParser _parser;
        private readonly ResultsGridController _gridController;
        private readonly Action<string> _statusCallback;
        private readonly Action _enterCallback;

        // Buffer for accumulating incomplete lines
        private StringBuilder _lineBuffer = new StringBuilder();


        // Improved Regex for ANSI CSI sequences
        private const string AnsiCsiRegex = @"\x1B\[[\x30-\x3F]*[\x20-\x2F]*[\x40-\x7E]";
        private const string AnsiSplitRegex = @"\x1B\[[0-9;?]*[ -/]*[@-~]";
        private const string AnsiAllRegex = @"(\x1B\[[\x30-\x3F]*[\x20-\x2F]*[\x40-\x7E]|\x1B\][^\x07\x1B]*(\x07|\x1B\\)|\x1B[PX^_].*?(\x07|\x1B\\))";

        public ConsoleOutputHandler(
            ILogParser parser,
            ResultsGridController gridController,
            Action<string> statusCallback,
            Action enterCallback,
            string logFilePath = null)
            {
            _parser = parser;
            _gridController = gridController;
            _statusCallback = statusCallback;
            _enterCallback = enterCallback;

            // Note: logFilePath parameter is kept for compatibility but not used
            // All logging now goes through Serilog to the main log file
            if (logFilePath != null)
                {
                Log.Warning("Console log file path provided but ignored - using main log file");
                }
            }

        public void ProcessLine(string rawChunk, RichTextBox rtbLog)
            {
            if (string.IsNullOrEmpty(rawChunk)) return;

            // 1. Логируем ВСЁ (чтобы в файле была полная история)
            LogRawChunk(rawChunk);

            // 2. Логика фильтрации для UI
            // Убираем ANSI, чтобы проверить текст
            string cleanText = Regex.Replace(rawChunk, AnsiAllRegex, "");

            // Проверяем, начинается ли строка с "Running task"
            bool isRunningTask = cleanText.TrimStart().StartsWith("Running task", StringComparison.OrdinalIgnoreCase);

            // Если это НЕ "Running task" — выводим в RichTextBox
            if (!isRunningTask)
                {
                if (rtbLog.InvokeRequired)
                    {
                    rtbLog.BeginInvoke(new Action(() => AppendTextToRichTextBox(rtbLog, rawChunk)));
                    }
                else
                    {
                    AppendTextToRichTextBox(rtbLog, rawChunk);
                    }
                }

            // 3. Буфер и паузы (оставляем как есть, чтобы логика не ломалась)
            lock (_lineBuffer)
                {
                _lineBuffer.Append(rawChunk);
                if (_lineBuffer.Length > 20000) _lineBuffer.Remove(0, 10000);
                CheckBufferForPauseOrLines();
                }
            }

        private void CheckBufferForPauseOrLines()
            {
            string currentBuffer = _lineBuffer.ToString();

            // 1. Clean ANSI codes for pause detection
            string cleanText = Regex.Replace(currentBuffer, AnsiAllRegex, "");

            // 2. PAUSE DETECTION - Check both cleaned and raw buffer
            // Common pause patterns:
            // - "Press any key to continue"
            // - "Press Enter to continue"
            // - "Press any key" (from pause command)
            bool pauseFound =
                cleanText.IndexOf("Press any key", StringComparison.OrdinalIgnoreCase) >= 0 ||
                cleanText.IndexOf("Press Enter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                cleanText.IndexOf("any key to continue", StringComparison.OrdinalIgnoreCase) >= 0 ||
                currentBuffer.IndexOf("Press any key", StringComparison.OrdinalIgnoreCase) >= 0;

            if (pauseFound)
                {
                Log.Information($"[PAUSE DETECTED] Buffer length: {currentBuffer.Length}");
                Log.Debug($"Clean buffer content: '{cleanText}'");
                Log.Debug($"Raw buffer content: '{currentBuffer}'");

                _lineBuffer.Clear();
                _statusCallback?.Invoke("Auto-continuing...");

                // Trigger the Enter callback
                _enterCallback?.Invoke();
                return;
                }

            // Debug logging for troubleshooting
            if (currentBuffer.Length > 50 &&
                ( currentBuffer.Contains("Press") || currentBuffer.Contains("press") ))
                {
                Log.Debug($"[BUFFER DEBUG] Found 'Press' but no match. Clean: '{cleanText.Substring(0, Math.Min(200, cleanText.Length))}'");
                }

            // 3. Parse complete lines
            int newlineIndex;
            while (( newlineIndex = currentBuffer.IndexOfAny(new[] { '\r', '\n' }) ) >= 0)
                {
                string line = currentBuffer.Substring(0, newlineIndex).Trim();

                // Remove line from buffer (+1 for the newline char)
                _lineBuffer.Remove(0, newlineIndex + 1);
                currentBuffer = _lineBuffer.ToString();

                if (!string.IsNullOrWhiteSpace(line))
                    {
                    // Clean line for parser
                    string cleanLine = Regex.Replace(line, AnsiAllRegex, "");

                    _parser.ParseLine(cleanLine,
                        (status, msg) => _gridController.HandleLogMessage(status, msg),
                        (progMsg) => _statusCallback?.Invoke(progMsg)
                    );
                    }
                }
            }

        private void LogRawChunk(string text)
            {
            // 1. УДАЛЯЕМ эту строку, она пишет "голый" текст мимо логгера
            // Debug.Write(text); 

            // 2. Логируем через Serilog. 
            // ВАЖНО: Используем Log.Debug (или Information), чтобы попало в файл.
            // Убираем ограничение в 200 символов, чтобы текст не пропадал.

            if (!string.IsNullOrEmpty(text))
                {
                // Экранируем переносы строк, чтобы лог файл не "разъезжался",
                // или можно убрать Replace, если хочешь видеть переносы как есть.
                string logText = text.Replace("\r", "\\r").Replace("\n", "\\n");

                Log.Debug($"[Console] {logText}");
                }
            }

        private void AppendTextToRichTextBox(RichTextBox box, string text)
            {
            try
                {
                string[] parts = Regex.Split(text, $"({AnsiSplitRegex})");

                // Use current ANSI settings or defaults
                Color currentForeColor = box.ForeColor;
                Color currentBackColor = box.BackColor;
                FontStyle currentStyle = FontStyle.Regular;

                foreach (string part in parts)
                    {
                    if (string.IsNullOrEmpty(part)) continue;

                    if (part.StartsWith("\x1B["))
                        {
                        ApplyAnsiCode(box, part, ref currentForeColor, ref currentBackColor, ref currentStyle);
                        }
                    else
                        {
                        box.SelectionColor = currentForeColor;
                        box.SelectionBackColor = currentBackColor;

                        // CHANGE: Use the font defined in ThemeManager (box.Font), but apply calculated style (Bold/Italic)
                        box.SelectionFont = new Font(box.Font.FontFamily, box.Font.Size, currentStyle);

                        box.AppendText(part);
                        }
                    }
                box.ScrollToCaret();
                }
            catch (Exception ex)
                {
                Log.Error(ex, "Error appending text to RichTextBox");
                }
            }

        private void ApplyAnsiCode(RichTextBox box, string ansiSeq, ref Color currentForeColor, ref Color currentBackColor, ref FontStyle currentStyle)
            {
            try
                {
                // ВАЖНО: Звездочка * вместо плюса +, чтобы ловить пустые параметры типа \x1b[m
                var match = Regex.Match(ansiSeq, @"\[([0-9;]*)([a-zA-Z])");
                if (!match.Success) return;

                string command = match.Groups[2].Value;
                if (command != "m") return;

                string paramString = match.Groups[1].Value;

                // Пустая строка = сброс (0)
                string[] codes = string.IsNullOrEmpty(paramString) ? new[] { "0" } : paramString.Split(';');

                foreach (var codeStr in codes)
                    {
                    if (int.TryParse(codeStr, out int code))
                        {
                        if (code == 0)
                            {
                            currentForeColor = box.ForeColor;
                            currentBackColor = box.BackColor;
                            currentStyle = FontStyle.Regular;
                            }
                        else if (code == 1) currentStyle |= FontStyle.Bold;
                        else if (code == 3) currentStyle |= FontStyle.Italic;
                        else if (code == 4) currentStyle |= FontStyle.Underline;
                        else if (code == 22) currentStyle &= ~FontStyle.Bold;
                        // Цвета текста
                        else if (code >= 30 && code <= 37) currentForeColor = GetAnsiColor(code - 30, false);
                        else if (code == 39) currentForeColor = box.ForeColor;
                        // Цвета фона
                        else if (code >= 40 && code <= 47) currentBackColor = GetAnsiColor(code - 40, false);
                        else if (code == 49) currentBackColor = box.BackColor;
                        // Яркие цвета текста (AIXterm)
                        else if (code >= 90 && code <= 97) currentForeColor = GetAnsiColor(code - 90, true);
                        // Яркие цвета фона
                        else if (code >= 100 && code <= 107) currentBackColor = GetAnsiColor(code - 100, true);
                        }
                    }
                }
            catch { /* ignore */ }
            }

        private Color GetAnsiColor(int code, bool bright)
            {
            // Схема "Campbell" (Modern Windows CMD) - отлично читается на черном
            switch (code)
                {
                case 0: // Black
                    return bright ? Color.FromArgb(118, 118, 118) : Color.Black;
                case 1: // Red
                    return bright ? Color.FromArgb(231, 72, 86) : Color.FromArgb(197, 15, 31);
                case 2: // Green
                    return bright ? Color.FromArgb(22, 198, 12) : Color.FromArgb(19, 161, 14);
                case 3: // Yellow
                    return bright ? Color.FromArgb(249, 241, 165) : Color.FromArgb(193, 156, 0);
                case 4: // Blue (Сделал поярче, чем стандартный Navy)
                    return bright ? Color.FromArgb(59, 120, 255) : Color.FromArgb(65, 105, 225);
                case 5: // Magenta
                    return bright ? Color.FromArgb(180, 0, 158) : Color.FromArgb(136, 23, 152);
                case 6: // Cyan
                    return bright ? Color.FromArgb(97, 214, 214) : Color.FromArgb(58, 150, 221);
                case 7: // White
                    return bright ? Color.White : Color.FromArgb(204, 204, 204);
                default:
                    return Color.White;
                }
            }

        public void Dispose()
            {
            // Nothing to dispose anymore since we removed the file logger
            }
        }
    }