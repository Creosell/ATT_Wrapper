using System;
using System.Text.RegularExpressions;

namespace ATT_Wrapper
    {
    // Interface for log parsing strategies
    public interface ILogParser
        {
        bool ParseLine(string line, Action<string, string> onResult, Action<string> onProgress);
        }

    // Parser for JATLAS test execution (Python output)
    public class JatlasTestParser : ILogParser
        {
        private int _lastUploaderRowIndex = -1;
        private readonly Action<int, string> _updateRowCallback;

        public JatlasTestParser(Action<int, string> updateRowCallback)
            {
            _updateRowCallback = updateRowCallback;
            }

        public bool ParseLine(string line, Action<string, string> onResult, Action<string> onProgress)
            {
            // Task progress
            var taskMatch = Regex.Match(line, @"Running task:\s+(.*)", RegexOptions.IgnoreCase);
            if (taskMatch.Success)
                {
                onProgress?.Invoke($"Running: {taskMatch.Groups[1].Value.Trim()}");
                return true;
                }

            // Report creation
            var reportMatch = Regex.Match(line, @"<Report:([^>]+)>\s+created", RegexOptions.IgnoreCase);
            if (reportMatch.Success)
                {
                onResult?.Invoke("PASS", $"{reportMatch.Groups[1].Value} report created");
                onProgress?.Invoke("Uploading reports...");
                return true;
                }

            // Uploaders logic
            var uploaderMatch = Regex.Match(line, @"^\s*<Uploader:([^>]+)>\s+(.*)", RegexOptions.IgnoreCase);
            if (uploaderMatch.Success)
                {
                string name = uploaderMatch.Groups[1].Value;
                string msg = uploaderMatch.Groups[2].Value;
                string status = msg.ToLower().Contains("success") ? "PASS" :
                                ( msg.ToLower().Contains("fail") ? "FAIL" : "INFO" );

                if (name.Equals("FeishuBot", StringComparison.OrdinalIgnoreCase))
                    {
                    onResult?.Invoke(status, status == "PASS" ? "FeishuBot: notification sent" : $"FeishuBot: {msg}");
                    _lastUploaderRowIndex = -1;
                    }
                else if (name.Equals("NextCloud", StringComparison.OrdinalIgnoreCase))
                    {
                    string desc = "Nextcloud: upload successful";
                    if (msg.Contains(".json")) desc = "Nextcloud: json report is uploaded";
                    else if (msg.Contains(".html")) desc = "Nextcloud: html report is uploaded";

                    // Trigger addition of row
                    onResult?.Invoke(status, desc);

                    // Mark for update if file extension is missing
                    if (!msg.Contains(".json") && !msg.Contains(".html"))
                        _lastUploaderRowIndex = -2; // Marker: waiting for next lines
                    else
                        _lastUploaderRowIndex = -1;
                    }
                else
                    {
                    onResult?.Invoke(status, $"{name}: {msg}");
                    _lastUploaderRowIndex = -1;
                    }
                return true;
                }

            // Multiline update for NextCloud
            if (_lastUploaderRowIndex == -2)
                {
                if (line.Contains(".json"))
                    {
                    _updateRowCallback?.Invoke(-1, "Nextcloud: json report is uploaded");
                    _lastUploaderRowIndex = -1;
                    return true;
                    }
                if (line.Contains(".html"))
                    {
                    _updateRowCallback?.Invoke(-1, "Nextcloud: html report is uploaded");
                    _lastUploaderRowIndex = -1;
                    return true;
                    }
                // Reset state on structure change
                if (line.StartsWith("PASS") || line.StartsWith("FAIL") || line.Contains("<Uploader:") || line.Contains("<Report:"))
                    _lastUploaderRowIndex = -1;
                }

            // Standard PASS/FAIL
            var resultMatch = Regex.Match(line, @"^\s*(PASS|FAIL|ERROR)\s+(.*)", RegexOptions.IgnoreCase);
            if (resultMatch.Success)
                {
                onResult?.Invoke(resultMatch.Groups[1].Value.ToUpper(), resultMatch.Groups[2].Value.Trim());
                return true;
                }

            return false;
            }
        }

    // Parser for Update process (Batch/Shell output)
    public class JatlasUpdateParser : ILogParser
        {
        public bool ParseLine(string line, Action<string, string> onResult, Action<string> onProgress)
            {
            // Admin check
            if (line.Contains("Running with administrative privileges"))
                {
                onResult?.Invoke("PASS", "Running with administrative privileges");
                return true;
                }

            // Internet check
            if (line.Contains("No internet connection"))
                {
                onProgress?.Invoke("Waiting for internet connection...");
                return true;
                }
            if (line.Contains("Internet connection detected"))
                {
                onResult?.Invoke("PASS", "Internet connection established");
                return true;
                }

            // Git operations
            if (line.Contains("Resetting branch"))
                {
                onProgress?.Invoke("Git: Pulling changes...");
                return true;
                }
            if (line.Contains("Successfully pulled"))
                {
                onResult?.Invoke("PASS", "Git: Repository updated successfully");
                return true;
                }
            if (line.Contains("Failed to pull"))
                {
                onResult?.Invoke("FAIL", "Git: Failed to pull latest changes");
                return true;
                }
            if (line.Contains("Already up to date"))
                {
                onResult?.Invoke("PASS", "Git: No new updates found");
                return true;
                }

            // Poetry operations
            if (line.Contains("Installing dependencies"))
                {
                onProgress?.Invoke("Poetry: Installing dependencies...");
                return true;
                }
            if (line.Contains("Installing the current project"))
                {
                onResult?.Invoke("PASS", "Poetry: Dependencies and Project installed");
                return true;
                }
            if (line.Contains("Poetry install failed"))
                {
                onResult?.Invoke("FAIL", "Poetry: Dependency installation failed");
                return true;
                }

            // Cleanup & scripts
            if (line.Contains("Removing old files"))
                {
                onResult?.Invoke("PASS", "Cleanup: Removing old files");
                return true;
                }
            if (line.Contains("Copy link"))
                {
                onResult?.Invoke("PASS", "System: Updating desktop shortcuts");
                return true;
                }
            if (line.Contains("uv ") && line.Contains("installed"))
                {
                onResult?.Invoke("PASS", "System: UV tools updated");
                return true;
                }

            return false;
            }
        }
    }