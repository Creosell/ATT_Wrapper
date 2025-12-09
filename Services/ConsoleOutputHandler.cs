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

        // Colors and fonts
        private readonly Color _defaultForeColor = Color.Gainsboro;
        private readonly Color _defaultBackColor = Color.FromArgb(30, 30, 30);
        private readonly Font _defaultFont = new Font("Consolas", 10F, FontStyle.Regular);

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

            // 1. Log raw chunk to Serilog (goes to Debug output and main log file)
            LogRawChunk(rawChunk);

            // 2. Output to UI
            if (rtbLog.InvokeRequired)
                {
                rtbLog.BeginInvoke(new Action(() => AppendTextToRichTextBox(rtbLog, rawChunk)));
                }
            else
                {
                AppendTextToRichTextBox(rtbLog, rawChunk);
                }

            // 3. Buffer and check for pause/lines
            lock (_lineBuffer)
                {
                _lineBuffer.Append(rawChunk);

                // Safety cap
                if (_lineBuffer.Length > 20000)
                    {
                    Log.Warning("Line buffer exceeded 20000 chars, trimming...");
                    _lineBuffer.Remove(0, 10000);
                    }

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
                // Split text by ANSI codes
                string[] parts = Regex.Split(text, $"({AnsiSplitRegex})");

                Color currentForeColor = _defaultForeColor;
                Color currentBackColor = _defaultBackColor;
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
                        // Check for keywords first (PASS/FAIL override ANSI colors)
                        Color wordColor = GetKeywordColor(part);

                        if (wordColor != _defaultForeColor)
                            box.SelectionColor = wordColor;
                        else
                            box.SelectionColor = currentForeColor;

                        box.SelectionBackColor = currentBackColor;
                        box.SelectionFont = new Font(_defaultFont, currentStyle);

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
                var match = Regex.Match(ansiSeq, @"\[([0-9;]+)([a-zA-Z])");
                if (!match.Success) return;

                string command = match.Groups[2].Value;
                if (command != "m") return; // Only handle SGR (Select Graphic Rendition)

                string[] codes = match.Groups[1].Value.Split(';');

                foreach (var codeStr in codes)
                    {
                    if (int.TryParse(codeStr, out int code))
                        {
                        if (code == 0)
                            {
                            currentForeColor = _defaultForeColor;
                            currentBackColor = _defaultBackColor;
                            currentStyle = FontStyle.Regular;
                            }
                        else if (code == 1) currentStyle |= FontStyle.Bold;
                        else if (code == 3) currentStyle |= FontStyle.Italic;
                        else if (code == 4) currentStyle |= FontStyle.Underline;
                        else if (code == 22) currentStyle &= ~FontStyle.Bold;
                        else if (code >= 30 && code <= 37) currentForeColor = GetAnsiColor(code - 30, false);
                        else if (code == 39) currentForeColor = _defaultForeColor;
                        else if (code >= 40 && code <= 47) currentBackColor = GetAnsiColor(code - 40, false);
                        else if (code == 49) currentBackColor = _defaultBackColor;
                        else if (code >= 90 && code <= 97) currentForeColor = GetAnsiColor(code - 90, true);
                        else if (code >= 100 && code <= 107) currentBackColor = GetAnsiColor(code - 100, true);
                        }
                    }
                }
            catch (Exception ex)
                {
                Log.Warning(ex, "Error applying ANSI code");
                }
            }

        private Color GetKeywordColor(string text)
            {
            if (text.Contains("PASS")) return Color.LightGreen;
            if (text.Contains("FAIL")) return Color.Salmon;
            return _defaultForeColor;
            }

        private Color GetAnsiColor(int code, bool bright)
            {
            switch (code)
                {
                case 0: return bright ? Color.DimGray : Color.Black;
                case 1: return bright ? Color.Salmon : Color.Maroon;
                case 2: return bright ? Color.Lime : Color.Green;
                case 3: return bright ? Color.Yellow : Color.Olive;
                case 4: return bright ? Color.DodgerBlue : Color.Navy;
                case 5: return bright ? Color.Magenta : Color.Purple;
                case 6: return bright ? Color.Cyan : Color.Teal;
                case 7: return bright ? Color.White : Color.Silver;
                default: return _defaultForeColor;
                }
            }

        public void Dispose()
            {
            // Nothing to dispose anymore since we removed the file logger
            }
        }
    }