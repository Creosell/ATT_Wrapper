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
        // Путь к папке со скриптами
        private const string SCRIPT_PATH = @"C:\jatlas\scripts\win_scripts\";
        private Process _currentProcess;

        public JatlasTestRunnerForm()
            {
            InitializeComponent();
            SetupGridColumns(); // Настраиваем колонки кодом, чтобы не делать это руками
            }

        private void SetupGridColumns()
            {
            // Очищаем, если в дизайнере что-то добавили случайно
            dgvResults.Columns.Clear();

            dgvResults.Columns.Add("Status", "Status");
            dgvResults.Columns.Add("Component", "Component");
            dgvResults.Columns.Add("Details", "Details");

            dgvResults.Columns[0].FillWeight = 10;
            dgvResults.Columns[1].FillWeight = 40;
            dgvResults.Columns[2].FillWeight = 50;
            dgvResults.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            }

        // --- Обработчики нажатий кнопок (События из Дизайнера) ---

        private void btnUpdate_Click(object sender, EventArgs e)
            {
            RunProcess("update.bat", "");
            }

        private void btnCommon_Click(object sender, EventArgs e)
            {
            RunProcess("run-jatlas-auto.bat", "-l common");
            }

        private void btnSpecial_Click(object sender, EventArgs e)
            {
            RunProcess("run-jatlas-auto.bat", "-l special");
            }

        private void btnCommonOffline_Click(object sender, EventArgs e)
            {
            RunProcess("run-jatlas-auto.bat", "-l common --offline");
            }

        // --- Логика запуска и парсинга ---

        private void RunProcess(string batchFile, string arguments)
            {
            ToggleButtons(false);
            dgvResults.Rows.Clear();

            string fullPath = Path.Combine(SCRIPT_PATH, batchFile);

            var processInfo = new ProcessStartInfo("cmd.exe", $"/c \"{fullPath}\" {arguments}")
                {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
                };

            try
                {
                _currentProcess = new Process { StartInfo = processInfo, EnableRaisingEvents = true };

                _currentProcess.OutputDataReceived += (s, e) => ParseAndDisplayOutput(e.Data);
                _currentProcess.ErrorDataReceived += (s, e) => ParseAndDisplayOutput(e.Data);

                _currentProcess.Exited += (s, e) => this.Invoke((Action)( () => {
                    ToggleButtons(true);
                    MessageBox.Show("Выполнение завершено.", "JATLAS", MessageBoxButtons.OK, MessageBoxIcon.Information);
                } ));

                _currentProcess.Start();
                _currentProcess.BeginOutputReadLine();
                _currentProcess.BeginErrorReadLine();
                }
            catch (Exception ex)
                {
                MessageBox.Show($"Ошибка запуска: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ToggleButtons(true);
                }
            }

        private void ParseAndDisplayOutput(string line)
            {
            if (string.IsNullOrWhiteSpace(line)) return;

            // Ищем строки вида: PASS Description: Value
            var match = Regex.Match(line, @"^\s*(PASS|FAIL|ERROR)\s+(.*)", RegexOptions.IgnoreCase);

            if (match.Success)
                {
                string status = match.Groups[1].Value.ToUpper();
                string fullMessage = match.Groups[2].Value;
                string component = fullMessage;
                string details = "";

                // Пытаемся разделить "Компонент: Детали"
                int colonIndex = fullMessage.IndexOf(':');
                if (colonIndex > 0)
                    {
                    component = fullMessage.Substring(0, colonIndex).Trim();
                    details = fullMessage.Substring(colonIndex + 1).Trim();
                    }

                // Используем Invoke, так как процесс работает в другом потоке
                this.Invoke((Action)( () => AddGridRow(status, component, details) ));
                }
            // Если нужно выводить служебную инфу (например, для Update), раскомментируйте:
            // else { this.Invoke((Action)(() => AddGridRow("INFO", "System", line))); }
            }

        private void AddGridRow(string status, string component, string details)
            {
            int rowIndex = dgvResults.Rows.Add(status, component, details);
            var row = dgvResults.Rows[rowIndex];

            switch (status)
                {
                case "PASS":
                    row.DefaultCellStyle.BackColor = Color.FromArgb(220, 255, 220); // Зеленый
                    break;
                case "FAIL":
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 220, 220); // Красный
                    break;
                case "ERROR":
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 220); // Желтый
                    break;
                default:
                    row.DefaultCellStyle.BackColor = Color.WhiteSmoke;
                    break;
                }

            // Автопрокрутка вниз
            dgvResults.FirstDisplayedScrollingRowIndex = rowIndex;
            }

        private void ToggleButtons(bool state)
            {
            btnUpdate.Enabled = state;
            btnCommon.Enabled = state;
            btnSpecial.Enabled = state;
            btnCommonOffline.Enabled = state;
            }

        }
    }