using System;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ATT_Wrapper.Components;
using Serilog;

namespace ATT_Wrapper.Services
    {
    public class ConsoleOutputHandler
        {
        private readonly ILogParser _parser;
        private readonly ResultsGridController _gridController;
        private readonly Action<string> _statusCallback;
        private readonly Action _enterCallback;

        // Цвета и шрифты
        private readonly Color _defaultForeColor = Color.Gainsboro;
        private readonly Color _defaultBackColor = Color.Black; // Предполагаем черный фон по умолчанию для консоли
        private readonly Font _defaultFont = new Font("Consolas", 10F, FontStyle.Regular);
        private readonly Font _boldFont = new Font("Consolas", 10F, FontStyle.Bold);
        private readonly Font _italicFont = new Font("Consolas", 10F, FontStyle.Italic);
        private readonly Font _underlineFont = new Font("Consolas", 10F, FontStyle.Underline);

        // Улучшенный Regex для ANSI CSI последовательностей (CSI = ESC [ ... )
        private const string AnsiRegex = @"\x1B\[[0-9;?]*[ -/]*[@-~]";

        public ConsoleOutputHandler(ILogParser parser, ResultsGridController gridController, Action<string> statusCallback, Action enterCallback)
            {
            _parser = parser;
            _gridController = gridController;
            _statusCallback = statusCallback;
            _enterCallback = enterCallback;
            }

        public void ProcessLine(string rawLine, RichTextBox rtbLog)
            {
            if (string.IsNullOrWhiteSpace(rawLine)) return;

            // 1. Дебаг сырых данных (опционально, можно отключить позже)
            LogRawString(rawLine);

            // 2. Создаем чистую версию строки (удаляем все ANSI коды)
            string plainLine = Regex.Replace(rawLine, AnsiRegex, "");

            // 3. Анализ содержимого
            bool isProgress = plainLine.TrimStart().StartsWith("Running task:", StringComparison.OrdinalIgnoreCase);
            bool isInfo = plainLine.Contains("INFO");

            // 4. Логика ввода (Pause)
            if (plainLine.Contains("Press any key"))
                {
                _statusCallback?.Invoke("Finalizing...");
                _enterCallback?.Invoke();
                return;
                }

            // 5. Вывод в Expert View (RichTextBox)
            if (!isInfo && !isProgress)
                {
                // Передаем сырую строку, чтобы распарсить цвета и стили
                AppendTextToRichTextBox(rtbLog, rawLine + Environment.NewLine);
                }

            // 6. Парсинг в таблицу (Simple View)
            _parser.ParseLine(plainLine,
                (status, msg) => _gridController.HandleLogMessage(status, msg),
                (progMsg) => _statusCallback?.Invoke(progMsg)
            );
            }

        private void LogRawString(string line)
            {
            StringBuilder sb = new StringBuilder();
            foreach (char c in line)
                {
                if (c == 0x1B) sb.Append("[ESC]");
                else if (char.IsControl(c)) sb.Append($"[x{(int)c:X2}]");
                else sb.Append(c);
                }
            Log.Debug($"[RAW HEX] {sb}");
            }

        private void AppendTextToRichTextBox(RichTextBox box, string text)
            {
            // Разбиваем строку на сегменты: [Текст] [Код] [Текст] ...
            // Группировка () в Regex.Split сохраняет разделители в массиве
            string[] parts = Regex.Split(text, $"({AnsiRegex})");

            foreach (string part in parts)
                {
                if (string.IsNullOrEmpty(part)) continue;

                if (part.StartsWith("\x1B["))
                    {
                    // Это ANSI код -> Применяем стиль
                    ApplyAnsiCode(box, part);
                    }
                else
                    {
                    // Это текст -> Проверяем на ключевые слова
                    Color keywordColor = GetKeywordColor(part);

                    // Если найдено ключевое слово (PASS/FAIL), принудительно меняем цвет,
                    // игнорируя текущий контекст. Это решает проблему "белых" PASS.
                    if (keywordColor != _defaultForeColor)
                        {
                        box.SelectionColor = keywordColor;
                        }

                    // Устанавливаем курсор в конец и пишем
                    box.SelectionStart = box.TextLength;
                    box.SelectionLength = 0;
                    box.AppendText(part);
                    }
                }
            }

        private void ApplyAnsiCode(RichTextBox box, string ansiSeq)
            {
            try
                {
                // Извлекаем содержимое кода (числа между [ и m)
                var match = Regex.Match(ansiSeq, @"\[([0-9;]+)m");
                if (match.Success)
                    {
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
                                box.SelectionColor = _defaultForeColor;
                                box.SelectionBackColor = _defaultBackColor;
                                box.SelectionFont = _defaultFont;
                                }
                            else if (code == 1) // Bold
                                {
                                box.SelectionFont = new Font(box.SelectionFont ?? _defaultFont, FontStyle.Bold);
                                }
                            else if (code == 3) // Italic
                                {
                                box.SelectionFont = new Font(box.SelectionFont ?? _defaultFont, FontStyle.Italic);
                                }
                            else if (code == 4) // Underline
                                {
                                box.SelectionFont = new Font(box.SelectionFont ?? _defaultFont, FontStyle.Underline);
                                }
                            else if (code == 22) // Normal intensity (not bold, not faint)
                                {
                                box.SelectionFont = new Font(box.SelectionFont ?? _defaultFont, FontStyle.Regular);
                                }
                            else if (code == 23) // Not italic
                                {
                                box.SelectionFont = new Font(box.SelectionFont ?? _defaultFont, FontStyle.Regular);
                                }
                            else if (code == 24) // Not underline
                                {
                                box.SelectionFont = new Font(box.SelectionFont ?? _defaultFont, FontStyle.Regular);
                                }
                            else if (code >= 30 && code <= 37) // Foreground color (standard)
                                {
                                box.SelectionColor = GetAnsiColor(code - 30, false);
                                }
                            else if (code >= 40 && code <= 47) // Background color (standard)
                                {
                                box.SelectionBackColor = GetAnsiColor(code - 40, false);
                                }
                            else if (code >= 90 && code <= 97) // Foreground color (bright)
                                {
                                box.SelectionColor = GetAnsiColor(code - 90, true);
                                }
                            else if (code >= 100 && code <= 107) // Background color (bright)
                                {
                                box.SelectionBackColor = GetAnsiColor(code - 100, true);
                                }
                            else if (code == 38 || code == 48) // Extended color (256 or RGB)
                                {
                                // Для 38;5;n (foreground 256), 38;2;r;g;b (RGB)
                                // Аналогично для 48 background
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
                                            if (isFore) box.SelectionColor = color;
                                            else box.SelectionBackColor = color;
                                            }
                                        }
                                    else if (subcode == 2 && i + 3 < codes.Length) // RGB
                                        {
                                        i++;
                                        int r = int.Parse(codes[i]);
                                        i++;
                                        int g = int.Parse(codes[i]);
                                        i++;
                                        int b = int.Parse(codes[i]);
                                        Color color = Color.FromArgb(r, g, b);
                                        if (isFore) box.SelectionColor = color;
                                        else box.SelectionBackColor = color;
                                        }
                                    }
                                }
                            }
                        i++;
                        }
                    }
                }
            catch { /* Игнорируем некорректные коды */ }
            }

        private Color GetKeywordColor(string text)
            {
            string trimmed = text.TrimStart();
            // Строгая проверка начал строк
            if (trimmed.StartsWith("PASS")) return Color.LightGreen;
            if (trimmed.StartsWith("FAIL")) return Color.Salmon;
            if (trimmed.StartsWith("SKIPPED")) return Color.Gold;
            if (trimmed.Contains("ERROR")) return Color.Salmon;
            if (trimmed.Contains("WARNING")) return Color.Gold;

            // Если ничего не найдено - возвращаем дефолтный цвет (маркер "не менять")
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
                case 4: return bright ? Color.RoyalBlue : Color.Navy;
                case 5: return bright ? Color.Magenta : Color.Purple;
                case 6: return bright ? Color.Cyan : Color.Teal;
                case 7: return bright ? Color.White : Color.Silver;
                default: return _defaultForeColor;
                }
            }

        private Color GetAnsi256Color(int index)
            {
            // Простая реализация для 256 цветов (можно расширить таблицей)
            // Здесь базовая аппроксимация; для полной таблицы используйте lookup table
            if (index < 0 || index > 255) return _defaultForeColor;

            if (index < 16)
                {
                // Стандартные цвета (0-15)
                return GetAnsiColor(index % 8, index >= 8);
                }
            else if (index < 232)
                {
                // 216 цветов куба (16-231)
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
        }
    }