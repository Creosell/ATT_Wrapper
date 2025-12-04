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
        private readonly Color _defaultColor = Color.Gainsboro;
        private readonly Font _defaultFont = new Font("Consolas", 10F, FontStyle.Regular);
        private readonly Font _boldFont = new Font("Consolas", 10F, FontStyle.Bold);

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
            // LogRawString(rawLine);

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
                // Передаем сырую строку, чтобы распарсить цвета
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
                    if (keywordColor != _defaultColor)
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

                    foreach (string codeStr in codes)
                        {
                        if (int.TryParse(codeStr, out int code))
                            {
                            if (code == 0) // Reset
                                {
                                box.SelectionColor = _defaultColor;
                                box.SelectionFont = _defaultFont;
                                }
                            else if (code == 1) // Bold
                                {
                                box.SelectionFont = _boldFont;
                                // Часто Bold также означает ярко-белый цвет
                                if (box.SelectionColor == _defaultColor) box.SelectionColor = Color.White;
                                }
                            else // Colors
                                {
                                Color? c = GetAnsiColor(code);
                                if (c.HasValue) box.SelectionColor = c.Value;
                                }
                            }
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
            return _defaultColor;
            }

        private Color? GetAnsiColor(int code)
            {
            switch (code)
                {
                case 30: return Color.Gray;
                case 31: return Color.Salmon;
                case 32: return Color.LightGreen;
                case 33: return Color.Gold;
                case 34: return Color.CornflowerBlue;
                case 35: return Color.Violet;
                case 36: return Color.Cyan;
                case 37: return Color.White;
                case 90: return Color.DimGray;
                case 91: return Color.Red;
                case 92: return Color.Lime;
                case 93: return Color.Yellow;
                case 94: return Color.RoyalBlue;
                case 95: return Color.Magenta;
                case 96: return Color.Cyan;
                case 97: return Color.White;
                default: return null;
                }
            }
        }
    }