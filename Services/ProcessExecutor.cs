using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ATT_Wrapper.Services
    {
    public class ProcessExecutor
        {
        // Событие теперь передает сырой кусок текста, а не полную строку
        public event Action<string> OnOutputReceived;
        public event Action OnExited;
        public int CurrentPid => _process?.Id ?? -1;

        private Process _process;

        public void Start(string scriptPath, string arguments)
            {
            GeminiLogger.Log($"Executor Start: {scriptPath} {arguments}");

            if (_process != null && !_process.HasExited)
                throw new InvalidOperationException("Process is already running.");

            var psi = new ProcessStartInfo("cmd.exe", $"/c \"{scriptPath}\" {arguments}")
                {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8, // Важно для корректных символов
                StandardErrorEncoding = Encoding.UTF8
                };

            // === [FIX] Переменные для обмана Rich и Python ===
            var env = psi.EnvironmentVariables;
            env["PYTHONUNBUFFERED"] = "1"; // Мгновенный вывод (самое важное!)
            env["FORCE_COLOR"] = "1";      // Заставляет Rich слать цвета
            env["CLICOLOR_FORCE"] = "1";   // Доп. флаг для других либ
            env["JATLAS_LOG_LEVEL"] = "INFO";
            env["TERM"] = "xterm-256color";
            env["COLUMNS"] = "120";        // Ширина виртуальной консоли
            // =================================================

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.Exited += (s, e) => {
                GeminiLogger.Log($"Process {_process?.Id} exited");
                OnExited?.Invoke();
            };

            try
                {
                _process.Start();
                GeminiLogger.Log($"Started Standard Process PID: {_process.Id}");
                }
            catch (Exception ex)
                {
                GeminiLogger.Error(ex, "Failed to start standard process");
                throw;
                }

            // Читаем потоки асинхронно
            Task.Run(() => ReadStreamAsync(_process.StandardOutput, "STDOUT"));
            Task.Run(() => ReadStreamAsync(_process.StandardError, "STDERR"));
            }

        public void SendInput(string input)
            {
            try
                {
                _process?.StandardInput.WriteLine(input);
                GeminiLogger.Debug($"Sent input: {input}");
                }
            catch (Exception ex) { GeminiLogger.Error(ex, "Failed to send input"); }
            }

        public void Kill()
            {
            if (_process == null || _process.HasExited) return;
            try
                {
                GeminiLogger.Log($"Killing process PID: {_process.Id}");
                Process.Start(new ProcessStartInfo("taskkill", $"/F /T /PID {_process.Id}")
                    { CreateNoWindow = true, UseShellExecute = false });
                }
            catch (Exception ex) { GeminiLogger.Error(ex, "Kill failed"); }
            }

        private async Task ReadStreamAsync(StreamReader reader, string streamName)
            {
            // Буфер на 4Кб
            char[] buffer = new char[4096];

            try
                {
                while (_process != null && !_process.HasExited)
                    {
                    // Читаем блок данных (не ждем перевода строки!)
                    int bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string content = new string(buffer, 0, bytesRead);

                    // Логируем сырой чанк (можно отключить, если слишком много логов)
                    // GeminiLogger.LogRawData($"{streamName} Chunk", content);

                    // Передаем данные сразу же
                    OnOutputReceived?.Invoke(content);
                    }
                }
            catch (Exception ex)
                {
                GeminiLogger.Error(ex, $"Stream read error ({streamName})");
                }
            }
        }
    }