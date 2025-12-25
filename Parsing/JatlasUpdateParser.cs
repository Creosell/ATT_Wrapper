using ATT_Wrapper.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATT_Wrapper.Parsing
    {
    public class JatlasUpdateParser : ILogParser
        {
        public bool ParseLine(string line, string statusFromLine,
                             Action<string, string, string> onResult,
                             Action<string> onProgress)
            {
            if (line.Contains("No internet"))
                {
                onProgress?.Invoke("Waiting for internet...");
                return false;
                }

            if (line.Contains("Internet connection detected"))
                {
                onResult?.Invoke("PASS", "Internet connected", null);
                return true;
                }

            if (line.Contains("Resetting branch"))
                {
                onProgress?.Invoke("Git: Pulling...");
                return false;
                }

            if (line.Contains("Successfully pulled"))
                {
                onResult?.Invoke("PASS", "Repository updated", null);
                return true;
                }

            if (line.Contains("Failed to pull"))
                {
                onResult?.Invoke("FAIL", "Git: Pull failed", null);
                return true;
                }

            if (line.Contains("Already up to date"))
                {
                onResult?.Invoke("PASS", "Repository has no updates", null);
                return true;
                }

            if (line.Contains("Installing dependencies"))
                {
                onProgress?.Invoke("Installing dependencies...");
                return false;
                }

            if (line.Contains("Installing the current project"))
                {
                onResult?.Invoke("PASS", "Dependencies installed", null);
                return true;
                }

            if (line.Contains("Update finished"))
                {
                onResult?.Invoke("PASS", "Update finished", null);
                return true;
                }

            return false;
            }
        }
    }
