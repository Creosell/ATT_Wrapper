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

        // Output handler (parsing logic for colors and text)
        private ConsoleOutputHandler _outputHandler;

        // Regex for cleaning debug output
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

            Log.Information("Event handlers subscribed");

            ThemeManager.Apply(this, dgvResults, rtbLog);
            }

        private void SetupLogging()
            {
            string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(logDirectory))
                {
                Directory.CreateDirectory(logDirectory);
                }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Debug()    // Write to Debug output (System.Diagnostics.Debug)
                .WriteTo.File(
                    Path.Combine(logDirectory, "jatlas_runner_.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();

            Log.Information("=== App Started ===");
            Log.Information($"Log directory: {logDirectory}");
            }

        // --- RUN LOGIC ---

        private void RunTest(ILogParser parser, string script, string args)
            {
            ToggleButtons(false);
            _gridController.Clear();
            rtbLog.Clear();
            rtbLog.ForeColor = Color.Gainsboro;

            if (statusLabel != null) statusLabel.Text = "Initializing...";

            // Dispose previous output handler if exists
            _outputHandler?.Dispose();

            // Create log file path for console output
            string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            string consoleLogPath = Path.Combine(
                logDirectory,
                $"console_{DateTime.Now:yyyyMMdd_HHmmss}.log"
            );

            Log.Information($"Console output will be logged to: {consoleLogPath}");

            // Create handler for current run with file logging enabled
            _outputHandler = new ConsoleOutputHandler(
                parser,
                _gridController,
                (status) => this.BeginInvoke((Action)( () =>
                {
                    if (statusLabel != null)
                        statusLabel.Text = status;
                } )),
                () => _executor.SendInput(""),
                consoleLogPath  // Enable file logging
            );

            try
                {
                string fullPath = Path.Combine(SCRIPT_PATH, script);
                Log.Information($"Starting test: {fullPath} {args}");
                String cmdFullPath = "C:\\WINDOWS\\system32\\cmd.exe";
                _executor.Start(cmdFullPath, "/c echo HELLO_FROM_PIPE & echo TEST_COLOR");
                //_executor.Start(fullPath, args);
                Log.Information("Process started successfully");
                }
            catch (Exception ex)
                {
                Log.Error(ex, "Start failed");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ToggleButtons(true);
                }
            }

        private void HandleOutput(string line)
            {
            if (this.IsDisposed || !this.IsHandleCreated) return;

            try
                {
                // Log raw output to verify we're receiving data
                Log.Debug($"[HandleOutput] Received line: {line.Substring(0, Math.Min(100, line.Length))}...");

                // 1. Output to Debug (VS Output window) - clean text without noise
                string plainLine = Regex.Replace(line, AnsiRegex, "");
                bool isProgress = plainLine.TrimStart().StartsWith("Running task:", StringComparison.OrdinalIgnoreCase);
                bool isInfo = plainLine.Contains("INFO");

                if (!isProgress && !isInfo && !string.IsNullOrWhiteSpace(plainLine))
                    {
                    Debug.WriteLine($"[CONSOLE] {plainLine}");
                    }

                // 2. Process logic (GUI, Parsing)
                if (_outputHandler != null)
                    {
                    this.BeginInvoke((Action)( () =>
                    {
                        try
                            {
                            _outputHandler.ProcessLine(line, rtbLog);
                            }
                        catch (Exception ex)
                            {
                            Log.Error(ex, "Error processing line in output handler");
                            }
                    } ));
                    }
                else
                    {
                    Log.Warning("OutputHandler is null when trying to process line");
                    }
                }
            catch (Exception ex)
                {
                Log.Error(ex, "Error in HandleOutput");
                }
            }

        private void HandleExit()
            {
            Log.Information("Process exited");

            this.BeginInvoke((Action)( () =>
            {
                if (statusLabel != null) statusLabel.Text = "Ready";
                ToggleButtons(true);
            } ));
            }

        // --- BUTTONS ---
        private void btnUpdate_Click(object sender, EventArgs e) =>
            RunTest(new JatlasUpdateParser(), "update.bat", "");

        private void btnCommon_Click(object sender, EventArgs e) =>
            RunTest(new JatlasTestParser((idx, msg) => _gridController.UpdateLastRow(msg)),
                    "run-jatlas-auto.bat", "-l common --stage dev");

        private void btnSpecial_Click(object sender, EventArgs e) =>
            RunTest(new JatlasTestParser((idx, msg) => _gridController.UpdateLastRow(msg)),
                    "run-jatlas-auto.bat", "-l special --stage dev");

        private void btnAging_Click(object sender, EventArgs e) =>
            RunTest(new JatlasAgingParser(), "run-jatlas-auto.bat", "-l aging --stage dev");

        private void btnCommonOffline_Click(object sender, EventArgs e) =>
            RunTest(new JatlasTestParser((idx, msg) => _gridController.UpdateLastRow(msg)),
                    "run-jatlas-auto.bat", "-l common --offline");

        private void taskKillBtn_Click(object sender, EventArgs e)
            {
            Log.Information("Kill button clicked");
            _executor.Kill();
            }

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
            Log.Information("Form closing");
            _executor?.Kill();
            _executor?.Dispose();
            _outputHandler?.Dispose();
            Log.CloseAndFlush();
            base.OnFormClosing(e);
            }
        }
    }