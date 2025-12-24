using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ATT_Wrapper.Services
    {
    public class UpdateChecker
        {
        // Путь к корню репозитория
        private const string RepoPath = @"C:\jatlas";

        public async Task<bool> IsUpdateAvailable()
            {
            return await Task.Run(() =>
            {
                try
                    {
                    // 1. Обновляем информацию об удаленных ветках
                    // Просто "fetch" скачает изменения для текущей ветки и её upstream
                    RunGitCommand("fetch");

                    // 2. Считаем разницу между HEAD (текущее состояние) и @{u} (upstream/удаленная версия текущей ветки)
                    // Синтаксис @{u} автоматически подставит origin/master, origin/dev или любую другую привязанную ветку.
                    string output = RunGitCommand("rev-list --count HEAD..@{u}");

                    if (int.TryParse(output, out int commitsBehind))
                        {
                        return commitsBehind > 0;
                        }

                    return false;
                    }
                catch (Exception ex)
                    {
                    // Ошибки могут возникнуть, если:
                    // - Это не git репозиторий
                    // - Ветка локальная и не имеет привязки к удаленной (no upstream configured)
                    // - Detached HEAD (состояние "оторванной головы", когда мы не на ветке)
                    Console.WriteLine($"Git check failed (возможно, нет upstream ветки): {ex.Message}");
                    return false;
                    }
            });
            }

        private string RunGitCommand(string arguments)
            {
            var processInfo = new ProcessStartInfo
                {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = RepoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true, // Важно перехватывать ошибки, т.к. git часто пишет туда инфо
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
                };

            using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit(10000); // Таймаут 10 сек

                if (!process.HasExited)
                    {
                    process.Kill();
                    throw new Exception("Git command timed out.");
                    }

                if (process.ExitCode != 0)
                    {
                    // Если ExitCode не 0, значит команды @{u} скорее всего не существует (нет upstream)
                    throw new Exception($"Git error: {error}");
                    }

                return output.Trim();
                }
            }
        }
    }