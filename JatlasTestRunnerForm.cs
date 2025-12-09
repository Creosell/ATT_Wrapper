using ATT_Wrapper.Components;
using ATT_Wrapper.Services;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ATT_Wrapper
    {
    public partial class JatlasTestRunnerForm : Form
        {
        private const string SCRIPT_PATH = @"C:\jatlas\scripts\win_scripts\";

        private ProcessExecutor _executor;
        private readonly ResultsGridController _gridController;
        private readonly MappingManager _mapper;
        private readonly string _mainLogPath;

        // Output handler (parsing logic for colors and text)
        private ConsoleOutputHandler _outputHandler;

        // Regex for cleaning debug output
        private const string AnsiRegex = @"\x1B\[[0-9;?]*[ -/]*[@-~]";

        public JatlasTestRunnerForm()
            {
            InitializeComponent();

            // Setup single log file path
            string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(logDirectory))
                {
                Directory.CreateDirectory(logDirectory);
                }
            _mainLogPath = Path.Combine(logDirectory, "jatlas_runner.log");

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
            // Clear the log file if it exists
            if (File.Exists(_mainLogPath))
                {
                try { File.Delete(_mainLogPath); }
                catch (Exception ex) { Debug.WriteLine($"Could not clear log file: {ex.Message}"); }
                }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.With(new CallerEnricher()) // <--- ПОДКЛЮЧАЕМ НАШ ОБОГАТИТЕЛЬ
                .WriteTo.Debug(
                    // Добавляем {Caller} в шаблон вывода Debug
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Caller}] {Message:lj}{NewLine}{Exception}"
                )
                .WriteTo.File(
                    _mainLogPath,
                    // Добавляем {Caller} в шаблон вывода Файла
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{Caller}] {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();

            Log.Information("=== App Started ===");
            Log.Information($"Log file: {_mainLogPath}");
            }

        private void InitializeExecutor()
            {
            // Если старый существует - убиваем и очищаем
            if (_executor != null)
                {
                try
                    {
                    _executor.OnOutputReceived -= HandleOutput;
                    _executor.OnExited -= HandleExit;
                    _executor.Dispose();
                    }
                catch { /* ignore */ }
                }

            // Создаем свежий экземпляр
            _executor = new ProcessExecutor();
            _executor.OnOutputReceived += HandleOutput;
            _executor.OnExited += HandleExit;
            }

        // --- RUN LOGIC ---

        private void RunTest(ILogParser parser, string script, string args)
            {
            ToggleButtons(false);
            _gridController.Clear();
            rtbLog.Clear();

            if (statusLabel != null) statusLabel.Text = "Initializing...";

            // Dispose previous output handler if exists
            _outputHandler?.Dispose();

            InitializeExecutor();

            Log.Information($"Creating output handler for script: {script}");

            // Create handler for current run - NO separate console log file
            _outputHandler = new ConsoleOutputHandler(
                parser,
                _gridController,
                (status) => this.BeginInvoke((Action)( () =>
                {
                    if (statusLabel != null)
                        statusLabel.Text = status;
                } )),
                () =>
                {
                    Log.Information("Auto-enter callback triggered, sending Enter key");
                    _executor.SendInput("\r\n");
                },
                null  // No separate console log - everything goes to main log
            );

            try
                {
                // Enable color support
                Environment.SetEnvironmentVariable("TERM", "xterm-256color");
                Environment.SetEnvironmentVariable("FORCE_COLOR", "1");

                string fullPath = Path.Combine(SCRIPT_PATH, script);

                // Check if file exists
                if (!File.Exists(fullPath))
                    {
                    Log.Error($"Script file not found: {fullPath}");
                    MessageBox.Show($"Script not found: {fullPath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    ToggleButtons(true);
                    return;
                    }

                // Get cmd.exe path
                string cmdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

                // Build command line properly
                // For batch files, we need: cmd.exe /c "path\to\script.bat" arg1 arg2
                string commandLine;
                if (string.IsNullOrWhiteSpace(args))
                    {
                    commandLine = $"\"{cmdPath}\" /c \"{fullPath}\"";
                    }
                else
                    {
                    commandLine = $"\"{cmdPath}\" /c \"\"{fullPath}\" {args}\"";
                    }

                Log.Information($"Starting test:");
                Log.Information($"  Script: {script}");
                Log.Information($"  Args: {args}");
                Log.Information($"  Full path: {fullPath}");
                Log.Information($"  Command: {commandLine}");

                _executor.Start(commandLine);

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
                //// Log ALL raw output for debugging - escape control chars
                //string escapedLine = line
                //    .Replace("\r", "\\r")
                //    .Replace("\n", "\\n")
                //    .Replace("\x1b", "\\x1b");

                //if (escapedLine.Length > 200)
                //    {
                //    Log.Debug($"[HandleOutput] {escapedLine.Substring(0, 200)}...");
                //    }
                //else
                //    {
                //    Log.Debug($"[HandleOutput] {escapedLine}");
                //    }

                //// 1. Output to Debug (VS Output window) - clean text without noise
                //string plainLine = Regex.Replace(line, AnsiRegex, "");
                //bool isProgress = plainLine.TrimStart().StartsWith("Running task:", StringComparison.OrdinalIgnoreCase);
                //bool isInfo = plainLine.Contains("INFO");

                //if (!isProgress && !isInfo && !string.IsNullOrWhiteSpace(plainLine))
                //    {
                //    // Теперь здесь будет видно [JatlasTestRunnerForm.HandleOutput] в логах
                //    Log.Information($"[CONSOLE] {plainLine}");
                //    }

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

        private void HandleExit(object sender, EventArgs e)
            {
            Log.Information("Process exited");

            if (this.IsDisposed || !this.IsHandleCreated) return;

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



        private void testBtn_Click(object sender, EventArgs e)
            {
            Log.Information("=== ANSI COLOR TEST STARTED ===");


            // Reset UI
            ToggleButtons(false);
            rtbLog.Clear();
            _gridController.Clear();
            if (statusLabel != null) statusLabel.Text = "Running ANSI Test...";

            InitializeExecutor();

            // Dispose previous handler
            _outputHandler?.Dispose();

            // Create a proper batch file
            string tempBatPath = Path.Combine(Path.GetTempPath(), "test_colors.bat");
            string esc = "\x1b";

            // Create batch content WITHOUT @ECHO OFF so we can see what's happening
            string batchContent =
                "@echo OFF"+
                "ECHO === START ANSI TEST ===\r\n" +
                "ECHO Normal Text Line\r\n" +
                $"ECHO {esc}[32m[PASS] This text should be GREEN{esc}[0m\r\n" +
                $"ECHO {esc}[31m[FAIL] This text should be RED{esc}[0m\r\n" +
                $"ECHO {esc}[34m[INFO] This text should be BLUE{esc}[0m\r\n" +
                $"ECHO {esc}[1;33m[WARN] This is BOLD YELLOW{esc}[0m\r\n" +
                "ECHO.\r\n" +
                "ECHO Testing Auto-Enter logic below:\r\n" +
                "ECHO About to pause - should auto-continue...\r\n" +
                "PAUSE\r\n" +
                "ECHO.\r\n" +
                "ECHO === AUTO-ENTER WORKED! ===\r\n" +
                "ECHO Test completed successfully\r\n";

            try
                {
                System.IO.File.WriteAllText(tempBatPath, batchContent, System.Text.Encoding.ASCII);
                Log.Information($"Created test batch file: {tempBatPath}");

                // Verify the file was created and log its contents
                string readBack = File.ReadAllText(tempBatPath);
                Log.Debug($"Batch file contents ({readBack.Length} chars): {readBack.Substring(0, Math.Min(200, readBack.Length))}...");
                }
            catch (Exception ex)
                {
                Log.Error(ex, "Failed to create test batch file");
                MessageBox.Show($"Failed to create test file: {ex.Message}");
                ToggleButtons(true);
                return;
                }

            // Initialize OutputHandler with auto-enter callback
            _outputHandler = new ConsoleOutputHandler(
                new JatlasTestParser((idx, msg) => _gridController.UpdateLastRow(msg)),
                _gridController,
                (status) => this.BeginInvoke((Action)( () =>
                {
                    if (statusLabel != null)
                        statusLabel.Text = status;
                    Log.Information($"[UI] Status updated: {status}");
                } )),
                () =>
                {
                    Log.Information("*** AUTO-ENTER TRIGGERED BY PAUSE DETECTION ***");
                    _executor.SendInput("\r\n");
                },
                null  // Use main log file
            );

            try
                {
                // Enable color support
                Environment.SetEnvironmentVariable("TERM", "xterm-256color");
                Environment.SetEnvironmentVariable("FORCE_COLOR", "1");

                // Get proper cmd.exe path
                string cmdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

                // Use cmd /c to run the batch file
                string commandLine = $"\"{cmdPath}\" /c \"{tempBatPath}\"";

                Log.Information($"Starting ANSI test process...");
                Log.Information($"CMD: {cmdPath}");
                Log.Information($"Batch: {tempBatPath}");
                Log.Information($"Command: {commandLine}");

                _executor.Start(commandLine);

                Log.Information("Test process started successfully");
                }
            catch (Exception ex)
                {
                Log.Error(ex, "ANSI Test failed to start");
                MessageBox.Show($"Error: {ex.Message}");
                ToggleButtons(true);
                }
            }

        public class CallerEnricher : ILogEventEnricher
            {
            public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
                {
                var skip = 3; // Пропускаем методы самого Serilog
                while (true)
                    {
                    var stack = new StackFrame(skip);
                    if (!stack.HasMethod()) break;

                    var method = stack.GetMethod();
                    var declaringType = method.DeclaringType;

                    // Пропускаем сборку Serilog, чтобы найти реальный вызов из нашего кода
                    if (declaringType == null || declaringType.Assembly.GetName().Name.StartsWith("Serilog"))
                        {
                        skip++;
                        continue;
                        }

                    // Формируем строку "Class.Method"
                    var caller = $"{declaringType.Name}.{method.Name}";
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Caller", caller));
                    break;
                    }
                }
            }
        }
    }