using System.Text.RegularExpressions;

namespace ATT_Wrapper.Parsing
    {
    public static class LogPatterns
        {
        // Compile once, use everywhere
        public static readonly Regex RenderException = new Regex(
            @"RenderException\((.*?)\)\s+tpl:\s+(.*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static readonly Regex NetworkTest = new Regex(
            @"Running task:\s+(Switch network to|Test bitrate.*?network:)\s+([^\x1b]+)(?:.*?)(\d+/\d+s)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static readonly Regex RunningTask = new Regex(
            @"Running task:\s+([^\x1b]+)(?:.*?)((?:\d+:\d+(?::\d+)?)|(?:\d+/\d+s))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static readonly Regex Report = new Regex(
            @"<Report:([^>]+)>\s+created",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static readonly Regex StandardResult = new Regex(
            @"^\s*(PASS|FAIL|ERROR)\s+(.*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static readonly Regex Uploader = new Regex(
            @".*?<Uploader:([^>]+)>\s+(.*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static readonly Regex AnsiColorCode = new Regex(
            @"\x1B\[(?<code>\d+)m",
            RegexOptions.Compiled);

        // ANSI cleaning patterns
        public static readonly Regex AnsiAll = new Regex(
            @"(\x1B\[[\x30-\x3F]*[\x20-\x2F]*[\x40-\x7E]|\x1B\][^\x07\x1B]*(\x07|\x1B\\)|\x1B[PX^_].*?(\x07|\x1B\\))",
            RegexOptions.Compiled);

        public static readonly Regex LineReset = new Regex(
            @"(\r|\x1B\[\d+;\d+[Hf]|\x1B\[\d+[Hf])",
            RegexOptions.Compiled);

        public static readonly Regex PauseCleaner = new Regex(
            @"Press any key to continue( \. \. \.)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static readonly Regex WindowTitle = new Regex(
            @"\x1B\]0;.*?\x07",
            RegexOptions.Compiled);

        public static readonly Regex CursorForward = new Regex(
            @"\x1B\[(\d*)C",
            RegexOptions.Compiled);

        public static readonly Regex PositionalCodes = new Regex(
            @"\x1B\[\d+(;\d+)?[Hf]",
            RegexOptions.Compiled);

        public static readonly Regex AnsiSplit = new Regex(
            @"(\x1B\[[0-9;?]*[ -/]*[@-~])",
            RegexOptions.Compiled);

        public static readonly Regex UploaderIssue = new Regex(
                    @".*?(?:WARNING|ERROR)\s+\|\s+jatlas\.uploaders\.(\w+)_uploader.*?:.*?\s+-\s+(.*)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 2. Ловит ошибки соединения, где явно указан HOST (для urllib3/requests)
        // Ловит: host='calydonqc.com'
        public static readonly Regex ConnectionHostError = new Regex(
            @"(?:HTTPSConnectionPool|NameResolutionError|ConnectionError).*?host='([^']+)'",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 3. Ловит ошибки резолва DNS, где хост в кавычках
        // Ловит: Failed to resolve 'calydonqc.com'
        public static readonly Regex DnsResolveError = new Regex(
            @"Failed to resolve '([^']+)'",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);


        public static readonly Regex GitNetworkError = new Regex(
    @"(?:kex_exchange_identification|ssh: connect to host|Connection closed by remote|The remote end hung up unexpectedly|Software caused connection abort)",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static readonly Regex GitFatalError = new Regex(
            @"fatal:\s+(Could not read from remote repository|repository not found|Authentication failed|unable to access|The remote end hung up unexpectedly)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static readonly Regex GitLockError = new Regex(
            @"Unable to create .*?\.git/index\.lock",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static readonly Regex GitHostKeyError = new Regex(
            @"Host key verification failed",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);


        }
    }
