using System;
using System.Collections.Generic;
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
        private ILogParser _currentParser;
        private MappingManager _mapper;

        private Dictionary<DataGridViewRow, List<DataGridViewRow>> _groupChildren = new Dictionary<DataGridViewRow, List<DataGridViewRow>>();
        private Dictionary<string, DataGridViewRow> _groupRowsCache = new Dictionary<string, DataGridViewRow>();

        public JatlasTestRunnerForm()
            {
            InitializeComponent();
            SetupGridColumns();
            SetupLogging();
            ApplyModernStyle();

            dgvResults.CellClick += DgvResults_CellClick;
            string mappingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mappings.json");
            _mapper = new MappingManager(mappingPath);
            }

        private void SetupLogging()
            {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File("logs/jatlas_runner_.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();
            Log.Information("=== Application Started ===");
            }

        private void SetupGridColumns()
            {
            dgvResults.Columns.Clear();
            dgvResults.RowHeadersVisible = false;
            dgvResults.ColumnHeadersVisible = false;
            dgvResults.Columns.Add("Status", "Status");
            dgvResults.Columns.Add("Component", "Component / Description");
            dgvResults.Columns[0].FillWeight = 15;
            dgvResults.Columns[1].FillWeight = 85;
            dgvResults.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            }

        private void ApplyModernStyle()
            {
            this.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            this.BackColor = Color.White;

            dgvResults.BackgroundColor = Color.White;
            dgvResults.BorderStyle = BorderStyle.None;
            dgvResults.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvResults.GridColor = Color.FromArgb(230, 230, 230);

            dgvResults.RowTemplate.Height = 32;
            dgvResults.DefaultCellStyle.SelectionBackColor = Color.FromArgb(240, 245, 255);
            dgvResults.DefaultCellStyle.SelectionForeColor = Color.Black;
            dgvResults.ReadOnly = true;

            if (rtbLog != null)
                {
                rtbLog.BackColor = Color.FromArgb(30, 30, 30);
                rtbLog.ForeColor = Color.Gainsboro;
                rtbLog.Font = new Font("Consolas", 10F);
                rtbLog.BorderStyle = BorderStyle.None;
                rtbLog.WordWrap = false;
                rtbLog.ScrollBars = RichTextBoxScrollBars.Both;
                }

            StyleButton(btnUpdate, Color.FromArgb(240, 240, 240));
            StyleButton(btnCommon, Color.FromArgb(225, 240, 255));
            StyleButton(btnSpecial, Color.FromArgb(225, 240, 255));
            StyleButton(btnCommonOffline, Color.FromArgb(240, 240, 240));
            if (taskKillBtn != null) StyleButton(taskKillBtn, Color.FromArgb(255, 235, 235));
            }

        private void StyleButton(Button btn, Color backColor)
            {
            if (btn == null) return;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = backColor;
            btn.Font = new Font("Segoe UI Semibold", 9.5F);
            btn.Cursor = Cursors.Hand;
            }

        // --- GRID LOGIC ---

        private void DgvResults_CellClick(object sender, DataGridViewCellEventArgs e)
            {
            if (e.RowIndex < 0) return;
            var clickedRow = dgvResults.Rows[e.RowIndex];
            if (_groupChildren.ContainsKey(clickedRow)) ToggleGroup(clickedRow);
            }

        private void ToggleGroup(DataGridViewRow groupRow, bool? forceState = null)
            {
            if (!_groupChildren.ContainsKey(groupRow)) return;
            var children = _groupChildren[groupRow];
            if (children.Count == 0) return;

            bool isExpanded = children[0].Visible;
            bool newState = forceState.HasValue ? forceState.Value : !isExpanded;
            if (isExpanded == newState) return;

            string statusText = groupRow.Cells[0].Value.ToString().Replace("▶ ", "").Replace("▼ ", "");
            groupRow.Cells[0].Value = ( newState ? "▼ " : "▶ " ) + statusText;

            foreach (var child in children) child.Visible = newState;
            }

        private void ProcessTestResult(string status, string message)
            {
            var (groupName, ufn) = _mapper.IdentifyCheck(message);

            if (string.IsNullOrEmpty(groupName))
                {
                AddRowToEnd(status, message);
                return;
                }

            if (!_groupRowsCache.ContainsKey(groupName))
                {
                int idx = dgvResults.Rows.Add($"▶ PASS", $"{groupName}: OK");
                var newGroupRow = dgvResults.Rows[idx];

                newGroupRow.DefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                newGroupRow.DefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
                SetRowColor(newGroupRow, "PASS", isGroup: true);

                _groupRowsCache[groupName] = newGroupRow;
                _groupChildren[newGroupRow] = new List<DataGridViewRow>();
                }

            var groupRow = _groupRowsCache[groupName];

            if (status == "FAIL")
                {
                if (!groupRow.Cells[0].Value.ToString().Contains("FAIL"))
                    {
                    groupRow.Cells[0].Value = "▼ FAIL";
                    groupRow.Cells[1].Value = $"{groupName}: Failed";
                    SetRowColor(groupRow, "FAIL", isGroup: true);
                    }
                ToggleGroup(groupRow, forceState: true);
                }

            var children = _groupChildren[groupRow];
            int insertIndex = groupRow.Index + children.Count + 1;
            if (insertIndex > dgvResults.Rows.Count) insertIndex = dgvResults.Rows.Count;

            dgvResults.Rows.Insert(insertIndex, status, ufn ?? message);
            var childRow = dgvResults.Rows[insertIndex];
            SetRowColor(childRow, status, isGroup: false);

            childRow.Cells[1].Style.Padding = new Padding(25, 0, 0, 0);
            childRow.Cells[1].Style.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            children.Add(childRow);
            bool isGroupExpanded = groupRow.Cells[0].Value.ToString().Contains("▼");
            childRow.Visible = isGroupExpanded;
            }

        private void AddRowToEnd(string status, string message)
            {
            int idx = dgvResults.Rows.Add(status, message);
            SetRowColor(dgvResults.Rows[idx], status);
            dgvResults.FirstDisplayedScrollingRowIndex = idx;
            dgvResults.ClearSelection();
            dgvResults.CurrentCell = null;
            }

        private void SetRowColor(DataGridViewRow row, string status, bool isGroup = false)
            {
            Color backPass = isGroup ? Color.FromArgb(230, 250, 230) : Color.White;
            Color backFail = isGroup ? Color.FromArgb(255, 230, 230) : Color.White;

            switch (status)
                {
                case "PASS":
                    row.DefaultCellStyle.BackColor = backPass;
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(0, 100, 0);
                    break;
                case "FAIL":
                    row.DefaultCellStyle.BackColor = backFail;
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(180, 0, 0);
                    break;
                case "ERROR":
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 250, 220);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(150, 100, 0);
                    break;
                default:
                    row.DefaultCellStyle.BackColor = Color.White;
                    row.DefaultCellStyle.ForeColor = Color.Black;
                    break;
                }
            }

        private void UpdateLastGridRow(string newMessage)
            {
            if (dgvResults.Rows.Count > 0)
                dgvResults.Rows[dgvResults.Rows.Count - 1].Cells[1].Value = newMessage;
            }

        // --- EXECUTION ---

        private void RunProcess(string batchFile, string arguments)
            {
            ToggleButtons(false);
            dgvResults.Rows.Clear();
            rtbLog.Clear();
            _groupChildren.Clear();
            _groupRowsCache.Clear();

            if (statusLabel != null) statusLabel.Text = "Initializing...";

            string fullPath = Path.Combine(SCRIPT_PATH, batchFile);
            Log.Information($"Starting: {fullPath} {arguments}");

            var psi = new ProcessStartInfo("cmd.exe", $"/c \"{fullPath}\" {arguments}")
                {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
                };

            var env = psi.EnvironmentVariables;
            env["JATLAS_LOG_LEVEL"] = "INFO";
            env["PYTHONUNBUFFERED"] = "1";
            env["FORCE_COLOR"] = "1";
            env["CLICOLOR_FORCE"] = "1";

            try
                {
                _currentProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _currentProcess.Exited += (s, e) => this.Invoke((Action)( () => {
                    _processId = -1;
                    if (statusLabel != null) statusLabel.Text = "Ready";
                    ToggleButtons(true);
                } ));

                _currentProcess.Start();
                _processId = _currentProcess.Id;

                System.Threading.Tasks.Task.Run(() => ReadStreamAsync(_currentProcess.StandardOutput));
                System.Threading.Tasks.Task.Run(() => ReadStreamAsync(_currentProcess.StandardError));
                }
            catch (Exception ex)
                {
                Log.Error(ex, "Start error");
                ToggleButtons(true);
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

                    // Ищем любой разделитель строк (\r или \n)
                    // Это позволит разделить "Running task... \r" и следующий лог
                    int splitIndex;
                    char[] separators = { '\n', '\r' };

                    while (( splitIndex = content.IndexOfAny(separators) ) >= 0)
                        {
                        string line = content.Substring(0, splitIndex).Trim();
                        if (!string.IsNullOrEmpty(line))
                            {
                            ParseAndDisplayOutput(line);
                            }

                        // Пропускаем сам разделитель и возможный следующий (\r\n)
                        int nextCharIdx = splitIndex + 1;
                        if (nextCharIdx < content.Length &&
                            ( ( content[splitIndex] == '\r' && content[nextCharIdx] == '\n' ) ||
                             ( content[splitIndex] == '\n' && content[nextCharIdx] == '\r' ) ))
                            {
                            nextCharIdx++;
                            }

                        content = content.Substring(nextCharIdx);
                        }

                    lineBuffer.Clear();
                    lineBuffer.Append(content);

                    // Проверка на "висящий" промпт (без Enter)
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

            // 1. Сначала чистим от ANSI, чтобы анализ текста работал корректно
            string plainLine = Regex.Replace(line, @"\x1B\[[^@-~]*[@-~]", "");

            // 2. Обновляем Expert View (Фильтрация мусора)
            this.Invoke((Action)( () =>
            {
                bool isInfo = plainLine.Contains("INFO"); // Фильтр служебных INFO
                bool isRunningTask = plainLine.TrimStart().StartsWith("Running task:"); // Фильтр таймеров задач

                if (!isInfo && !isRunningTask)
                    {
                    // Пишем ОРИГИНАЛЬНУЮ строку с цветами, но отфильтрованную
                    AppendAnsiText(rtbLog, line + Environment.NewLine);
                    }
            } ));

            // 3. Обработка завершения (Pause)
            if (line.Contains("Press any key"))
                {
                this.Invoke((Action)( () => { if (statusLabel != null) statusLabel.Text = "Finalizing..."; } ));
                try { _currentProcess.StandardInput.WriteLine(); } catch { }
                return;
                }

            // 4. Парсинг для таблицы (Simple View)
            _currentParser?.ParseLine(plainLine,
                (status, msg) => this.Invoke((Action)( () =>
                {
                    if (_currentParser is JatlasTestParser && ( status == "PASS" || status == "FAIL" ))
                        ProcessTestResult(status, msg);
                    else
                        AddRowToEnd(status, msg);
                } )),
                (progMsg) => this.Invoke((Action)( () => { if (statusLabel != null) statusLabel.Text = progMsg; } ))
            );
            }

        private void AppendAnsiText(RichTextBox box, string text)
            {
            int cursor = 0;
            var matches = Regex.Matches(text, @"\x1B\[(\d+)(;\d+)*m");

            if (matches.Count == 0)
                {
                box.AppendText(text);
                return;
                }

            foreach (Match match in matches)
                {
                box.AppendText(text.Substring(cursor, match.Index - cursor));

                string code = match.Groups[1].Value;
                int colorCode = int.Parse(code);
                box.SelectionStart = box.TextLength;
                box.SelectionLength = 0;
                box.SelectionColor = GetColorFromAnsi(colorCode);

                cursor = match.Index + match.Length;
                }
            box.AppendText(text.Substring(cursor));
            }

        private Color GetColorFromAnsi(int code)
            {
            switch (code)
                {
                case 30: return Color.Gray;
                case 31: return Color.Salmon; // Red
                case 32: return Color.LightGreen; // Green
                case 33: return Color.Gold; // Yellow
                case 34: return Color.CornflowerBlue; // Blue
                case 35: return Color.Violet; // Magenta
                case 36: return Color.Cyan;
                case 37: return Color.White;
                case 90: return Color.DimGray;
                case 91: return Color.Red;
                case 92: return Color.Lime;
                case 93: return Color.Yellow;
                case 0: return Color.Gainsboro;
                default: return Color.Gainsboro;
                }
            }

        // --- BUTTONS ---
        private void btnUpdate_Click(object sender, EventArgs e) { _currentParser = new JatlasUpdateParser(); RunProcess("update.bat", ""); }
        private void btnCommon_Click(object sender, EventArgs e) { _currentParser = new JatlasTestParser((idx, msg) => UpdateLastGridRow(msg)); RunProcess("run-jatlas-auto.bat", "-l common --stage dev"); }
        private void btnSpecial_Click(object sender, EventArgs e) { _currentParser = new JatlasTestParser((idx, msg) => UpdateLastGridRow(msg)); RunProcess("run-jatlas-auto.bat", "-l special --stage dev"); }
        private void btnAging_Click(object sender, EventArgs e) { _currentParser = new JatlasAgingParser(); RunProcess("run-jatlas-auto.bat", "-l aging --stage dev"); }
        private void btnCommonOffline_Click(object sender, EventArgs e) { _currentParser = new JatlasTestParser((idx, msg) => UpdateLastGridRow(msg)); RunProcess("run-jatlas-auto.bat", "-l common --offline"); }
        private void taskKillBtn_Click(object sender, EventArgs e) => KillProcess();

        private void ToggleButtons(bool state)
            {
            foreach (Control c in this.Controls) RecurseEnable(c, state);
            if (taskKillBtn != null) taskKillBtn.Enabled = true;
            }
        private void RecurseEnable(Control c, bool state)
            {
            if (c is Button && !c.Name.Contains("Kill")) c.Enabled = state;
            if (c.HasChildren) foreach (Control child in c.Controls) RecurseEnable(child, state);
            }

        public void KillProcess()
            {
            if (_processId != -1)
                {
                try { Process.Start(new ProcessStartInfo("taskkill", $"/F /T /PID {_processId}") { CreateNoWindow = true, UseShellExecute = false }); }
                catch (Exception ex) { Log.Error(ex, "Kill failed"); }
                finally { _processId = -1; ToggleButtons(true); if (statusLabel != null) statusLabel.Text = "Terminated"; }
                }
            }

        protected override void OnFormClosing(FormClosingEventArgs e) { Log.CloseAndFlush(); base.OnFormClosing(e); }
        }
    }