using ATT_Wrapper.Components;
using ATT_Wrapper.Services;
using MaterialSkin;
using MaterialSkin.Controls;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ATT_Wrapper
    {
    public partial class JatlasTestRunnerForm : MaterialForm
        {
        private const string SCRIPT_PATH = @"C:\jatlas\scripts\win_scripts\";
        private MaterialSkinManager materialSkinManager;

        private ProcessExecutor _executor;
        private ResultsGridController _gridController;
        private MappingManager _mapper;
        private string _mainLogPath;
        private ConsoleOutputHandler _outputHandler;
        private ToolStripLoadingSpinner _loadingSpinner;
        private Action _onTaskFinished;
        private ReportStatusManager _reportStatusManager;

        public JatlasTestRunnerForm()
            {
            InitializeComponent();

            // Center window
            CenterAppWindow();

            // Setup logging
            SetupLogging();

            // Setup DataGridView
            SetupDataGridView();

            // Setup theme
            SetupTheme();

            // Initialize loading spinner in status bar
            InitializeLoadingSpinner();

            // Setup focus behavior
            SetupFocus();
            }

        private async void JatlasTestRunnerForm_Load(object sender, EventArgs e)
            {
            await CheckUpdateStatusAsync();
            }

        private void CenterAppWindow()
            {

            Screen screen = Screen.FromControl(this);

            // WorkingArea — это область экрана БЕЗ панели задач
            Rectangle workingArea = screen.WorkingArea;

            // Вычисляем координаты левого верхнего угла для центра
            int x = workingArea.X + ( workingArea.Width - this.Width ) / 2;
            int y = workingArea.Y + ( workingArea.Height - this.Height ) / 2;

            // Если по Y мы улезаем вверх (из-за заголовка), ставим 0
            if (y < workingArea.Y) y = workingArea.Y;

            // Применяем
            this.Location = new Point(x, y);
            }

        private void SetupLogging()
            {
            string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(logDirectory)) Directory.CreateDirectory(logDirectory);
            _mainLogPath = Path.Combine(logDirectory, "jatlas_runner.log");

            // Clear existing log file
            if (File.Exists(_mainLogPath))
                {
                try { File.Delete(_mainLogPath); }
                catch (Exception ex) { Debug.WriteLine($"Could not clear log file: {ex.Message}"); }
                }

            // Configure Serilog with CallerEnricher
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.With(new CallerEnricher())
                .WriteTo.Debug(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Caller}] {Message:lj}{NewLine}{Exception}"
                )
                .WriteTo.File(
                    _mainLogPath,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{Caller}] {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();

            Log.Information("=== App Started ===");
            Log.Information($"Log file: {_mainLogPath}");
            }

        private void SetupTheme()
            {
            // Visual setup
            // Инициализация MaterialSkin
            materialSkinManager = MaterialSkinManager.Instance;
            materialSkinManager.AddFormToManage(this);
            materialSkinManager.Theme = MaterialSkinManager.Themes.LIGHT;

            // Цветовая палитра Material Design
            materialSkinManager.ColorScheme = new ColorScheme(
                Primary.BlueGrey500,
                Primary.BlueGrey700,
                Primary.LightBlue100,
                Accent.Red700,
                TextShade.WHITE
            );

            _reportStatusManager = new ReportStatusManager();

            // Инициализация менеджера
            _reportStatusManager = new ReportStatusManager();

            // === РЕГИСТРАЦИЯ ЗАГРУЗЧИКОВ ===

            // 1. Nextcloud
            if (NextсloudStatusIcon != null && NextсloudStatusLabel != null)
                {
                _reportStatusManager.Register("nextcloud", NextсloudStatusIcon, NextсloudStatusLabel);
                }

            // 2. Calydon
            if (CalydonStatusIcon != null && CalydonStatusLabel != null)
                {
                _reportStatusManager.Register("webhook", CalydonStatusIcon, CalydonStatusLabel);
                }

            // 3. FeishuBot
            if (FeishuStatusIcon != null && FeishuStatusLabel != null)
                {
                _reportStatusManager.Register("feishubot", FeishuStatusIcon, FeishuStatusLabel);
                }

            // Custom theme manager
            ThemeManager.Apply(this, dgvResults, rtbLog, mainButtonsLayoutPanel, extraButtonsLayoutPanel, ReportStatusLayoutPanel);
            }

        private void SetupDataGridView()
            {
            // Mapping setup
            string mappingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mappings.json");
            _mapper = new MappingManager(mappingPath);

            // ResultGrid setup
            _gridController = new ResultsGridController(dgvResults, _mapper);
            }

        private void InitializeExecutor()
            {
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

            _executor = new ProcessExecutor();
            _executor.OnOutputReceived += HandleOutput;
            _executor.OnExited += HandleExit;
            }

        private async Task CheckUpdateStatusAsync()
            {
            try
                {
                // Создаем экземпляр нашего нового сервиса
                var checker = new UpdateChecker();

                SetStatus("Checking updates...");

                bool isUpdateAvailable = await checker.IsUpdateAvailable();

                if (isUpdateAvailable)
                    {
                    // Если есть обновление - выделяем кнопку
                    btnUpdate.Text = "UPDATE AVAILABLE";
                    btnUpdate.UseAccentColor = true; 
                    }
                else
                    {
                    // Если обновлений нет, возвращаем обычный текст
                    btnUpdate.Text = "Update";
                    btnUpdate.UseAccentColor = false;
                    }
                SetStatus("Ready");
                }
            catch (Exception ex)
                {
                // Если что-то пошло не так (нет интернета и т.д.), просто логируем и оставляем всё как есть
                Console.WriteLine($"Update check failed: {ex.Message}");
                btnUpdate.Text = "Update failed";
                SetStatus("Ready");
                }
            }

        private void SetStatus(string status)
            {
            this.BeginInvoke((Action)( () =>
            {
                if (statusLabel != null)
                    {
                    statusLabel.Text = status;
                    }
                ToggleButtons(true);
            } ));
            }

        // --- RUN LOGIC ---

        private void RunTest(ILogParser parser, string script, string args, Action onFinished = null)
            {
            _onTaskFinished = onFinished;
            ToggleButtons(false);
            _gridController.Clear();
            rtbLog.Clear();
            _reportStatusManager?.ResetAll();

            if (statusLabel != null) statusLabel.Text = "Initializing...";

            _outputHandler?.Dispose();

            InitializeExecutor();

            Log.Information($"Creating output handler for script: {script}");

            _outputHandler = new ConsoleOutputHandler(
                parser,
                _gridController,
                _reportStatusManager,
                (status) => this.BeginInvoke((Action)( () =>
                {
                    if (statusLabel != null)
                        statusLabel.Text = status;
                } )),
                () =>
                {
                    Log.Information("Auto-enter callback triggered, sending Enter key");
                    _executor.SendInput("\r\n");
                }
            );

            try
                {
                // Force color output for terminals
                Environment.SetEnvironmentVariable("TERM", "xterm-256color");
                Environment.SetEnvironmentVariable("FORCE_COLOR", "1");

                string fullPath = Path.Combine(SCRIPT_PATH, script);

                if (!File.Exists(fullPath))
                    {
                    Log.Error($"Script file not found: {fullPath}");
                    MessageBox.Show($"Script not found: {fullPath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    ToggleButtons(true);
                    return;
                    }

                string cmdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

                // Construct command line for batch files
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
                Log.Information($"  On Finished: {onFinished}");

                _executor.Start(commandLine);

                Log.Information("Process started successfully");
                }
            catch (Exception ex)
                {
                Log.Error(ex, "Start failed");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ToggleButtons(true);
                this.BeginInvoke((Action)( () =>
                {
                    if (statusLabel != null) statusLabel.Text = "Ready";
                    ToggleButtons(true);
                } ));
                }
            }

        private void HandleOutput(string line)
            {
            if (this.IsDisposed || !this.IsHandleCreated) return;

            try
                {
                // Pass output to handler for UI update and parsing
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
                if (_onTaskFinished != null)
                    {
                    _onTaskFinished.Invoke();
                    _onTaskFinished = null;
                    }
            } ));
            }

        // --- BUTTONS ---
        private void UpdateATT(object sender, EventArgs e) =>
            RunTest(
                new JatlasUpdateParser(),
                "update.bat",
                "",
                // Передаем действие, которое выполнится после закрытия консоли
                async () => await CheckUpdateStatusAsync()
            );

        private void CommonATT(object sender, EventArgs e) =>
            RunTest(new JatlasTestParser((idx, msg) => _gridController.UpdateLastRow(msg)),
                    "run-jatlas-auto.bat", "-l common --stage dev");

        private void SpecialATT(object sender, EventArgs e) =>
            RunTest(new JatlasTestParser((idx, msg) => _gridController.UpdateLastRow(msg)),
                    "run-jatlas-auto.bat", "-l special --stage dev");

        private void AgingATT(object sender, EventArgs e) =>
            RunTest(new JatlasAgingParser(), "run-jatlas-auto.bat", "-l aging --stage dev");

        private void CommonOfflineATT(object sender, EventArgs e) =>
            RunTest(new JatlasTestParser((idx, msg) => _gridController.UpdateLastRow(msg)),
                    "run-jatlas-auto.bat", "-l common --offline");

        private void MockReportATT(object sender, EventArgs e) =>
            RunTest(new JatlasTestParser((idx, msg) => _gridController.UpdateLastRow(msg)),
            "run-jatlas.bat",
            ".\\suites\\SDNB-14iA.yaml -mr \"C:\\Users\\Pavel\\Downloads\\Wrapper_test_reports\\SDNB-14iA\\SDNB-14iA-Good.json\" -l common --offline");

        private void KillTask(object sender, EventArgs e)
            {
            Log.Information("Kill button clicked");

            _executor?.Kill();

            this.BeginInvoke((Action)( () =>
            {
                if (statusLabel != null)
                    {
                    statusLabel.Text = "Ready";
                    }
                ToggleButtons(true);
            } ));
            }

        private void ToggleButtons(bool enabled)
            {
            foreach (Control c in this.Controls) EnableRecursive(c, enabled);
            if (taskKillBtnMain != null) taskKillBtnMain.Enabled = true;
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

        private void InitializeLoadingSpinner()
            {

            // 1. Создаем наш кастомный спиннер
            _loadingSpinner = new ToolStripLoadingSpinner
                {
                SpinnerColor = materialSkinManager.ColorScheme.AccentColor, // Берем цвет из темы!
                Visible = false, // Скрыт по умолчанию

                // Выравниваем его вправо или сразу после текста (по желанию)
                Alignment = ToolStripItemAlignment.Right
                };

            // 2. Добавляем его в статус-бар
            statusStrip.Items.Insert(0, _loadingSpinner);

            // 3. Подписываемся на изменение текста лейбла
            statusLabel.TextChanged += (s, e) =>
            {
                bool isReady = statusLabel.Text == "Ready";
                // Показываем спиннер, если статус НЕ Ready
                _loadingSpinner.Visible = !isReady;
            };
            }

        private void SetupFocus()
            {
            void removeFocus(object s, MouseEventArgs e)
                {
                // ActiveControl = null убирает фокус с текущего элемента формы
                this.ActiveControl = null;
                }

            // 3. Применяем этот хак к обоим TabControl
            tabControlOutput.MouseDown +=  removeFocus ;
            tabControlActions.MouseDown +=  removeFocus ;

            dgvResults.MouseDown +=  removeFocus ;

            }

        protected override void OnShown(EventArgs e)
            {
            base.OnShown(e);

            // Сбрасываем активный фокус, чтобы ни одна кнопка не была подсвечена
            this.ActiveControl = null;

            // Если нужно, чтобы фокус был на таблице, но без выделения строки:
            // dgvResults.Focus();
            }


        public class CallerEnricher : ILogEventEnricher
            {
            public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
                {
                var skip = 3;
                while (true)
                    {
                    var stack = new StackFrame(skip);
                    if (!stack.HasMethod()) break;

                    var method = stack.GetMethod();
                    var declaringType = method.DeclaringType;

                    // Skip Serilog internal frames
                    if (declaringType == null || declaringType.Assembly.GetName().Name.StartsWith("Serilog"))
                        {
                        skip++;
                        continue;
                        }

                    var caller = $"{declaringType.Name}.{method.Name}";
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Caller", caller));
                    break;
                    }
                }
            }

        }
    }