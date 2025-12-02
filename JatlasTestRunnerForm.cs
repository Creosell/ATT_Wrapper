using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Serilog;

namespace ATT_Wrapper
    {
    public partial class JatlasTestRunnerForm : Form
        {
        private const string SCRIPT_PATH = @"C:\jatlas\scripts\win_scripts\";
        private Process _currentProcess;
        private int _processId = -1;
        private ILogParser _currentParser; // Active parser strategy

        public JatlasTestRunnerForm()
            {
            InitializeComponent();
            SetupGridColumns();
            SetupLogging();
            }

        private void SetupLogging()
            {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File("logs/jatlas_runner_.log",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
            Log.Information("=== Application Started ===");
            }

        private void SetupGridColumns()
            {
            dgvResults.Columns.Clear();
            dgvResults.RowHeadersVisible = false;
            dgvResults.Columns.Add("Status", "Status");
            dgvResults.Columns.Add("Component", "Component / Description");
            dgvResults.Columns[0].FillWeight = 15;
            dgvResults.Columns[1].FillWeight = 85;
            dgvResults.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            }

        // --- Button Handlers (Parser Selection) ---

        private void btnUpdate_Click(object sender, EventArgs e)
            {
            _currentParser = new JatlasUpdateParser();
            RunProcess("update.bat", "");
            }

        private void btnCommon_Click(object sender, EventArgs e)
            {
            _currentParser = new JatlasTestParser((idx, msg) => UpdateLastGridRow(msg));
            RunProcess("run-jatlas-auto.bat", "-l common --stage dev");
            }

        private void btnSpecial_Click(object sender, EventArgs e)
            {
            _currentParser = new JatlasTestParser((idx, msg) => UpdateLastGridRow(msg));
            RunProcess("run-jatlas-auto.bat", "-l special --stage dev");
            }

        private void btnCommonOffline_Click(object sender, EventArgs e)
            {
            _currentParser = new JatlasTestParser((idx, msg) => UpdateLastGridRow(msg));
            RunProcess("run-jatlas-auto.bat", "-l common --offline");
            }

        private void taskKillBtn_Click(object sender, EventArgs e) => KillProcess();

        // --- Process Management ---

        public void KillProcess()
            {
            if (_processId != -1)
                {
                try
                    {
                    Log.Warning($"Killing PID: {_processId}");
                    Process.Start(new ProcessStartInfo("taskkill", $"/F /T /PID {_processId}") { CreateNoWindow = true, UseShellExecute = false });
                    }
                catch (Exception ex)
                    {
                    Log.Error(ex, "Failed to kill process");
                    MessageBox.Show($"Error: {ex.Message}");
                    }
                finally
                    {
                    _processId = -1;
                    ToggleButtons(true);
                    if (statusLabel != null) statusLabel.Text = "Terminated by user";
                    }
                }
            }

        private void RunProcess(string batchFile, string arguments)
            {
            ToggleButtons(false);
            dgvResults.Rows.Clear();
            if (statusLabel != null) statusLabel.Text = "Initializing...";

            string fullPath = Path.Combine(SCRIPT_PATH, batchFile);
            Log.Information($"Starting: {fullPath} {arguments} [Parser: {_currentParser?.GetType().Name}]");

            var psi = new ProcessStartInfo("cmd.exe", $"/c \"{fullPath}\" {arguments}")
                {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
                };

            // Force INFO logging for Jatlas to capture task progress
            if (psi.EnvironmentVariables.ContainsKey("JATLAS_LOG_LEVEL")) psi.EnvironmentVariables["JATLAS_LOG_LEVEL"] = "INFO";
            else psi.EnvironmentVariables.Add("JATLAS_LOG_LEVEL", "INFO");

            if (!psi.EnvironmentVariables.ContainsKey("PYTHONUNBUFFERED")) psi.EnvironmentVariables.Add("PYTHONUNBUFFERED", "1");

            try
                {
                _currentProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };

                _currentProcess.Exited += (s, e) => this.Invoke((Action)( () => {
                    Log.Information($"Exited. Last PID: {_processId}");
                    _processId = -1;
                    if (statusLabel != null) statusLabel.Text = "Ready";
                    ToggleButtons(true);
                } ));

                _currentProcess.Start();
                _processId = _currentProcess.Id;
                Log.Information($"Started PID: {_processId}");

                // Start async stream reading
                System.Threading.Tasks.Task.Run(() => ReadStreamAsync(_currentProcess.StandardOutput));
                System.Threading.Tasks.Task.Run(() => ReadStreamAsync(_currentProcess.StandardError));
                }
            catch (Exception ex)
                {
                Log.Error(ex, "Start error");
                MessageBox.Show($"Start error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ToggleButtons(true);
                if (statusLabel != null) statusLabel.Text = "Error";
                }
            }

        private async void ReadStreamAsync(StreamReader reader)
            {
            char[] buffer = new char[1024];
            System.Text.StringBuilder lineBuffer = new System.Text.StringBuilder();

            try
                {
                while (!_currentProcess.HasExited)
                    {
                    int bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    lineBuffer.Append(buffer, 0, bytesRead);
                    string content = lineBuffer.ToString();

                    int newlineIndex;
                    while (( newlineIndex = content.IndexOf('\n') ) >= 0)
                        {
                        string line = content.Substring(0, newlineIndex).Trim();
                        ParseAndDisplayOutput(line);
                        content = content.Substring(newlineIndex + 1);
                        }

                    lineBuffer.Clear();
                    lineBuffer.Append(content);

                    // Check for "pause" prompt in the buffer tail
                    if (content.Contains("Press any key to continue"))
                        {
                        ParseAndDisplayOutput(content.Trim());
                        lineBuffer.Clear();
                        }
                    }
                }
            catch (Exception ex) { Log.Debug($"Stream read error: {ex.Message}"); }
            }

        private void ParseAndDisplayOutput(string line)
            {
            if (string.IsNullOrWhiteSpace(line)) return;
            Debug.WriteLine($"[RAW] {line}");
            Log.Debug($"[OUT] {line}");

            // Handle "Pause" universally
            if (line.Contains("Press any key"))
                {
                this.Invoke((Action)( () => { if (statusLabel != null) statusLabel.Text = "Finalizing..."; } ));
                try { _currentProcess.StandardInput.WriteLine(); } catch { }
                return;
                }

            // Delegate logic to current parser
            _currentParser?.ParseLine(line,
                (status, msg) => this.Invoke((Action)( () => AddGridRow(status, msg) )),
                (progMsg) => this.Invoke((Action)( () => { if (statusLabel != null) statusLabel.Text = progMsg; } ))
            );
            }

        // --- Grid Helpers ---

        private void AddGridRow(string status, string message)
            {
            int rowIndex = dgvResults.Rows.Add(status, message);
            var row = dgvResults.Rows[rowIndex];

            switch (status)
                {
                case "PASS": row.DefaultCellStyle.BackColor = Color.FromArgb(220, 255, 220); break;
                case "FAIL": row.DefaultCellStyle.BackColor = Color.FromArgb(255, 220, 220); break;
                case "ERROR": row.DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 220); break;
                case "INFO": row.DefaultCellStyle.BackColor = Color.FromArgb(240, 240, 255); break;
                }
            dgvResults.FirstDisplayedScrollingRowIndex = rowIndex;
            }

        private void UpdateLastGridRow(string newMessage)
            {
            if (dgvResults.Rows.Count > 0)
                {
                dgvResults.Rows[dgvResults.Rows.Count - 1].Cells[1].Value = newMessage;
                }
            }

        private void ToggleButtons(bool state)
            {
            if (btnUpdate != null) btnUpdate.Enabled = state;
            if (btnCommon != null) btnCommon.Enabled = state;
            if (btnSpecial != null) btnSpecial.Enabled = state;
            if (btnCommonOffline != null) btnCommonOffline.Enabled = state;
            }

        protected override void OnFormClosing(FormClosingEventArgs e)
            {
            Log.CloseAndFlush();
            base.OnFormClosing(e);
            }
        }
    }