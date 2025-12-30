using ATT_Wrapper.Interfaces;
using Serilog;
using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ATT_Wrapper.UI.Helpers
    {
    public class RichTextBoxConsoleRenderer : IConsoleRenderer
        {
        private readonly RichTextBox _box;
        // Эта регулярка нужна только для отрисовки (разделение на цветные блоки)
        private static readonly Regex AnsiSplitRegex = new Regex(@"(\x1B\[[0-9;?]*[ -/]*[@-~])", RegexOptions.Compiled);

        public RichTextBoxConsoleRenderer(RichTextBox box)
            {
            _box = box;
            }

        public void AppendText(string text)
            {
            if (string.IsNullOrEmpty(text)) return;

            if (_box.InvokeRequired)
                {
                _box.BeginInvoke(new Action(() => AppendTextInternal(text)));
                }
            else
                {
                AppendTextInternal(text);
                }
            }

        private void AppendTextInternal(string text)
            {
            try
                {
                string[] parts = AnsiSplitRegex.Split(text);
                Color currentForeColor = _box.SelectionColor.Name == "0" ? _box.ForeColor : _box.SelectionColor;
                Color currentBackColor = _box.SelectionBackColor.Name == "0" ? _box.BackColor : _box.SelectionBackColor;
                FontStyle currentStyle = _box.SelectionFont?.Style ?? FontStyle.Regular;

                _box.SuspendLayout();
                foreach (string part in parts)
                    {
                    if (string.IsNullOrEmpty(part)) continue;
                    if (part.StartsWith("\x1B["))
                        {
                        ApplyAnsiCode(part, ref currentForeColor, ref currentBackColor, ref currentStyle);
                        }
                    else
                        {
                        _box.SelectionColor = currentForeColor;
                        _box.SelectionBackColor = currentBackColor;
                        using (var currentFont = _box.SelectionFont)
                            {
                            var family = _box.Font.FontFamily; // Безопасное получение шрифта
                            float size = _box.Font.Size;
                            if (currentFont != null) { family = currentFont.FontFamily; size = currentFont.Size; }

                            _box.SelectionFont = new Font(family, size, currentStyle);
                            }
                        _box.AppendText(part);
                        }
                    }
                _box.ResumeLayout();
                _box.ScrollToCaret();
                }
            catch (Exception ex)
                {
                Log.Error(ex, "Error appending to UI.");
                }
            }

        private void ApplyAnsiCode(string ansiSeq, ref Color fg, ref Color bg, ref FontStyle style)
            {
            var match = Regex.Match(ansiSeq, @"\[([0-9;]*)([a-zA-Z])");
            if (!match.Success || match.Groups[2].Value != "m") return;
            string paramString = match.Groups[1].Value;
            string[] codes = string.IsNullOrEmpty(paramString) ? new[] { "0" } : paramString.Split(';');

            foreach (var codeStr in codes)
                {
                if (int.TryParse(codeStr, out int code))
                    {
                    if (code == 0) { fg = _box.ForeColor; bg = _box.BackColor; style = FontStyle.Regular; }
                    else if (code == 1) style |= FontStyle.Bold;
                    else if (code == 3) style |= FontStyle.Italic;
                    else if (code == 4) style |= FontStyle.Underline;
                    else if (code == 22) style &= ~FontStyle.Bold;
                    else if (code >= 30 && code <= 37) fg = GetAnsiColor(code - 30, false);
                    else if (code == 39) fg = _box.ForeColor;
                    else if (code >= 40 && code <= 47) bg = GetAnsiColor(code - 40, false);
                    else if (code == 49) bg = _box.BackColor;
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
        }
    }