using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ATT_Wrapper.Components;
using ATT_Wrapper.Services;
using Serilog;

namespace ATT_Wrapper
    {
    public partial class JatlasTestRunnerForm : Form
        {
        private const string SCRIPT_PATH = @"C:\jatlas\scripts\win_scripts\";

        private readonly ProcessExecutor _executor;
        private readonly ResultsGridController _gridController;
        private readonly MappingManager _mapper;

        // Обработчик вывода (логика парсинга цветов и текста переехала сюда)
        private ConsoleOutputHandler _outputHandler;

        // Тот же regex для очистки debug вывода
        private const string AnsiRegex = @"\x1B\[[0-9;?]*[ -/]*[@-~]";

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

        // --- RUN LOGIC ---

        private void RunTest(ILogParser parser, string script, string args)
            {
            ToggleButtons(false);
            _gridController.Clear();
            rtbLog.Clear();
            rtbLog.ForeColor = Color.Gainsboro;

            if (statusLabel != null) statusLabel.Text = "Initializing...";

            // Создаем хендлер для текущего запуска
            _outputHandler = new ConsoleOutputHandler(
                parser,
                _gridController,
                (status) => this.BeginInvoke((Action)( () => { if (statusLabel != null) statusLabel.Text = status; } )),
                () => _executor.SendInput("")
            );

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

            // 1. Вывод в Debug (Output окно VS) - чистый текст без шума
            string plainLine = Regex.Replace(line, AnsiRegex, "");
            bool isProgress = plainLine.TrimStart().StartsWith("Running task:", StringComparison.OrdinalIgnoreCase);
            bool isInfo = plainLine.Contains("INFO");

            if (!isProgress && !isInfo && !string.IsNullOrWhiteSpace(plainLine))
                {
                Debug.WriteLine($"[RAW] {plainLine}");
                }

            // 2. Обработка логики (GUI, Parsing)
            this.BeginInvoke((Action)( () =>
            {
                _outputHandler?.ProcessLine(line, rtbLog);
            } ));
            }

        private void HandleExit()
            {
            this.BeginInvoke((Action)( () => {
                if (statusLabel != null) statusLabel.Text = "Ready";
                ToggleButtons(true);
            } ));
            }

        // --- BUTTONS ---
        private void btnUpdate_Click(object sender, EventArgs e) => RunTest(new JatlasUpdateParser(), "update.bat", "");
        private void btnCommon_Click(object sender, EventArgs e) => RunTest(new JatlasTestParser((idx, msg) => _gridController.UpdateLastRow(msg)), "run-jatlas-auto.bat", "-l common --stage dev");
        private void btnSpecial_Click(object sender, EventArgs e) => RunTest(new JatlasTestParser((idx, msg) => _gridController.UpdateLastRow(msg)), "run-jatlas-auto.bat", "-l special --stage dev");
        private void btnAging_Click(object sender, EventArgs e) => RunTest(new JatlasAgingParser(), "run-jatlas-auto.bat", "-l aging --stage dev");
        private void btnCommonOffline_Click(object sender, EventArgs e) => RunTest(new JatlasTestParser((idx, msg) => _gridController.UpdateLastRow(msg)), "run-jatlas-auto.bat", "-l common --offline");
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