using ATT_Wrapper.Components;
using ATT_Wrapper.Parsing;
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
    /// <summary>
    /// Главная форма приложения. Управляет запуском тестов, отображением логов
    /// и взаимодействием с пользователем.
    /// </summary>
    public partial class JatlasTestRunnerForm : MaterialForm
        {
        private const string SCRIPT_PATH = @"C:\jatlas\scripts\win_scripts\";

        private MaterialSkinManager _materialSkinManager;
        private ProcessExecutor _executor;
        private ResultsGridController _gridController;
        private MappingManager _mapper;
        private ConsoleOutputHandler _outputHandler;
        private ToolStripLoadingSpinner _loadingSpinner;
        private ReportStatusManager _reportStatusManager;

        private string _mainLogPath;
        private Action _onTaskFinished;
        private bool _isTaskRunning = false;

        /// <summary>
        /// Управляет состоянием выполнения задачи. 
        /// При изменении автоматически блокирует или разблокирует интерфейс.
        /// </summary>
        private bool IsTaskRunning
            {
            get => _isTaskRunning;
            set
                {
                _isTaskRunning = value;
                // Автоматически переключаем кнопки в UI потоке
                // Если задача бежит (true) -> кнопки выключены (false)
                this.BeginInvoke((Action)( () => ToggleButtons(!value) ));

                if (!_isTaskRunning)
                    {
                    SetStatus("Ready");
                    }
                }
            }

        public JatlasTestRunnerForm()
            {
            InitializeComponent();
            CenterAppWindow();
            SetupLogging();
            SetupDataGridView();
            SetupTheme();
            InitializeLoadingSpinner();
            SetupFocus();
            }

        // --- Lifecycle Methods ---

        private async void JatlasTestRunnerForm_Load(object sender, EventArgs e)
            {
            await CheckUpdateStatusAsync();
            }

        protected override void OnShown(EventArgs e)
            {
            base.OnShown(e);
            // Сбрасываем активный фокус, чтобы ни одна кнопка не была подсвечена при старте
            this.ActiveControl = null;
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

        // --- Setup & Initialization ---

        /// <summary>
        /// Центрирует окно приложения на текущем мониторе.
        /// </summary>
        private void CenterAppWindow()
            {
            Screen screen = Screen.FromControl(this);
            Rectangle workingArea = screen.WorkingArea;

            int x = workingArea.X + ( workingArea.Width - this.Width ) / 2;
            int y = workingArea.Y + ( workingArea.Height - this.Height ) / 2;

            if (y < workingArea.Y) y = workingArea.Y;

            this.Location = new Point(x, y);
            }

        /// <summary>
        /// Настраивает Serilog для записи логов в файл и окно отладки.
        /// </summary>
        private void SetupLogging()
            {
            string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(logDirectory)) Directory.CreateDirectory(logDirectory);
            _mainLogPath = Path.Combine(logDirectory, "jatlas_runner.log");

            // Очистка старого лога
            if (File.Exists(_mainLogPath))
                {
                try { File.Delete(_mainLogPath); }
                catch (Exception ex) { Debug.WriteLine($"Could not clear log file: {ex.Message}"); }
                }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.With(new CallerEnricher())
                .WriteTo.Debug(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Caller}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(_mainLogPath, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{Caller}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("=== App Started ===");
            Log.Information($"Log file: {_mainLogPath}");
            }

        /// <summary>
        /// Применяет тему Material Design и настраивает менеджер иконок статуса.
        /// </summary>
        private void SetupTheme()
            {
            _materialSkinManager = MaterialSkinManager.Instance;
            _materialSkinManager.AddFormToManage(this);
            _materialSkinManager.Theme = MaterialSkinManager.Themes.LIGHT;

            _materialSkinManager.ColorScheme = new ColorScheme(
                Primary.BlueGrey500,
                Primary.BlueGrey700,
                Primary.LightBlue100,
                Accent.Red700,
                TextShade.WHITE
            );

            // Инициализация менеджера статусов (только один раз!)
            _reportStatusManager = new ReportStatusManager();

            // Регистрация загрузчиков
            if (NextсloudStatusIcon != null && NextсloudStatusLabel != null)
                _reportStatusManager.Register("nextcloud", NextсloudStatusIcon, NextсloudStatusLabel);

            if (CalydonStatusIcon != null && CalydonStatusLabel != null)
                _reportStatusManager.Register("webhook", CalydonStatusIcon, CalydonStatusLabel);

            if (FeishuStatusIcon != null && FeishuStatusLabel != null)
                _reportStatusManager.Register("feishubot", FeishuStatusIcon, FeishuStatusLabel);

            ThemeManager.Apply(this, dgvResults, rtbLog, mainButtonsLayoutPanel, extraButtonsLayoutPanel, ReportStatusLayoutPanel);
            }

        /// <summary>
        /// Инициализирует контроллер таблицы результатов.
        /// </summary>
        private void SetupDataGridView()
            {
            string mappingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mappings.json");
            _mapper = new MappingManager(mappingPath);
            _gridController = new ResultsGridController(dgvResults, _mapper);
            }

        /// <summary>
        /// Пересоздает исполнителя процессов, подписываясь на события вывода.
        /// </summary>
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

        /// <summary>
        /// Добавляет кастомный спиннер загрузки в статус-бар.
        /// </summary>
        private void InitializeLoadingSpinner()
            {
            _loadingSpinner = new ToolStripLoadingSpinner
                {
                SpinnerColor = _materialSkinManager.ColorScheme.AccentColor,
                Visible = false,
                Alignment = ToolStripItemAlignment.Right
                };

            statusStrip.Items.Insert(0, _loadingSpinner);

            statusLabel.TextChanged += (s, e) =>
            {
                bool isReady = statusLabel.Text == "Ready";
                _loadingSpinner.Visible = !isReady;
            };
            }

        /// <summary>
        /// Настраивает поведение фокуса, чтобы клики не оставляли выделение на элементах.
        /// </summary>
        private void SetupFocus()
            {
            void removeFocus(object s, MouseEventArgs e)
                {
                this.ActiveControl = null;
                }

            tabControlOutput.MouseDown += removeFocus;
            tabControlActions.MouseDown += removeFocus;
            dgvResults.MouseDown += removeFocus;
            }

        // --- Core Logic ---

        /// <summary>
        /// Асинхронно проверяет наличие обновлений в репозитории.
        /// </summary>
        private async Task CheckUpdateStatusAsync()
            {
            if (IsTaskRunning) return;

            try
                {
                IsTaskRunning = true;
                SetStatus("Checking updates...");

                var checker = new UpdateChecker();
                LogResult checkResult = await checker.IsUpdateAvailable();

                if (checkResult.Level == LogLevel.Pass && checkResult.Message.Equals("Updates found"))
                    {
                    btnUpdate.Text = "Update available";
                    btnUpdate.UseAccentColor = true;
                    }
                else if (checkResult.Level == LogLevel.Fail)
                    {
                    btnUpdate.Text = "Update check fail";
                    btnUpdate.UseAccentColor = true;
                    }
                else
                    {
                    btnUpdate.Text = "Update";
                    btnUpdate.UseAccentColor = false;
                    }
                }
            catch (Exception ex)
                {
                Log.Error($"Update check failed: {ex.Message}");
                }
            finally
                {
                IsTaskRunning = false;
                }
            }

        /// <summary>
        /// Запускает выполнение тестового скрипта.
        /// </summary>
        /// <param name="parser">Парсер логов для конкретного типа теста.</param>
        /// <param name="script">Имя скрипта (bat-файла).</param>
        /// <param name="args">Аргументы запуска.</param>
        /// <param name="onFinished">Колбэк, вызываемый после завершения процесса.</param>
        private void RunTest(ILogParser parser, string script, string args, Action onFinished = null)
            {
            IsTaskRunning = true;
            _onTaskFinished = onFinished;

            // Очистка UI перед запуском
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
                SetStatus,
                () =>
                {
                    Log.Information("Auto-enter callback triggered, sending Enter key");
                    _executor.SendInput("\r\n");
                }
            );

            try
                {
                Environment.SetEnvironmentVariable("TERM", "xterm-256color");
                Environment.SetEnvironmentVariable("FORCE_COLOR", "1");

                string fullPath = Path.Combine(SCRIPT_PATH, script);

                if (!File.Exists(fullPath))
                    {
                    Log.Error($"Script file not found: {fullPath}");
                    MessageBox.Show($"Script not found: {fullPath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    IsTaskRunning = false; // Разблокируем вручную, т.к. процесс не стартовал
                    return;
                    }

                string cmdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                string commandLine = string.IsNullOrWhiteSpace(args)
                    ? $"\"{cmdPath}\" /c \"{fullPath}\""
                    : $"\"{cmdPath}\" /c \"\"{fullPath}\" {args}\"";

                Log.Information($"Starting test: {commandLine}");
                _executor.Start(commandLine);
                }
            catch (Exception ex)
                {
                Log.Error(ex, "Start failed");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                IsTaskRunning = false;
                }
            }

        /// <summary>
        /// Обновляет текст в статус-баре (thread-safe).
        /// </summary>
        private void SetStatus(string status)
            {
            this.BeginInvoke((Action)( () =>
            {
                if (statusLabel != null)
                    {
                    statusLabel.Text = status;
                    }
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

        // --- Event Handlers ---

        private void HandleOutput(string line)
            {
            if (this.IsDisposed || !this.IsHandleCreated) return;

            try
                {
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
                IsTaskRunning = false;

                if (_onTaskFinished != null)
                    {
                    _onTaskFinished.Invoke();
                    _onTaskFinished = null;
                    }
            } ));
            }

        // --- Button Click Handlers ---

        private void UpdateATT(object sender, EventArgs e) =>
             RunTest(
                 new JatlasUpdateParser(),
                 "update.bat",
                 "",
                 async () => await CheckUpdateStatusAsync()
             );

        private void CommonATT(object sender, EventArgs e) =>
            RunTest(new JatlasTestParser(), "run-jatlas-auto.bat", "-l common --stage dev");

        private void SpecialATT(object sender, EventArgs e) =>
            RunTest(new JatlasTestParser(), "run-jatlas-auto.bat", "-l special --stage dev");

        private void AgingATT(object sender, EventArgs e) =>
            RunTest(new JatlasTestParser(), "run-jatlas-auto.bat", "-l aging --stage dev");

        private void CommonOfflineATT(object sender, EventArgs e) =>
            RunTest(new JatlasTestParser(), "run-jatlas-auto.bat", "-l common --offline");

        private void MockReportATT(object sender, EventArgs e) =>
            RunTest(new JatlasTestParser(), "run-jatlas.bat", "suites\\SDNB-M16iA.yaml -l common --stage dev");

        private void KillTask(object sender, EventArgs e)
            {
            Log.Information("Kill button clicked");
            _executor?.Kill();
            }

        // --- Helpers ---

        /// <summary>
        /// Вспомогательный класс для добавления имени метода в логи Serilog.
        /// </summary>
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