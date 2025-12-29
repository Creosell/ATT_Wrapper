using ATT_Wrapper.Components;
using System.Collections.Generic;

namespace ATT_Wrapper.Parsing
    {
    public class JatlasUpdateParser : ILogParser
        {
        public IEnumerable<LogResult> Parse(string line, string statusFromLine = null)
            {
            if (line.Contains("No internet"))
                {
                yield return LogResult.Progress("Waiting for internet...");
                yield break;
                }

            if (line.Contains("Internet connection detected"))
                {
                yield return LogResult.Pass("Internet connected");
                yield break;
                }

            if (line.Contains("Resetting branch"))
                {
                yield return LogResult.Progress("Git: Pulling...");
                yield break;
                }

            if (line.Contains("Successfully pulled"))
                {
                yield return LogResult.Pass("Repository updated");
                yield break;
                }

            if (line.Contains("Failed to pull"))
                {
                yield return LogResult.Fail("Git: Pull failed");
                yield break;
                }

            if (line.Contains("Already up to date"))
                {
                yield return LogResult.Pass("Repository has no updates");
                yield break;
                }

            if (line.Contains("Installing dependencies"))
                {
                yield return LogResult.Progress("Installing dependencies...");
                yield break;
                }

            if (line.Contains("Installing the current project"))
                {
                yield return LogResult.Pass("Dependencies installed");
                yield break;
                }

            if (line.Contains("Update finished"))
                {
                yield return LogResult.Pass("Update finished");
                yield break;
                }
            }
        }
    }