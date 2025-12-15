using ATT_Wrapper.Components;
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

        private readonly StringBuilder _lineBuffer = new StringBuilder();
        private readonly object _bufferLock = new object();

        private static readonly Regex AnsiSplitRegex = new Regex(@"(\x1B\[[0-9;?]*[ -/]*[@-~])", RegexOptions.Compiled);
        private static readonly Regex AnsiAllRegex = new Regex(@"(\x1B\[[\x30-\x3F]*[\x20-\x2F]*[\x40-\x7E]|\x1B\][^\x07\x1B]*(\x07|\x1B\\)|\x1B[PX^_].*?(\x07|\x1B\\))", RegexOptions.Compiled);
        private static readonly Regex PausePromptRegex = new Regex(@"(Press any key|Press Enter|any key to continue)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex UiCleanerRegex = new Regex(@"Press any key to continue( \. \. \.)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Catches carriage return (\r) and cursor position (H)
        private static readonly Regex LineResetRegex = new Regex(@"(\r|\x1B\[\d+;\d+[Hf]|\x1B\[\d+[Hf])", RegexOptions.Compiled);

        // Matches OSC 0 sequences (Window Title)
        private static readonly Regex WindowTitleRegex = new Regex(@"\x1B\]0;.*?\x07", RegexOptions.Compiled);
        private static readonly Regex CursorForwardRegex = new Regex(@"\x1B\[(\d*)C", RegexOptions.Compiled);

        // Группа 1: Имя задачи (все до символа ESC)
        // (?:.*?): Незахватывающая группа, лениво пропускаем ANSI-коды и пробелы
        // Группа 2: Таймер (цифры:цифры и опционально :цифры)
        private static readonly Regex RunningTaskRegex = new Regex(
            @"Running task:\s+([^\x1b]+)(?:.*?)(\d+:\d+(?::\d+)?)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Captures: 
        // Group 1: Operation context ("Switch network to" OR "Test bitrate... network:")
        // Group 2: Network Name (e.g. "Ethernet", "Wi-Fi 5G")
        // Group 3: Timer in format "1/120s"
        private static readonly Regex NetworkTestRegex = new Regex(
            @"Running task:\s+(Switch network to|Test bitrate.*?network:)\s+([^\x1b]+)(?:.*?)(\d+/\d+s)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);


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

        private void StatusUpdate (string rawChunk)
            {
            // Task Progress: Ловим все строки Running task
            var match = RunningTaskRegex.Match(rawChunk);

            if (match.Success)
                {
                // Группа 1: Имя задачи (например, "Check current Wifi RSSI")
                string taskName = match.Groups[1].Value.Trim();

                // Группа 2: Таймер (например, "0:00:00")
                string timer = match.Groups[2].Value;

                _statusCallback?.Invoke($"Running: {taskName} {timer}");
                }

            var netMatch = NetworkTestRegex.Match(rawChunk);
            if (netMatch.Success)
                {
                string opContext = netMatch.Groups[1].Value; // "Switch..." or "Test bitrate..."
                string networkName = netMatch.Groups[2].Value.Trim();
                string timer = netMatch.Groups[3].Value;

                // Determine readable prefix based on operation
                string prefix = opContext.StartsWith("Switch", StringComparison.OrdinalIgnoreCase)
                    ? "Switching to"
                    : "Bitrate Test";

                _statusCallback?.Invoke($"Running: {prefix}: {networkName} {timer}");
                return; // Exit early if matched
                }

            }

        public void ProcessLine(string rawChunk, RichTextBox rtbLog)
            {
            if (string.IsNullOrEmpty(rawChunk)) return;

            //LogRawChunk(rawChunk);

            StatusUpdate(rawChunk);

            string linesToPrint = string.Empty;

            lock (_bufferLock)
                {
                _lineBuffer.Append(rawChunk);

                // Buffer overflow protection
                if (_lineBuffer.Length > 50000)
                    {
                    Log.Warning("Buffer overflow. Force flush.");
                    linesToPrint = _lineBuffer.ToString() + "\n";
                    _lineBuffer.Clear();
                    }
                else
                    {
                    // Extract complete lines, squashing animations
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
                    // Extract raw line between newlines
                    int startIndex = lastNewlineIndex + 1;
                    int length = i - startIndex;

                    string rawLine = currentBuffer.Substring(startIndex, length);

                    // Collapse "Loading...[CR]Done" -> "Done"
                    string collapsedLine = GetFinalLineState(rawLine);

                    if (!string.IsNullOrEmpty(collapsedLine))
                        {
                        sbFinalOutput.Append(collapsedLine).Append('\n');
                        ParseCleanLine(collapsedLine);
                        }

                    lastNewlineIndex = i;
                    }
                }

            // Remove processed lines
            if (lastNewlineIndex != -1)
                {
                _lineBuffer.Remove(0, lastNewlineIndex + 1);
                }

            return sbFinalOutput.ToString();
            }

        private string GetFinalLineState(string rawLine)
            {
            if (string.IsNullOrEmpty(rawLine)) return rawLine;

            // Trim trailing \r stuck to \n
            string trimmed = rawLine.TrimEnd('\r');

            if (!LineResetRegex.IsMatch(trimmed)) return trimmed;

            // Split by frame delimiters (\r or cursor codes)
            string[] frames = LineResetRegex.Split(trimmed);

            StringBuilder ansiPrefix = new StringBuilder();
            string finalContent = null;

            // Process frames backwards to find final content and accumulate ANSI codes
            for (int i = frames.Length - 1; i >= 0; i--)
                {
                string frame = frames[i];

                if (string.IsNullOrEmpty(frame)) continue;

                if (finalContent == null && !IsJustAnsiOrEmpty(frame))
                    {
                    finalContent = frame;
                    }
                else if (finalContent != null)
                    {
                    // Collect ANSI codes from overwritten frames
                    var ansiCodes = ExtractAnsiCodes(frame);
                    if (!string.IsNullOrEmpty(ansiCodes))
                        {
                        ansiPrefix.Insert(0, ansiCodes);
                        }
                    }
                }

            if (finalContent != null && ansiPrefix.Length > 0)
                {
                return ansiPrefix.ToString() + finalContent;
                }

            return finalContent ?? trimmed;
            }

        private string ExtractAnsiCodes(string text)
            {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var matches = AnsiAllRegex.Matches(text);
            if (matches.Count == 0) return string.Empty;

            StringBuilder sb = new StringBuilder();
            foreach (Match match in matches)
                {
                sb.Append(match.Value);
                }
            return sb.ToString();
            }

        private bool IsJustAnsiOrEmpty(string text)
            {
            if (string.IsNullOrEmpty(text)) return true;
            // Remove ANSI to check for real text
            string clean = AnsiAllRegex.Replace(text, "").Replace("\r", "").Trim();
            return string.IsNullOrEmpty(clean);
            }

        private void ParseCleanLine(string lineWithColors)
            {
            string cleanLine = AnsiAllRegex.Replace(lineWithColors, "").Trim();
            if (!string.IsNullOrWhiteSpace(cleanLine))
                {
                try
                    {
                    _parser.ParseLine(cleanLine,
                        (status, msg, group) => _gridController.HandleLogMessage(status, msg),
                        (progMsg) => _statusCallback?.Invoke(progMsg)
                    );
                    }
                catch { /* Ignore parsing errors */ }
                }
            }

        private void CheckBufferForPauseLocked()
            {
            if (_lineBuffer.Length == 0) return;

            // Check remaining buffer for pause prompts
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
                // Preserve structural newlines, skip split artifact
                if (line == "" && lines[lines.Length - 1] == line) continue;

                string cleanContent = AnsiAllRegex.Replace(line, "").Trim();

                // Filters
                if (cleanContent.StartsWith("Running task", StringComparison.OrdinalIgnoreCase)) continue;
                if (cleanContent.Contains("]0;") && cleanContent.Contains("cmd.exe") && cleanContent.Length < 50) continue;

                string lineForUi = UiCleanerRegex.Replace(line, "");

                // Remove positioning codes
                lineForUi = Regex.Replace(lineForUi, @"\x1B\[\d+(;\d+)?[Hf]", "");

                // Convert cursor movement to spaces before stripping ANSI
                lineForUi = ReplaceCursorMovementWithSpaces(lineForUi);

                lineForUi = lineForUi.Replace("\x1B[K", "");
                lineForUi = WindowTitleRegex.Replace(lineForUi, "");

                string cleanedForCheck = AnsiAllRegex.Replace(lineForUi, "");

                // Skip purely ANSI lines
                if (string.IsNullOrWhiteSpace(cleanedForCheck) && !string.IsNullOrWhiteSpace(AnsiAllRegex.Replace(line, "")))
                    {
                    continue;
                    }

                sbUi.Append(lineForUi).Append('\n');
                }

            string textToAppend = sbUi.ToString();
            if (string.IsNullOrEmpty(textToAppend)) return;

            Log.Debug($"[UI Render] {SanitizeForLog(textToAppend)}");

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

                // Ignore large movements (likely screen positioning)
                if (count > 10)
                    {
                    return "";
                    }

                return new string(' ', count);
            });
            }

        private void LogRawChunk(string text)
            {
            if (string.IsNullOrEmpty(text)) return;
            // Log readable control characters
            string safeText = SanitizeForLog(text);
            Log.Debug($"{safeText}");
            }

        private string SanitizeForLog(string input)
            {
            if (string.IsNullOrEmpty(input)) return input;

            var sb = new StringBuilder(input.Length * 2);

            foreach (char c in input)
                {
                if (c < 32) // Control chars (ASCII 0-31)
                    {
                    switch (c)
                        {
                        case '\r': sb.Append("[CR]"); break;
                        case '\n': sb.Append("[LF]"); break;
                        case '\t': sb.Append("[TAB]"); break;
                        case '\x1b': sb.Append("[ESC]"); break;
                        case '\x07': sb.Append("[BEL]"); break;
                        case '\x08': sb.Append("[BS]"); break;
                        default: sb.Append($"[x{(int)c:X2}]"); break;
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
                string[] parts = AnsiSplitRegex.Split(text);

                Color currentForeColor = box.SelectionColor.Name == "0" ? box.ForeColor : box.SelectionColor;
                Color currentBackColor = box.SelectionBackColor.Name == "0" ? box.BackColor : box.SelectionBackColor;
                FontStyle currentStyle = box.SelectionFont?.Style ?? FontStyle.Regular;

                box.Suspend();

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

    public static class RichTextBoxExtensions
        {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, int wParam, ref Point lParam);
        private const int WM_USER = 0x400;

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