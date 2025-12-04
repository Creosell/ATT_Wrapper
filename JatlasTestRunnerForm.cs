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

        // Возвращаемся к стандартному экзекутору
        private readonly ProcessExecutor _executor;

        private readonly ResultsGridController _gridController;
        private readonly MappingManager _mapper;
        private ConsoleOutputHandler _outputHandler;

        public JatlasTestRunnerForm()
            {
            GeminiLogger.Initialize();
            InitializeComponent();
            SetupLogging();

            GeminiLogger.Log("Initializing Main Form (Standard Process Mode)...");

            _executor = new ProcessExecutor();

            string mappingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mappings.json");
            _mapper = new MappingManager(mappingPath);
            _gridController = new ResultsGridController(dgvResults, _mapper);

            // --- Подписки ---

            // Получаем сырые данные от процесса и передаем в Handler
            _executor.OnOutputReceived += HandleRawOutput;

            _executor.OnExited += HandleExit;
            // ----------------

            ThemeManager.Apply(this, dgvResults, rtbLog);
            GeminiLogger.Log("Initialization complete.");
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
            GeminiLogger.Log($"RunTest requested: {script} {args}");

            ToggleButtons(false);
            _gridController.Clear();
            rtbLog.Clear();
            rtbLog.ForeColor = Color.Gainsboro;

            if (statusLabel != null) statusLabel.Text = "Running...";

            // Создаем Handler для отрисовки
            _outputHandler = new ConsoleOutputHandler(
                parser,
                _gridController,
                (status) => this.BeginInvoke((Action)( () => { if (statusLabel != null) statusLabel.Text = status; } )),
                () => _executor.SendInput("") // Для "Press any key"
            );

            try
                {
                string fullPath = Path.Combine(SCRIPT_PATH, script);
                _executor.Start(fullPath, args);
                }
            catch (Exception ex)
                {
                GeminiLogger.Error(ex, "Start failed");
                MessageBox.Show($"Error: {ex.Message}");
                ToggleButtons(true);
                }
            }

        private void HandleRawOutput(string rawData)
            {
            if (this.IsDisposed || !this.IsHandleCreated) return;

            // Логируем, что реально пришло
            // GeminiLogger.LogRawData("Executor -> Form", rawData);

            this.BeginInvoke((Action)( () =>
            {
                // Передаем в OutputHandler (он сам рисует цвета и собирает строки)
                _outputHandler?.ProcessRawData(rawData, rtbLog);
            } ));
            }

        private void HandleExit()
            {
            GeminiLogger.Log("HandleExit called");
            this.BeginInvoke((Action)( () => {
                if (statusLabel != null) statusLabel.Text = "Ready";
                ToggleButtons(true);
            } ));
            }

        // --- Кнопки ---
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
            GeminiLogger.Close();
            Log.CloseAndFlush();
            base.OnFormClosing(e);
            }
        }
    }