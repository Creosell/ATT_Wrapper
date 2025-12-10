using System;
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

        // Thread-safe buffer for incomplete lines
        private readonly StringBuilder _lineBuffer = new StringBuilder();
        private readonly object _bufferLock = new object();

        // Optimized Regex: Compiled for performance
        private static readonly Regex AnsiSplitRegex = new Regex(@"(\x1B\[[0-9;?]*[ -/]*[@-~])", RegexOptions.Compiled);
        private static readonly Regex AnsiAllRegex = new Regex(@"(\x1B\[[\x30-\x3F]*[\x20-\x2F]*[\x40-\x7E]|\x1B\][^\x07\x1B]*(\x07|\x1B\\)|\x1B[PX^_].*?(\x07|\x1B\\))", RegexOptions.Compiled);
        private static readonly Regex PausePromptRegex = new Regex(@"(Press any key|Press Enter|any key to continue)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex UiCleanerRegex = new Regex(@"Press any key to continue( \. \. \.)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public ConsoleOutputHandler(
            ILogParser parser,
            ResultsGridController gridController,
            Action<string> statusCallback,
            Action enterCallback)
            {
            _parser = parser;
            _gridController = gridController;
            _statusCallback = statusCallback;
            _enterCallback = enterCallback;

            Log.Information("Initialized.");
            }

        public void ProcessLine(string rawChunk, RichTextBox rtbLog)
            {
            if (string.IsNullOrEmpty(rawChunk)) return;

            // 1. Detailed Logging (File layer typically captures Debug/Verbose)
            LogRawChunk(rawChunk);

            // 2. Buffer Processing (Parser & Pause Detection)
            lock (_bufferLock)
                {
                _lineBuffer.Append(rawChunk);

                // Prevent infinite buffer growth
                if (_lineBuffer.Length > 20000)
                    {
                    Log.Warning("Buffer overflow protected. Trimming start.");
                    _lineBuffer.Remove(0, 10000);
                    }

                ProcessBufferLocked();
                }

            // 3. UI Update Logic
            UpdateUi(rawChunk, rtbLog);
            }

        private void UpdateUi(string rawChunk, RichTextBox rtbLog)
            {
            // Fix glue issues by handling cursor movement ANSI manually
            string processedChunk = rawChunk.Replace("\x1b[1C", " ");

            // Split into lines to apply filters per line
            var lines = processedChunk.Split(new[] { '\n' }, StringSplitOptions.None);
            var sbUi = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
                {
                string line = lines[i];

                // Filter: Remove "Press any key" visual clutter
                string lineForUi = UiCleanerRegex.Replace(line, "");

                // Check content without ANSI
                string contentCheck = AnsiAllRegex.Replace(lineForUi, "").Trim();

                // Filter 1: Skip "Running task"
                if (contentCheck.StartsWith("Running task", StringComparison.OrdinalIgnoreCase))
                    {
                    Log.Debug($"Skipped 'Running task' message: '{contentCheck}'");
                    continue;
                    }

                // Filter 2: Skip CMD title artifacts (ADDED with Logging)
                if (lineForUi.Contains("]0;") && lineForUi.Contains("cmd.exe"))
                    {
                    // Logging at Debug level to keep Console clean (Info level) but capture in File
                    Log.Debug($"Skipped CMD artifact: '{lineForUi.Trim()}'");
                    continue;
                    }

                // Filter 3: Skip purely empty lines unless they are structural newlines
                if (string.IsNullOrWhiteSpace(contentCheck) && string.IsNullOrWhiteSpace(lineForUi))
                    {
                    // Allow one empty line if the original chunk was just a newline
                    if (lines.Length == 1 && string.IsNullOrWhiteSpace(line))
                        {
                        // Keep logic implicit
                        }
                    else if (i < lines.Length - 1)
                        {
                        // Skip empty intermediate lines
                        continue;
                        }
                    }

                sbUi.Append(lineForUi.Replace("\r", ""));

                if (i < lines.Length - 1) sbUi.Append('\n');
                }

            string finalUiText = sbUi.ToString();

            // Restore trailing newline if lost during split
            if (( rawChunk.EndsWith("\n") || rawChunk.EndsWith("\r") ) && !finalUiText.EndsWith("\n") && finalUiText.Length > 0)
                {
                finalUiText += "\n";
                }

            if (string.IsNullOrEmpty(finalUiText)) return;

            // Thread-safe UI update
            if (rtbLog.InvokeRequired)
                {
                rtbLog.BeginInvoke(new Action(() => AppendTextToRichTextBox(rtbLog, finalUiText)));
                }
            else
                {
                AppendTextToRichTextBox(rtbLog, finalUiText);
                }
            }

        private void ProcessBufferLocked()
            {
            string currentBuffer = _lineBuffer.ToString();

            // Optimization: rapid check before heavy regex
            if (currentBuffer.Length == 0) return;

            // 1. Pause Detection
            // We check the raw buffer (cleaned of ANSI) for prompts
            string cleanBuffer = AnsiAllRegex.Replace(currentBuffer, "");

            if (PausePromptRegex.IsMatch(cleanBuffer))
                {
                Log.Information("Pause prompt detected in buffer. Auto-continuing.");
                Log.Verbose($"Pause context: '{cleanBuffer.Trim()}'");

                _lineBuffer.Clear();
                _statusCallback?.Invoke("Auto-continuing...");
                _enterCallback?.Invoke();
                return;
                }

            // 2. Parse Complete Lines
            int newlineIndex;
            // Iterate while there are newlines
            while (( newlineIndex = indexOfNewline(_lineBuffer) ) >= 0)
                {
                // Extract line including valid content before the newline
                string line = _lineBuffer.ToString(0, newlineIndex).Trim();

                // Remove processed line + newline char(s)
                // Need to handle \r\n vs \n correctly for removal
                int removeLength = newlineIndex + 1;
                if (newlineIndex < _lineBuffer.Length - 1 && _lineBuffer[newlineIndex] == '\r' && _lineBuffer[newlineIndex+1] == '\n')
                    {
                    removeLength++;
                    }

                _lineBuffer.Remove(0, removeLength);

                if (!string.IsNullOrWhiteSpace(line))
                    {
                    string cleanLine = AnsiAllRegex.Replace(line, "");
                    if (!string.IsNullOrWhiteSpace(cleanLine))
                        {
                        // Pass to external parser
                        try
                            {
                            _parser.ParseLine(cleanLine,
                                (status, msg) => _gridController.HandleLogMessage(status, msg),
                                (progMsg) => _statusCallback?.Invoke(progMsg)
                            );
                            }
                        catch (Exception ex)
                            {
                            Log.Error(ex, $"Error parsing line: {cleanLine}");
                            }
                        }
                    }
                }
            }

        // Helper to find newline in StringBuilder without creating strings
        private int indexOfNewline(StringBuilder sb)
            {
            for (int i = 0; i < sb.Length; i++)
                {
                if (sb[i] == '\n' || sb[i] == '\r') return i;
                }
            return -1;
            }

        private void LogRawChunk(string text)
            {
            if (string.IsNullOrEmpty(text)) return;

            // Convert ALL control characters to readable tags ([ESC], [x07], etc.)
            // so absolutely nothing remains hidden in the text editor.
            string safeText = SanitizeForLog(text);

            Log.Debug($"{safeText}");
            }

        private string SanitizeForLog(string input)
            {
            if (string.IsNullOrEmpty(input)) return input;

            var sb = new StringBuilder(input.Length * 2);

            foreach (char c in input)
                {
                if (c < 32) // Control characters (ASCII 0-31)
                    {
                    switch (c)
                        {
                        case '\r': sb.Append("[CR]"); break;
                        case '\n': sb.Append("[LF]"); break;
                        case '\t': sb.Append("[TAB]"); break;
                        case '\x1b': sb.Append("[ESC]"); break;
                        case '\x07': sb.Append("[BEL]"); break; // The 0x07 you noticed
                        case '\x08': sb.Append("[BS]"); break;  // Backspace
                        default: sb.Append($"[x{(int)c:X2}]"); break; // Any other weird char -> hex
                        }
                    }
                else
                    {
                    sb.Append(c);
                    }
                }
            return sb.ToString();
            }

        private void AppendTextToRichTextBox(RichTextBox box, string text)
            {
            try
                {
                // Use compiled regex split
                string[] parts = AnsiSplitRegex.Split(text);

                // Start with current state
                Color currentForeColor = box.SelectionColor.Name == "0" ? box.ForeColor : box.SelectionColor; // Fallback
                Color currentBackColor = box.SelectionBackColor.Name == "0" ? box.BackColor : box.SelectionBackColor;
                FontStyle currentStyle = box.SelectionFont?.Style ?? FontStyle.Regular;

                box.Suspend(); // Performance optimization for bulk updates

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

                        // Apply font style safely
                        using (var currentFont = box.SelectionFont)
                            {
                            box.SelectionFont = new Font(box.Font.FontFamily, box.Font.Size, currentStyle);
                            }

                        box.AppendText(part);
                        }
                    }

                box.Resume();
                box.ScrollToCaret();
                }
            catch (Exception ex)
                {
                Log.Error(ex, "Error appending to UI.");
                }
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
                    if (code == 0)
                        {
                        fg = box.ForeColor;
                        bg = box.BackColor;
                        style = FontStyle.Regular;
                        }
                    else if (code == 1) style |= FontStyle.Bold;
                    else if (code == 3) style |= FontStyle.Italic;
                    else if (code == 4) style |= FontStyle.Underline;
                    else if (code == 22) style &= ~FontStyle.Bold;

                    // Standard Colors
                    else if (code >= 30 && code <= 37) fg = GetAnsiColor(code - 30, false);
                    else if (code == 39) fg = box.ForeColor;
                    else if (code >= 40 && code <= 47) bg = GetAnsiColor(code - 40, false);
                    else if (code == 49) bg = box.BackColor;

                    // Bright Colors
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

    // Extension methods for RichTextBox performance
    public static class RichTextBoxExtensions
        {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, int wParam, ref Point lParam);
        private const int WM_USER = 0x400;
        private const int EM_SETSCROLLPOS = WM_USER + 222;
        private const int EM_GETSCROLLPOS = WM_USER + 221;

        // Helps reduce flicker during updates
        public static void Suspend(this Control control)
            {
            Message msg = Message.Create(control.Handle, 0x000B, IntPtr.Zero, IntPtr.Zero); // WM_SETREDRAW
            NativeWindow window = NativeWindow.FromHandle(control.Handle);
            window.DefWndProc(ref msg);
            }

        public static void Resume(this Control control)
            {
            Message msg = Message.Create(control.Handle, 0x000B, new IntPtr(1), IntPtr.Zero); // WM_SETREDRAW
            NativeWindow window = NativeWindow.FromHandle(control.Handle);
            window.DefWndProc(ref msg);
            control.Invalidate();
            }
        }
    }