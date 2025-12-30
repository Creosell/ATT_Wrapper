namespace ATT_Wrapper.Models
    {
    public enum LogLevel
        {
        Pass,
        Fail,
        Error,
        Progress
        }

    public class LogResult
        {
        public LogLevel Level { get; private set; }
        public string Message { get; private set; }
        public string GroupKey { get; private set; }  // null for ungrouped results

        public LogResult(LogLevel level, string message, string groupKey = null)
            {
            Level = level;
            Message = message;
            GroupKey = groupKey;
            }

        public static LogResult Pass(string message, string groupKey = null)
            {
            return new LogResult(LogLevel.Pass, message, groupKey);
            }

        public static LogResult Fail(string message, string groupKey = null)
            {
            return new LogResult(LogLevel.Fail, message, groupKey);
            }

        public static LogResult Error(string message, string groupKey = null)
            {
            return new LogResult(LogLevel.Error, message, groupKey);
            }

        public static LogResult Progress(string message)
            {
            return new LogResult(LogLevel.Progress, message);
            }
        }
    }
