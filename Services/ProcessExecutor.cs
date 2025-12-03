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

            // Настройка окружения
            var env = psi.EnvironmentVariables;
            env["JATLAS_LOG_LEVEL"] = "INFO";
            env["PYTHONUNBUFFERED"] = "1";
            env["FORCE_COLOR"] = "1";
            env["CLICOLOR_FORCE"] = "1";

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            _process.Exited += (s, e) => OnExited?.Invoke();

            _process.Start();
            Log.Information($"Started process PID: {_process.Id}");

            // Асинхронное чтение без блокировки
            Task.Run(() => ReadStreamAsync(_process.StandardOutput));
            Task.Run(() => ReadStreamAsync(_process.StandardError));
            }

        public void SendInput(string input)
            {
            try
                {
                _process?.StandardInput.WriteLine(input);
                }
            catch (Exception ex)
                {
                Log.Warning(ex, "Failed to send input to process");
                }
            }

        public void Kill()
            {
            if (_process == null || _process.HasExited) return;

            try
                {
                // Убиваем всё дерево процессов
                Process.Start(new ProcessStartInfo("taskkill", $"/F /T /PID {_process.Id}")
                    { CreateNoWindow = true, UseShellExecute = false });
                }
            catch (Exception ex)
                {
                Log.Error(ex, "Failed to kill process tree");
                }
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

                    int newlineIndex;
                    while (( newlineIndex = content.IndexOf('\n') ) >= 0)
                        {
                        string line = content.Substring(0, newlineIndex).TrimEnd('\r');
                        OnOutputReceived?.Invoke(line);
                        content = content.Substring(newlineIndex + 1);
                        }

                    lineBuffer.Clear();
                    lineBuffer.Append(content);

                    // Обработка "висящих" промптов (например pause)
                    if (content.Contains("Press any key to continue"))
                        {
                        OnOutputReceived?.Invoke(content.Trim());
                        lineBuffer.Clear();
                        }
                    }
                }
            catch (Exception ex)
                {
                Log.Debug($"Stream read ended: {ex.Message}");
                }
            }
        }
    }