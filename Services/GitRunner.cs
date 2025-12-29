using System;
using System.Diagnostics;

namespace ATT_Wrapper.Services
    {
    public class GitRunner : IGitRunner
        {
        private const string RepoPath = @"C:\jatlas";

        public string Run(string arguments)
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

                bool exited = process.WaitForExit(10000); // 10 сек таймаут

                if (!exited)
                    {
                    try { process.Kill(); } catch { }
                    throw new Exception("Git command timed out.");
                    }

                if (process.ExitCode != 0)
                    {
                    // Для rev-list это может быть нормальным (если нет upstream),
                    // но мы все равно кидаем исключение, которое ловит UpdateChecker
                    throw new Exception($"Git error (ExitCode {process.ExitCode}): {error}");
                    }

                return output.Trim();
                }
            }
        }
    }