using ATT_Wrapper.Components;
using Serilog;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ATT_Wrapper.Services
    {
    /// <summary>
    /// Сервис для проверки наличия обновлений в локальном Git-репозитории.
    /// </summary>
    public class UpdateChecker
        {
        // Путь к корню репозитория
        private const string RepoPath = @"C:\jatlas";

        /// <summary>
        /// Асинхронно проверяет наличие обновлений в удаленной ветке (upstream).
        /// </summary>
        /// <returns>
        /// LogResult.Pass("Updates found") — если есть обновления.
        /// LogResult.Pass("No updates available") — если обновлений нет.
        /// LogResult.Fail — если произошла ошибка git или парсинга.
        /// </returns>
        public async Task<LogResult> IsUpdateAvailable()
            {
            return await Task.Run(() =>
            {
                try
                    {
                    // 1. Скачиваем изменения для текущей ветки и её upstream
                    RunGitCommand("fetch");

                    // 2. Считаем разницу коммитов между HEAD и upstream (@{u})
                    string output = RunGitCommand("rev-list --count HEAD..@{u}");

                    if (int.TryParse(output, out int commitsBehind))
                        {
                        if (commitsBehind > 0)
                            {
                            return LogResult.Pass("Updates found");
                            }
                        return LogResult.Pass("No updates available");
                        }
                    else
                        {
                        Log.Warning($"Git returned unexpected output: {output}");
                        return LogResult.Fail("Git output parsing error");
                        }
                    }
                catch (Exception ex)
                    {
                    // Ошибки: не git-репозиторий, нет upstream, нет сети и т.д.
                    Log.Error(ex, "Update check failed");
                    return LogResult.Fail("Updates check failed");
                    }
            });
            }

        /// <summary>
        /// Запускает команду git с заданными аргументами в папке репозитория.
        /// </summary>
        /// <param name="arguments">Аргументы команды (например, "fetch").</param>
        /// <returns>Стандартный вывод (stdout) команды.</returns>
        /// <exception cref="Exception">Выбрасывается при таймауте или ненулевом коде возврата.</exception>
        private string RunGitCommand(string arguments)
            {
            var processInfo = new ProcessStartInfo
                {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = RepoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
                };

            using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                if (process == null)
                    throw new Exception("Failed to start git process.");

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                // Ожидание завершения с таймаутом 10 секунд
                bool exited = process.WaitForExit(10000);

                if (!exited)
                    {
                    try { process.Kill(); } catch { /* Ignore error on kill */ }
                    throw new Exception("Git command timed out.");
                    }

                if (process.ExitCode != 0)
                    {
                    throw new Exception($"Git error (ExitCode {process.ExitCode}): {error}");
                    }

                return output.Trim();
                }
            }
        }
    }