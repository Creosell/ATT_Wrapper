using System;
using System.Collections.Generic;
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
        private readonly List<string> _renderErrors = new List<string>();
        private static readonly Regex RenderExRegex = new Regex(
        @"RenderException\((.*?)\)\s+tpl:\s+(.*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public JatlasTestParser(Action<int, string> updateRowCallback)
            {
            _updateRowCallback = updateRowCallback;
            }

        public bool ParseLine(string line, Action<string, string> onResult, Action<string> onProgress)
            {

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
                    string desc = msg.Contains(".json") ? "Nextcloud: json report uploaded" :
                                  msg.Contains(".html") ? "Nextcloud: html report uploaded" :
                                  msg.Contains("Upload failed")? "Nextcloud: upload failed" :
                                  "Nextcloud: upload successful";


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
            //if (line.Contains("Running with administrative")) { onResult?.Invoke("PASS", "Admin privileges"); return true; }
            if (line.Contains("No internet")) { onProgress?.Invoke("Waiting for internet..."); return false; }
            if (line.Contains("Internet connection detected")) { onResult?.Invoke("PASS", "Internet connected"); return true; }
            onProgress?.Invoke("Updating...");

            if (line.Contains("Resetting branch")) { onProgress?.Invoke("Git: Pulling..."); return false; }
            if (line.Contains("Successfully pulled")) { onResult?.Invoke("PASS", "Repository updated"); return true; }
            if (line.Contains("Failed to pull")) { onResult?.Invoke("FAIL", "Git: Pull failed"); return true; }
            if (line.Contains("Already up to date")) { onResult?.Invoke("PASS", "Repository has no updates"); return true; }

            if (line.Contains("Installing dependencies")) { onProgress?.Invoke("Installing dependencies..."); return false; }
            if (line.Contains("Installing the current project")) { onResult?.Invoke("PASS", "Dependencies installed"); return true; }
            if (line.Contains("Update finished")) { onResult?.Invoke("PASS", "Update finished"); return true; }

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