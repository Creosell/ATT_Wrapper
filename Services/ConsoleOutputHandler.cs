using System;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ATT_Wrapper.Components;

namespace ATT_Wrapper.Services
    {
    public class ConsoleOutputHandler
        {
        private readonly ILogParser _parser;
        private readonly ResultsGridController _gridController;
        private readonly Action<string> _statusCallback;
        private readonly Action _enterCallback;

        // Дефолтные стили
        private readonly Color _defaultColor = Color.Gainsboro;
        private readonly Font _defaultFont = new Font("Consolas", 10F, FontStyle.Regular);
        private readonly Font _boldFont = new Font("Consolas", 10F, FontStyle.Bold);

        // Regex для поиска ANSI escape-последовательностей
        private const string AnsiRegex = @"\x1B\[[0-9;?]*[ -/]*[@-~]";

        // Буфер для сборки полных строк (для парсера логики)
        private StringBuilder _lineBuffer = new StringBuilder();

        public ConsoleOutputHandler(ILogParser parser, ResultsGridController gridController, Action<string> statusCallback, Action enterCallback)
            {
            _parser = parser;
            _gridController = gridController;
            _statusCallback = statusCallback;
            _enterCallback = enterCallback;
            }

        // Входная точка для сырых данных из ConPty
        public void ProcessRawData(string rawData, RichTextBox rtbLog)
            {
            if (string.IsNullOrEmpty(rawData)) return;

            // [LOG] Логируем входящий чанк данных
            GeminiLogger.LogRawData("Handler Input Chunk", rawData);

            // 1. СРАЗУ рисуем то, что пришло (Native Look & Feel)
            // Это позволяет видеть прогресс-бары и цвета в реальном времени
            AppendTextToRichTextBox(rtbLog, rawData);

            // 2. Накапливаем данные для логического парсера (ему нужны полные строки)
            _lineBuffer.Append(rawData);
            ProcessBufferedLines();
            }

        private void ProcessBufferedLines()
            {
            string content = _lineBuffer.ToString();
            int newlineIndex;

            // Ищем полные строки (заканчивающиеся на \n)
            while (( newlineIndex = content.IndexOf('\n') ) >= 0)
                {
                string line = content.Substring(0, newlineIndex).TrimEnd('\r'); // Достаем строку без \r

                // [LOG] Логируем факт выделения полной строки для парсера
                // GeminiLogger.Debug($"Extracted full line for logic: '{line.Trim()}'");

                ProcessLogicLine(line);

                // Удаляем обработанную часть из контента
                content = content.Substring(newlineIndex + 1);
                }

            // Обновляем буфер остатком
            _lineBuffer.Clear();
            _lineBuffer.Append(content);
            }

        private void ProcessLogicLine(string line)
            {
            // Очищаем от ANSI кодов для анализа текста
            string plainLine = Regex.Replace(line, AnsiRegex, "");

            // Ловим "Press any key"
            if (plainLine.Contains("Press any key"))
                {
                GeminiLogger.Log("Detected 'Press any key' -> Triggering callbacks");
                _statusCallback?.Invoke("Finalizing...");
                _enterCallback?.Invoke();
                return;
                }

            // Отправляем в парсер (обновление таблицы)
            // Парсеру не обязательно знать про цвета, он смотрит суть
            _parser.ParseLine(plainLine,
                (status, msg) =>
                {
                    GeminiLogger.Log($"Parser Result -> Status: {status}, Msg: {msg}");
                    _gridController.HandleLogMessage(status, msg);
                },
                (progMsg) => _statusCallback?.Invoke(progMsg)
            );
            }

        private void AppendTextToRichTextBox(RichTextBox box, string text)
            {
            // Разбиваем текст на куски: [Текст] [ANSI-код] [Текст]
            string[] parts = Regex.Split(text, $"({AnsiRegex})");

            foreach (string part in parts)
                {
                if (string.IsNullOrEmpty(part)) continue;

                if (part.StartsWith("\x1B["))
                    {
                    // Это код цвета/стиля -> Меняем настройки кисти
                    ApplyAnsiCode(box, part);
                    }
                else
                    {
                    // Это просто текст -> Пишем его ТЕКУЩИМ цветом
                    // Мы больше не вмешиваемся в цвета здесь (никакого GetKeywordColor)

                    // [LOG] Логируем, какой текст пишем и текущий цвет
                    // GeminiLogger.Debug($"Draw Text: '{part.Trim()}' Color: {box.SelectionColor.Name}");

                    box.SelectionStart = box.TextLength;
                    box.SelectionLength = 0;
                    box.AppendText(part);

                    // Автоскролл
                    box.ScrollToCaret();
                    }
                }
            }

        private void ApplyAnsiCode(RichTextBox box, string ansiSeq)
            {
            try
                {
                var match = Regex.Match(ansiSeq, @"\[([0-9;]+)m");
                if (match.Success)
                    {
                    string[] codes = match.Groups[1].Value.Split(';');

                    // Настройки применяются к *будущему* тексту (Selection)
                    box.SelectionStart = box.TextLength;
                    box.SelectionLength = 0;

                    foreach (string codeStr in codes)
                        {
                        if (int.TryParse(codeStr, out int code))
                            {
                            if (code == 0) // Reset
                                {
                                GeminiLogger.Debug("ANSI [0] -> Reset to Default");
                                box.SelectionColor = _defaultColor;
                                box.SelectionFont = _defaultFont;
                                }
                            else if (code == 1) // Bold
                                {
                                GeminiLogger.Debug("ANSI [1] -> Bold");
                                box.SelectionFont = _boldFont;
                                // Часто Bold также делает текст белым/ярким в терминалах
                                if (box.SelectionColor == _defaultColor) box.SelectionColor = Color.White;
                                }
                            else // Colors
                                {
                                Color? c = GetAnsiColor(code);
                                if (c.HasValue)
                                    {
                                    GeminiLogger.Debug($"ANSI [{code}] -> Color {c.Value.Name}");
                                    box.SelectionColor = c.Value;
                                    }
                                else
                                    {
                                    GeminiLogger.Debug($"ANSI [{code}] -> Unknown/Unsupported");
                                    }
                                }
                            }
                        }
                    }
                }
            catch (Exception ex)
                {
                GeminiLogger.Error(ex, $"Error applying ANSI code: {ansiSeq}");
                }
            }

        private Color? GetAnsiColor(int code)
            {
            switch (code)
                {
                case 30: return Color.Gray;
                case 31: return Color.Salmon; // Red
                case 32: return Color.LightGreen; // Green
                case 33: return Color.Gold; // Yellow
                case 34: return Color.CornflowerBlue; // Blue
                case 35: return Color.Violet; // Magenta
                case 36: return Color.Cyan; // Cyan
                case 37: return Color.White; // White
                // Bright versions (90-97)
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