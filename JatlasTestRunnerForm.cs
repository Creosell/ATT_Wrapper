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

        public JatlasTestRunnerForm()
            {
            InitializeComponent();
            SetupLogging();

            // Инициализация сервисов
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

        // --- HANDLERS ---

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
            // 0. Защита от вызова на закрытой форме
            if (this.IsDisposed || !this.IsHandleCreated) return;

            if (string.IsNullOrWhiteSpace(line)) return;

            // 1. Возвращаем логи в Output Visual Studio (с фильтром спама)
            // Не пишем в консоль строки прогресса, чтобы не засорять её
            bool isProgress = line.TrimStart().StartsWith("Running task:", StringComparison.OrdinalIgnoreCase);
            if (!isProgress)
                {
                Debug.WriteLine($"[RAW] {line}");
                }

            // 2. Логика ввода (Pause)
            if (line.Contains("Press any key"))
                {
                // Используем BeginInvoke, чтобы не блокировать поток чтения
                this.BeginInvoke((Action)( () => {
                    if (statusLabel != null) statusLabel.Text = "Finalizing...";
                } ));
                _executor.SendInput("");
                return;
                }

            // 3. Очистка ANSI для логики
            string plainLine = Regex.Replace(line, @"\x1B\[[^@-~]*[@-~]", "");

            // 4. Expert View (Thread-safe update)
            this.BeginInvoke((Action)( () =>
            {
                // Фильтрация для GUI (Expert View)
                if (!plainLine.Contains("INFO") && !isProgress)
                    {
                    AppendAnsiText(rtbLog, line + Environment.NewLine);
                    }
            } ));

            // 5. Simple View (Parser -> GridController)
            _currentParser?.ParseLine(plainLine,
                // Result Callback
                (status, msg) => this.BeginInvoke((Action)( () => _gridController.HandleLogMessage(status, msg) )),
                // Progress Callback
                (progMsg) => this.BeginInvoke((Action)( () => { if (statusLabel != null) statusLabel.Text = progMsg; } ))
            );
            }

        private void AppendAnsiText(RichTextBox box, string text)
            {
            // Упрощенный парсер цветов для RichTextBox
            int cursor = 0;
            var matches = Regex.Matches(text, @"\x1B\[(\d+)(;\d+)*m");
            if (matches.Count == 0) { box.AppendText(text); return; }

            foreach (Match match in matches)
                {
                box.AppendText(text.Substring(cursor, match.Index - cursor));
                string code = match.Groups[1].Value;
                if (int.TryParse(code, out int c)) box.SelectionColor = GetColor(c);
                box.SelectionStart = box.TextLength;
                box.SelectionLength = 0;
                cursor = match.Index + match.Length;
                }
            box.AppendText(text.Substring(cursor));
            }

        private Color GetColor(int code)
            {
            switch (code)
                {
                case 31: return Color.Salmon;
                case 32: return Color.LightGreen;
                case 33: return Color.Gold;
                case 34: return Color.CornflowerBlue;
                default: return Color.Gainsboro;
                }
            }

        private void HandleExit()
            {
            this.Invoke((Action)( () => {
                if (statusLabel != null) statusLabel.Text = "Ready";
                ToggleButtons(true);
            } ));
            }

        // --- BUTTONS ---
        private void btnUpdate_Click(object sender, EventArgs e) => RunTest(new JatlasUpdateParser(), "update.bat", "");
        private void btnCommon_Click(object sender, EventArgs e) => RunTest(new JatlasTestParser((idx, msg) => this.Invoke((Action)( () => _gridController.UpdateLastRow(msg) ))), "run-jatlas-auto.bat", "-l common --stage dev");
        private void btnSpecial_Click(object sender, EventArgs e) => RunTest(new JatlasTestParser((idx, msg) => this.Invoke((Action)( () => _gridController.UpdateLastRow(msg) ))), "run-jatlas-auto.bat", "-l special --stage dev");
        private void btnAging_Click(object sender, EventArgs e) => RunTest(new JatlasAgingParser(), "run-jatlas-auto.bat", "-l aging --stage dev");
        private void btnCommonOffline_Click(object sender, EventArgs e) => RunTest(new JatlasTestParser((idx, msg) => this.Invoke((Action)( () => _gridController.UpdateLastRow(msg) ))), "run-jatlas-auto.bat", "-l common --offline");
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