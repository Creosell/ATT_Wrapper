using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace ATT_Wrapper.Services
    {
    public class ProcessExecutor
        {
        public event Action<string> OnOutputReceived;
        public event Action OnExited;
        public int CurrentPid => _process?.Id ?? -1;

        private Process _process;

        public void Start(string scriptPath, string arguments)
            {
            if (_process != null && !_process.HasExited)
                throw new InvalidOperationException("Process is already running.");

            var psi = new ProcessStartInfo("cmd.exe", $"/c \"{scriptPath}\" {arguments}")
                {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
                };

            // Настройка окружения для Python/Rich/Loguru
            var env = psi.EnvironmentVariables;
            env["JATLAS_LOG_LEVEL"] = "INFO";
            env["PYTHONUNBUFFERED"] = "1";

            // Rich color settings
            env["FORCE_COLOR"] = "true";     // Принудительные цвета для rich
            env["CLICOLOR_FORCE"] = "1";     // Стандарт BSD/Linux
            env["PYTHONIOENCODING"] = "utf-8";
            env["TERM"] = "xterm";           // Используем простой xterm, чтобы коды были стандартными (30-37)

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.Exited += (s, e) => OnExited?.Invoke();

            _process.Start();
            Log.Information($"Started process PID: {_process.Id}");

            Task.Run(() => ReadStreamAsync(_process.StandardOutput));
            Task.Run(() => ReadStreamAsync(_process.StandardError));
            }

        public void SendInput(string input)
            {
            try { _process?.StandardInput.WriteLine(input); }
            catch (Exception ex) { Log.Warning(ex, "Failed to send input"); }
            }

        public void Kill()
            {
            if (_process == null || _process.HasExited) return;
            try
                {
                Process.Start(new ProcessStartInfo("taskkill", $"/F /T /PID {_process.Id}")
                    { CreateNoWindow = true, UseShellExecute = false });
                }
            catch (Exception ex) { Log.Error(ex, "Kill failed"); }
            }

        private async Task ReadStreamAsync(StreamReader reader)
            {
            char[] buffer = new char[1024];
            StringBuilder lineBuffer = new StringBuilder();

            try
                {
                while (_process != null && !_process.HasExited)
                    {
                    int bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    lineBuffer.Append(buffer, 0, bytesRead);
                    string content = lineBuffer.ToString();

                    // Разделение по \n и \r для правильной обработки прогресса
                    int splitIndex;
                    char[] separators = { '\n', '\r' };

                    while (( splitIndex = content.IndexOfAny(separators) ) >= 0)
                        {
                        string line = content.Substring(0, splitIndex).Trim();
                        // Фильтруем пустые строки, если нужно, или передаем как есть
                        if (!string.IsNullOrEmpty(line)) OnOutputReceived?.Invoke(line);

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

                    if (content.Contains("Press any key to continue"))
                        {
                        OnOutputReceived?.Invoke(content.Trim());
                        lineBuffer.Clear();
                        }
                    }
                }
            catch (Exception ex) { Log.Debug($"Stream read ended: {ex.Message}"); }
            }
        }
    }