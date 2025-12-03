using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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

        // Связь: Строка Группы -> Список её дочерних строк
        private Dictionary<DataGridViewRow, List<DataGridViewRow>> _groupChildren = new Dictionary<DataGridViewRow, List<DataGridViewRow>>();
        // Кэш для быстрого поиска строки группы по имени
        private Dictionary<string, DataGridViewRow> _groupRowsCache = new Dictionary<string, DataGridViewRow>();

        public JatlasTestRunnerForm()
            {
            InitializeComponent();
            SetupGridColumns();
            SetupLogging();
            ApplyModernStyle();

            // Подписка на клик для сворачивания/разворачивания
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
            dgvResults.EnableHeadersVisualStyles = false;
            dgvResults.ColumnHeadersDefaultCellStyle.BackColor = Color.White;
            dgvResults.ColumnHeadersDefaultCellStyle.ForeColor = Color.Gray;
            dgvResults.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgvResults.ColumnHeadersHeight = 40;
            dgvResults.RowTemplate.Height = 32;
            dgvResults.DefaultCellStyle.SelectionBackColor = Color.FromArgb(240, 245, 255);
            dgvResults.DefaultCellStyle.SelectionForeColor = Color.Black;
            // Разрешаем кликать по ячейкам без входа в режим редактирования
            dgvResults.ReadOnly = true;

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

        // --- ЛОГИКА ГРУППИРОВКИ И ВЛОЖЕННОСТИ ---

        private void DgvResults_CellClick(object sender, DataGridViewCellEventArgs e)
            {
            if (e.RowIndex < 0) return; // Клик по заголовку

            var clickedRow = dgvResults.Rows[e.RowIndex];

            // Если кликнули по строке группы - переключаем видимость детей
            if (_groupChildren.ContainsKey(clickedRow))
                {
                ToggleGroup(clickedRow);
                }
            }

        private void ToggleGroup(DataGridViewRow groupRow, bool? forceState = null)
            {
            if (!_groupChildren.ContainsKey(groupRow)) return;

            var children = _groupChildren[groupRow];
            if (children.Count == 0) return;

            // Определяем текущее состояние (развернуто если первый ребенок видим)
            bool isExpanded = children[0].Visible;
            bool newState = forceState.HasValue ? forceState.Value : !isExpanded;

            // Если состояние не меняется, выходим
            if (isExpanded == newState) return;

            // Обновляем иконку
            string statusText = groupRow.Cells[0].Value.ToString();
            // Убираем старую стрелку
            statusText = statusText.Replace("▶ ", "").Replace("▼ ", "");
            // Ставим новую
            groupRow.Cells[0].Value = ( newState ? "▼ " : "▶ " ) + statusText;

            // Скрываем/Показываем детей
            foreach (var child in children)
                {
                child.Visible = newState;
                }
            }

        private void ProcessTestResult(string status, string message)
            {
            var (groupName, ufn) = _mapper.IdentifyCheck(message);

            // Если маппинга нет — просто добавляем строку в конец
            if (string.IsNullOrEmpty(groupName))
                {
                AddRowToEnd(status, message);
                return;
                }

            // Создаем или получаем группу
            if (!_groupRowsCache.ContainsKey(groupName))
                {
                int idx = dgvResults.Rows.Add($"▶ PASS", $"{groupName}: OK");
                var newGroupRow = dgvResults.Rows[idx];

                newGroupRow.DefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                newGroupRow.DefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245); // Легкий серый фон для групп
                SetRowColor(newGroupRow, "PASS", isGroup: true);

                _groupRowsCache[groupName] = newGroupRow;
                _groupChildren[newGroupRow] = new List<DataGridViewRow>();
                }

            var groupRow = _groupRowsCache[groupName];

            // Если FAIL — обновляем статус группы и АВТОМАТИЧЕСКИ РАЗВОРАЧИВАЕМ
            if (status == "FAIL")
                {
                // Если группа была PASS, меняем на FAIL
                if (!groupRow.Cells[0].Value.ToString().Contains("FAIL"))
                    {
                    groupRow.Cells[0].Value = "▼ FAIL"; // Сразу ставим "развернуто"
                    groupRow.Cells[1].Value = $"{groupName}: Failed";
                    SetRowColor(groupRow, "FAIL", isGroup: true);
                    }

                // Принудительно разворачиваем, чтобы показать ошибку
                ToggleGroup(groupRow, forceState: true);
                }

            // Добавляем дочернюю строку
            // Вставляем физически в таблицу сразу после последнего известного ребенка этой группы
            // Или после самой группы, если детей еще нет.

            var children = _groupChildren[groupRow];
            int insertIndex = groupRow.Index + children.Count + 1;

            // Защита от выхода за границы (хотя Rows.Add/Insert это обрабатывают, но на всякий случай)
            if (insertIndex > dgvResults.Rows.Count) insertIndex = dgvResults.Rows.Count;

            dgvResults.Rows.Insert(insertIndex, status, ufn ?? message);
            var childRow = dgvResults.Rows[insertIndex];

            // Настройка стиля ребенка
            childRow.Cells[1].Style.Padding = new Padding(25, 0, 0, 0); // Отступ
            childRow.Cells[1].Style.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            if (status == "FAIL") childRow.Cells[0].Style.ForeColor = Color.DarkRed;

            // Добавляем в список детей
            children.Add(childRow);

            // По умолчанию СКРЫВАЕМ новую строку, если группа свернута
            // Проверяем текущее состояние группы по иконке
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
            // Обновляем последнюю видимую строку (упрощенно)
            if (dgvResults.Rows.Count > 0)
                dgvResults.Rows[dgvResults.Rows.Count - 1].Cells[1].Value = newMessage;
            }

        // --- ЗАПУСК И ПАРСИНГ (Без изменений логики) ---

        private void RunProcess(string batchFile, string arguments)
            {
            ToggleButtons(false);
            dgvResults.Rows.Clear();
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

            if (psi.EnvironmentVariables.ContainsKey("JATLAS_LOG_LEVEL")) psi.EnvironmentVariables["JATLAS_LOG_LEVEL"] = "INFO";
            else psi.EnvironmentVariables.Add("JATLAS_LOG_LEVEL", "INFO");
            if (!psi.EnvironmentVariables.ContainsKey("PYTHONUNBUFFERED")) psi.EnvironmentVariables.Add("PYTHONUNBUFFERED", "1");

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
                    int newlineIndex;
                    while (( newlineIndex = content.IndexOf('\n') ) >= 0)
                        {
                        ParseAndDisplayOutput(content.Substring(0, newlineIndex).Trim());
                        content = content.Substring(newlineIndex + 1);
                        }
                    lineBuffer.Clear();
                    lineBuffer.Append(content);
                    if (content.Contains("Press any key to continue")) { ParseAndDisplayOutput(content.Trim()); lineBuffer.Clear(); }
                    }
                }
            catch (Exception ex) { Log.Debug($"Stream read error: {ex.Message}"); }
            }

        private void ParseAndDisplayOutput(string line)
            {
            if (string.IsNullOrWhiteSpace(line)) return;
            Debug.WriteLine($"[RAW] {line}");

            if (line.Contains("Press any key"))
                {
                this.Invoke((Action)( () => { if (statusLabel != null) statusLabel.Text = "Finalizing..."; } ));
                try { _currentProcess.StandardInput.WriteLine(); } catch { }
                return;
                }

            _currentParser?.ParseLine(line,
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

        // --- BUTTONS ---
        private void btnUpdate_Click(object sender, EventArgs e) { _currentParser = new JatlasUpdateParser(); RunProcess("update.bat", ""); }
        private void btnCommon_Click(object sender, EventArgs e) { _currentParser = new JatlasTestParser((idx, msg) => UpdateLastGridRow(msg)); RunProcess("run-jatlas-auto.bat", "-l common --stage dev"); }
        private void btnSpecial_Click(object sender, EventArgs e) { _currentParser = new JatlasTestParser((idx, msg) => UpdateLastGridRow(msg)); RunProcess("run-jatlas-auto.bat", "-l special --stage dev"); }
        private void btnCommonOffline_Click(object sender, EventArgs e) { _currentParser = new JatlasTestParser((idx, msg) => UpdateLastGridRow(msg)); RunProcess("run-jatlas-auto.bat", "-l common --offline"); }
        private void taskKillBtn_Click(object sender, EventArgs e) => KillProcess();

        private void ToggleButtons(bool state)
            {
            if (btnUpdate != null) btnUpdate.Enabled = state;
            if (btnCommon != null) btnCommon.Enabled = state;
            if (btnSpecial != null) btnSpecial.Enabled = state;
            if (btnCommonOffline != null) btnCommonOffline.Enabled = state;
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