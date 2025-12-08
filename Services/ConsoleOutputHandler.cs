using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
        private readonly StreamWriter _fileLogger;
        private readonly object _logLock = new object();

        // Colors and fonts
        private readonly Color _defaultForeColor = Color.Gainsboro;
        private readonly Color _defaultBackColor = Color.FromArgb(30, 30, 30); // Dark background
        private readonly Font _defaultFont = new Font("Consolas", 10F, FontStyle.Regular);
        private readonly Font _boldFont = new Font("Consolas", 10F, FontStyle.Bold);
        private readonly Font _italicFont = new Font("Consolas", 10F, FontStyle.Italic);
        private readonly Font _underlineFont = new Font("Consolas", 10F, FontStyle.Underline);

        // Improved ANSI CSI sequence regex (CSI = ESC [ ... )
        private const string AnsiRegex = @"\x1B\[[0-9;?]*[ -/]*[@-~]";

        // Additional ANSI sequences to handle
        private const string AnsiAllRegex = @"\x1B(\[[0-9;?]*[ -/]*[@-~]|[>=]|[()][AB012]|\][^\x07]*\x07)";

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

            // Setup file logging if path provided
            if (!string.IsNullOrEmpty(logFilePath))
                {
                try
                    {
                    var directory = Path.GetDirectoryName(logFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                        Directory.CreateDirectory(directory);
                        }

                    _fileLogger = new StreamWriter(logFilePath, append: false, Encoding.UTF8)
                        {
                        AutoFlush = true
                        };

                    Log.Information($"Console output will be logged to: {logFilePath}");
                    }
                catch (Exception ex)
                    {
                    Log.Error(ex, "Failed to create console log file");
                    }
                }
            }

        public void ProcessLine(string rawLine, RichTextBox rtbLog)
            {
            if (string.IsNullOrEmpty(rawLine)) return;

            // Log to all destinations
            LogToAllDestinations(rawLine);

            // Create clean version of the line (remove all ANSI codes)
            string plainLine = Regex.Replace(rawLine, AnsiAllRegex, "");

            // Content analysis
            bool isProgress = plainLine.TrimStart().StartsWith("Running task:", StringComparison.OrdinalIgnoreCase);
            bool isInfo = plainLine.Contains("INFO");

            // Pause logic - detect various pause messages
            if (plainLine.IndexOf("Press any key", StringComparison.OrdinalIgnoreCase) >= 0 ||
                plainLine.IndexOf("Press Enter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                plainLine.IndexOf("continue", StringComparison.OrdinalIgnoreCase) >= 0 &&
                plainLine.IndexOf("Press", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                Log.Information($"Pause detected in line: {plainLine}");
                _statusCallback?.Invoke("Finalizing...");
                _enterCallback?.Invoke();
                return;
                }

            // Output to Expert View (RichTextBox) - use UI thread
            if (!isInfo && !isProgress)
                {
                if (rtbLog.InvokeRequired)
                    {
                    rtbLog.BeginInvoke(new Action(() => AppendTextToRichTextBox(rtbLog, rawLine + Environment.NewLine)));
                    }
                else
                    {
                    AppendTextToRichTextBox(rtbLog, rawLine + Environment.NewLine);
                    }
                }

            // Parse to table (Simple View)
            _parser.ParseLine(plainLine,
                (status, msg) => _gridController.HandleLogMessage(status, msg),
                (progMsg) => _statusCallback?.Invoke(progMsg)
            );
            }

        private void LogToAllDestinations(string rawLine)
            {
            lock (_logLock)
                {
                // 1. Log to Visual Studio Debug Output (with ANSI codes visible as hex)
                string debugLine = ConvertAnsiToReadableFormat(rawLine);
                Debug.WriteLine($"[CONSOLE] {debugLine}");

                // 2. Log to Serilog (which can go to file/console based on config)
                Log.Debug("[CONSOLE] {RawOutput}", rawLine);

                // 3. Log to dedicated file if configured
                if (_fileLogger != null)
                    {
                    try
                        {
                        _fileLogger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {rawLine}");
                        }
                    catch (Exception ex)
                        {
                        Log.Error(ex, "Failed to write to console log file");
                        }
                    }
                }
            }

        private string ConvertAnsiToReadableFormat(string line)
            {
            StringBuilder sb = new StringBuilder();
            foreach (char c in line)
                {
                if (c == 0x1B) sb.Append("[ESC]");
                else if (c == '\r') sb.Append("[CR]");
                else if (c == '\n') sb.Append("[LF]");
                else if (c == '\t') sb.Append("[TAB]");
                else if (char.IsControl(c)) sb.Append($"[x{(int)c:X2}]");
                else sb.Append(c);
                }
            return sb.ToString();
            }

        private void AppendTextToRichTextBox(RichTextBox box, string text)
            {
            try
                {
                // Split string into segments: [Text] [Code] [Text] ...
                // Grouping () in Regex.Split keeps the separators in the array
                string[] parts = Regex.Split(text, $"({AnsiRegex})");

                // Store current state
                Color currentForeColor = _defaultForeColor;
                Color currentBackColor = _defaultBackColor;
                FontStyle currentStyle = FontStyle.Regular;

                foreach (string part in parts)
                    {
                    if (string.IsNullOrEmpty(part)) continue;

                    if (part.StartsWith("\x1B["))
                        {
                        // This is an ANSI code -> Apply style
                        ApplyAnsiCode(box, part, ref currentForeColor, ref currentBackColor, ref currentStyle);
                        }
                    else
                        {
                        // This is text -> Check for keywords
                        Color keywordColor = GetKeywordColor(part);

                        // If keyword found (PASS/FAIL), override current color
                        if (keywordColor != _defaultForeColor)
                            {
                            box.SelectionColor = keywordColor;
                            }
                        else
                            {
                            box.SelectionColor = currentForeColor;
                            }

                        box.SelectionBackColor = currentBackColor;
                        box.SelectionFont = new Font(_defaultFont, currentStyle);

                        // Set cursor at the end and write
                        box.SelectionStart = box.TextLength;
                        box.SelectionLength = 0;
                        box.AppendText(part);
                        }
                    }

                // Auto-scroll to bottom
                box.SelectionStart = box.TextLength;
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
                // Extract code content (numbers between [ and m or other terminator)
                var match = Regex.Match(ansiSeq, @"\[([0-9;]+)([a-zA-Z])");
                if (!match.Success) return;

                string command = match.Groups[2].Value;

                // Only process SGR (Select Graphic Rendition) commands (m)
                if (command != "m") return;

                string[] codes = match.Groups[1].Value.Split(';');

                box.SelectionStart = box.TextLength;
                box.SelectionLength = 0;

                int i = 0;
                while (i < codes.Length)
                    {
                    if (int.TryParse(codes[i], out int code))
                        {
                        if (code == 0) // Reset all
                            {
                            currentForeColor = _defaultForeColor;
                            currentBackColor = _defaultBackColor;
                            currentStyle = FontStyle.Regular;
                            }
                        else if (code == 1) // Bold
                            {
                            currentStyle |= FontStyle.Bold;
                            }
                        else if (code == 3) // Italic
                            {
                            currentStyle |= FontStyle.Italic;
                            }
                        else if (code == 4) // Underline
                            {
                            currentStyle |= FontStyle.Underline;
                            }
                        else if (code == 22) // Normal intensity (not bold)
                            {
                            currentStyle &= ~FontStyle.Bold;
                            }
                        else if (code == 23) // Not italic
                            {
                            currentStyle &= ~FontStyle.Italic;
                            }
                        else if (code == 24) // Not underline
                            {
                            currentStyle &= ~FontStyle.Underline;
                            }
                        else if (code >= 30 && code <= 37) // Foreground color (standard)
                            {
                            currentForeColor = GetAnsiColor(code - 30, false);
                            }
                        else if (code == 39) // Default foreground color
                            {
                            currentForeColor = _defaultForeColor;
                            }
                        else if (code >= 40 && code <= 47) // Background color (standard)
                            {
                            currentBackColor = GetAnsiColor(code - 40, false);
                            }
                        else if (code == 49) // Default background color
                            {
                            currentBackColor = _defaultBackColor;
                            }
                        else if (code >= 90 && code <= 97) // Foreground color (bright)
                            {
                            currentForeColor = GetAnsiColor(code - 90, true);
                            }
                        else if (code >= 100 && code <= 107) // Background color (bright)
                            {
                            currentBackColor = GetAnsiColor(code - 100, true);
                            }
                        else if (code == 38 || code == 48) // Extended color (256 or RGB)
                            {
                            bool isFore = code == 38;
                            i++;
                            if (i < codes.Length && int.TryParse(codes[i], out int subcode))
                                {
                                if (subcode == 5 && i + 1 < codes.Length) // 256 color
                                    {
                                    i++;
                                    if (int.TryParse(codes[i], out int colorIndex))
                                        {
                                        Color color = GetAnsi256Color(colorIndex);
                                        if (isFore) currentForeColor = color;
                                        else currentBackColor = color;
                                        }
                                    }
                                else if (subcode == 2 && i + 3 < codes.Length) // RGB
                                    {
                                    i++;
                                    if (int.TryParse(codes[i], out int r))
                                        {
                                        i++;
                                        if (int.TryParse(codes[i], out int g))
                                            {
                                            i++;
                                            if (int.TryParse(codes[i], out int b))
                                                {
                                                Color color = Color.FromArgb(r, g, b);
                                                if (isFore) currentForeColor = color;
                                                else currentBackColor = color;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    i++;
                    }
                }
            catch (Exception ex)
                {
                Log.Debug(ex, "Error parsing ANSI code: {Code}", ansiSeq);
                }
            }

        private Color GetKeywordColor(string text)
            {
            string trimmed = text.TrimStart();

            // Strict check for line starts
            if (trimmed.StartsWith("PASS", StringComparison.OrdinalIgnoreCase)) return Color.LightGreen;
            if (trimmed.StartsWith("FAIL", StringComparison.OrdinalIgnoreCase)) return Color.Salmon;
            if (trimmed.StartsWith("SKIPPED", StringComparison.OrdinalIgnoreCase)) return Color.Gold;
            if (trimmed.Contains("ERROR")) return Color.Salmon;
            if (trimmed.Contains("WARNING")) return Color.Gold;

            // If nothing found - return default color (marker "don't change")
            return _defaultForeColor;
            }

        private Color GetAnsiColor(int code, bool bright)
            {
            switch (code)
                {
                case 0: return bright ? Color.DimGray : Color.Black;
                case 1: return bright ? Color.Red : Color.Maroon;
                case 2: return bright ? Color.Lime : Color.Green;
                case 3: return bright ? Color.Yellow : Color.Olive;
                case 4: return bright ? Color.DodgerBlue : Color.Navy;
                case 5: return bright ? Color.Magenta : Color.Purple;
                case 6: return bright ? Color.Cyan : Color.Teal;
                case 7: return bright ? Color.White : Color.Silver;
                default: return _defaultForeColor;
                }
            }

        private Color GetAnsi256Color(int index)
            {
            // Simple implementation for 256 colors
            if (index < 0 || index > 255) return _defaultForeColor;

            if (index < 16)
                {
                // Standard colors (0-15)
                return GetAnsiColor(index % 8, index >= 8);
                }
            else if (index < 232)
                {
                // 216 color cube (16-231)
                int val = index - 16;
                int r = ( val / 36 ) * 51;
                int g = ( ( val / 6 ) % 6 ) * 51;
                int b = ( val % 6 ) * 51;
                return Color.FromArgb(r, g, b);
                }
            else
                {
                // Grayscale (232-255)
                int gray = ( index - 232 ) * 10 + 8;
                return Color.FromArgb(gray, gray, gray);
                }
            }

        public void Dispose()
            {
            lock (_logLock)
                {
                try
                    {
                    _fileLogger?.Flush();
                    _fileLogger?.Dispose();
                    }
                catch (Exception ex)
                    {
                    Log.Error(ex, "Error disposing ConsoleOutputHandler");
                    }
                }
            }
        }
    }