using System;
using System.Text.RegularExpressions;

namespace ATT_Wrapper
    {
    public interface ILogParser
        {
        bool ParseLine(string line, Action<string, string> onResult, Action<string> onProgress);
        }

    // 1. STANDARD TEST PARSER
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
            // Task Progress: Ловим все строки Running task
            var taskMatch = Regex.Match(line, @"Running task:\s+(.*)", RegexOptions.IgnoreCase);
            if (taskMatch.Success)
                {
                string content = taskMatch.Groups[1].Value.Trim();
                // Отрезаем таймер в конце (например " 0:00:23")
                string cleanName = Regex.Replace(content, @"\s+\d+:\d+:\d+$", "");

                onProgress?.Invoke($"Running: {cleanName}");
                return false;
                }

            // Report created
            var reportMatch = Regex.Match(line, @"<Report:([^>]+)>\s+created", RegexOptions.IgnoreCase);
            if (reportMatch.Success)
                {
                onResult?.Invoke("PASS", $"{reportMatch.Groups[1].Value} report created");
                onProgress?.Invoke("Uploading reports...");
                return true;
                }

            // Uploaders
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
                    if (msg.Contains(".json")) desc = "Nextcloud: json report";
                    else if (msg.Contains(".html")) desc = "Nextcloud: html report";

                    onResult?.Invoke(status, desc);

                    if (!msg.Contains(".json") && !msg.Contains(".html"))
                        _lastUploaderRowIndex = -2;
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

            // NextCloud multiline fix
            if (_lastUploaderRowIndex == -2)
                {
                if (line.Contains(".json"))
                    {
                    _updateRowCallback?.Invoke(-1, "Nextcloud: json report");
                    _lastUploaderRowIndex = -1;
                    return true;
                    }
                if (line.Contains(".html"))
                    {
                    _updateRowCallback?.Invoke(-1, "Nextcloud: html report");
                    _lastUploaderRowIndex = -1;
                    return true;
                    }
                if (line.Trim().Length > 0 && !line.StartsWith(" ")) _lastUploaderRowIndex = -1;
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

    // 2. UPDATER PARSER
    public class JatlasUpdateParser : ILogParser
        {
        public bool ParseLine(string line, Action<string, string> onResult, Action<string> onProgress)
            {
            if (line.Contains("Running with administrative")) { onResult?.Invoke("PASS", "Admin privileges"); return true; }
            if (line.Contains("No internet")) { onProgress?.Invoke("Waiting for internet..."); return false; }
            if (line.Contains("Internet connection detected")) { onResult?.Invoke("PASS", "Internet connected"); return true; }

            if (line.Contains("Resetting branch")) { onProgress?.Invoke("Git: Pulling..."); return false; }
            if (line.Contains("Successfully pulled")) { onResult?.Invoke("PASS", "Git: Updated"); return true; }
            if (line.Contains("Failed to pull")) { onResult?.Invoke("FAIL", "Git: Pull failed"); return true; }
            if (line.Contains("Already up to date")) { onResult?.Invoke("PASS", "Git: No updates"); return true; }

            if (line.Contains("Installing dependencies")) { onProgress?.Invoke("Poetry: Installing..."); return false; }
            if (line.Contains("Installing the current project")) { onResult?.Invoke("PASS", "Poetry: Installed"); return true; }

            return false;
            }
        }

    // 3. AGING PARSER
    public class JatlasAgingParser : ILogParser
        {
        public bool ParseLine(string line, Action<string, string> onResult, Action<string> onProgress)
            {
            var resultMatch = Regex.Match(line, @"^\s*(PASS|FAIL|ERROR)\s+(.*)", RegexOptions.IgnoreCase);
            if (resultMatch.Success)
                {
                onResult?.Invoke(resultMatch.Groups[1].Value.ToUpper(), resultMatch.Groups[2].Value.Trim());
                return true;
                }
            if (line.Contains("Cycle:"))
                {
                onProgress?.Invoke(line.Trim());
                return false;
                }
            return false;
            }
        }
    }