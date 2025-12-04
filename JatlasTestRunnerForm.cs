using ATT_Wrapper.Components;
using ATT_Wrapper.Services;
using Serilog;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ATT_Wrapper
    {
    public partial class JatlasTestRunnerForm : Form
        {
        private const string SCRIPT_PATH = @"C:\jatlas\scripts\win_scripts\";

        private readonly ProcessExecutor _executor;
        private readonly ResultsGridController _gridController;
        private readonly MappingManager _mapper;
        private ILogParser _currentParser;

        // Цвет по умолчанию для Expert Log
        private readonly Color _defaultColor = Color.Gainsboro;

        public JatlasTestRunnerForm()
            {
            InitializeComponent();
            SetupLogging();

            _executor = new ProcessExecutor();
            string mappingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mappings.json");
            _mapper = new MappingManager(mappingPath);
            _gridController = new ResultsGridController(dgvResults, _mapper);

            _executor.OnOutputReceived += HandleOutput;
            _executor.OnExited += HandleExit;

            ThemeManager.Apply(this, dgvResults, rtbLog);
            }

        private void SetupLogging()
            {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File("logs/jatlas_runner_.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();
            Log.Information("=== App Started ===");
            }

        private void RunTest(ILogParser parser, string script, string args)
            {
            ToggleButtons(false);
            _gridController.Clear();
            rtbLog.Clear();
            if (statusLabel != null) statusLabel.Text = "Initializing...";
            _currentParser = parser;

            try
                {
                string fullPath = Path.Combine(SCRIPT_PATH, script);
                _executor.Start(fullPath, args);
                }
            catch (Exception ex)
                {
                Log.Error(ex, "Start failed");
                MessageBox.Show($"Error: {ex.Message}");
                ToggleButtons(true);
                }
            }

        private void HandleOutput(string line)
            {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            if (string.IsNullOrWhiteSpace(line)) return;

            // 1. Создаем чистую версию для логики (удаляем ANSI)
            string plainLine = Regex.Replace(line, @"\x1B\[[^@-~]*[@-~]", "");

            // 2. Определяем, является ли строка прогресс-баром
            bool isProgress = plainLine.TrimStart().StartsWith("Running task:", StringComparison.OrdinalIgnoreCase);

            // 3. Пишем в Debug (без спама)
            if (!isProgress)
                {
                Debug.WriteLine($"[RAW] {line}");
                }

            // 4. Input handling
            if (line.Contains("Press any key"))
                {
                this.BeginInvoke((Action)( () => {
                    if (statusLabel != null) statusLabel.Text = "Finalizing...";
                } ));
                _executor.SendInput("");
                return;
                }

            // 5. Обновляем GUI
            this.BeginInvoke((Action)( () =>
            {
                // EXPERT VIEW: Фильтруем спам
                if (!plainLine.Contains("INFO") && !isProgress)
                    {
                    // Используем метод, который умеет и парсить ANSI, и красить по ключевым словам
                    AppendStyledText(rtbLog, line + Environment.NewLine);
                    }

                // SIMPLE VIEW: Парсим
                _currentParser?.ParseLine(plainLine,
                    (status, msg) => _gridController.HandleLogMessage(status, msg),
                    (progMsg) => { if (statusLabel != null) statusLabel.Text = progMsg; }
                );
            } ));
            }

        // --- ГЛАВНЫЙ МЕТОД РАСКРАСКИ ---
        private void AppendStyledText(RichTextBox box, string text)
            {
            // Regex для разделения текста по ANSI-кодам
            // Захватываем разделитель (код), чтобы он тоже попал в массив
            string[] parts = Regex.Split(text, @"(\x1B\[[0-9;]*m)");

            // Текущий цвет (начинаем с дефолтного)
            Color currentColor = _defaultColor;

            foreach (string part in parts)
                {
                // Если это ANSI-код
                if (part.StartsWith("\x1B["))
                    {
                    // Парсим код (убираем \x1B[ и m)
                    string codeSeq = part.Substring(2, part.Length - 3);
                    string[] codes = codeSeq.Split(';');

                    foreach (var c in codes)
                        {
                        if (int.TryParse(c, out int codeNum))
                            {
                            var newColor = GetColorFromAnsi(codeNum);
                            if (newColor.HasValue) currentColor = newColor.Value;
                            }
                        }
                    }
                // Если это текст
                else if (!string.IsNullOrEmpty(part))
                    {
                    // Устанавливаем цвет перед добавлением
                    box.SelectionStart = box.TextLength;
                    box.SelectionLength = 0;

                    // ХИТРОСТЬ: Если ANSI не поменял цвет (он дефолтный),
                    // пробуем раскрасить по ключевым словам (PASS/FAIL)
                    if (currentColor == _defaultColor)
                        {
                        Color keywordColor = GetColorFromKeywords(part);
                        box.SelectionColor = keywordColor;
                        }
                    else
                        {
                        box.SelectionColor = currentColor;
                        }

                    box.AppendText(part);
                    }
                }
            }

        // Цвета по ANSI кодам
        private Color? GetColorFromAnsi(int code)
            {
            switch (code)
                {
                case 30: return Color.Gray;
                case 31: return Color.Salmon;        // Red
                case 32: return Color.LightGreen;    // Green
                case 33: return Color.Gold;          // Yellow
                case 34: return Color.CornflowerBlue;// Blue
                case 35: return Color.Violet;        // Magenta
                case 36: return Color.Cyan;          // Cyan
                case 37: return Color.White;
                case 90: return Color.DimGray;
                case 91: return Color.Red;
                case 92: return Color.Lime;
                case 93: return Color.Yellow;
                case 0: return _defaultColor;       // Reset
                default: return null;                // Не цвет (bold и т.д.)
                }
            }

        // Цвета по ключевым словам (Fallback)
        private Color GetColorFromKeywords(string text)
            {
            string trimmed = text.TrimStart();
            if (trimmed.StartsWith("PASS")) return Color.LightGreen;
            if (trimmed.StartsWith("FAIL")) return Color.Salmon;
            if (trimmed.StartsWith("SKIPPED")) return Color.Gold;
            if (trimmed.Contains("ERROR")) return Color.Salmon;
            if (trimmed.Contains("WARNING")) return Color.Gold;
            return _defaultColor;
            }

        private void HandleExit()
            {
            this.BeginInvoke((Action)( () => {
                if (statusLabel != null) statusLabel.Text = "Ready";
                ToggleButtons(true);
            } ));
            }

        // --- BUTTONS & HELPERS ---

        private void btnUpdate_Click(object sender, EventArgs e) => RunTest(new JatlasUpdateParser(), "update.bat", "");

        private void btnCommon_Click(object sender, EventArgs e)
            => RunTest(new JatlasTestParser((idx, msg) => _gridController.UpdateLastRow(msg)), "run-jatlas-auto.bat", "-l common --stage dev");

        private void btnSpecial_Click(object sender, EventArgs e)
            => RunTest(new JatlasTestParser((idx, msg) => _gridController.UpdateLastRow(msg)), "run-jatlas-auto.bat", "-l special --stage dev");

        private void btnAging_Click(object sender, EventArgs e)
            => RunTest(new JatlasAgingParser(), "run-jatlas-auto.bat", "-l aging --stage dev");

        private void btnCommonOffline_Click(object sender, EventArgs e)
            => RunTest(new JatlasTestParser((idx, msg) => _gridController.UpdateLastRow(msg)), "run-jatlas-auto.bat", "-l common --offline");

        private void taskKillBtn_Click(object sender, EventArgs e) => _executor.Kill();

        private void ToggleButtons(bool enabled)
            {
            foreach (Control c in this.Controls) EnableRecursive(c, enabled);
            if (taskKillBtn != null) taskKillBtn.Enabled = true;
            }

        private void EnableRecursive(Control c, bool enabled)
            {
            if (c is Button && !c.Name.Contains("Kill")) c.Enabled = enabled;
            if (c.HasChildren) foreach (Control child in c.Controls) EnableRecursive(child, enabled);
            }

        protected override void OnFormClosing(FormClosingEventArgs e)
            {
            _executor.Kill();
            Log.CloseAndFlush();
            base.OnFormClosing(e);
            }
        }
    }