using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Serilog;
using Serilog.Core;

namespace ATT_Wrapper.Services
    {
    public static class GeminiLogger
        {
        private static Logger _debugLogger;
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "gemini_debug_full.log");

        // Инициализация: удаляет старый лог и создает новый
        public static void Initialize()
            {
            try
                {
                string directory = Path.GetDirectoryName(LogPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                if (File.Exists(LogPath))
                    File.Delete(LogPath); // Удаляем старый файл при каждом запуске

                _debugLogger = new LoggerConfiguration()
                    .MinimumLevel.Verbose() // Пишем абсолютно всё
                    .WriteTo.File(
                        LogPath,
                        outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                        fileSizeLimitBytes: null, // Без лимитов
                        shared: true
                    )
                    .CreateLogger();

                Log("=== GEMINI LOGGER INITIALIZED (NEW SESSION) ===");
                }
            catch (Exception ex)
                {
                // Если не удалось создать логгер, пишем в консоль отладчика
                System.Diagnostics.Debug.WriteLine($"Failed to init GeminiLogger: {ex.Message}");
                }
            }

        public static void Log(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "")
            {
            string fileName = Path.GetFileNameWithoutExtension(sourceFilePath);
            _debugLogger?.Information($"[{fileName}.{memberName}] {message}");
            }

        public static void Debug(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "")
            {
            string fileName = Path.GetFileNameWithoutExtension(sourceFilePath);
            _debugLogger?.Debug($"[{fileName}.{memberName}] {message}");
            }

        // Специальный метод для логирования сырых данных (с показом ANSI кодов)
        public static void LogRawData(string prefix, string rawData,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "")
            {
            string safeView = ToRawString(rawData);
            string fileName = Path.GetFileNameWithoutExtension(sourceFilePath);
            _debugLogger?.Debug($"[{fileName}.{memberName}] {prefix} -> \"{safeView}\"");
            }

        public static void Error(Exception ex, string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "")
            {
            string fileName = Path.GetFileNameWithoutExtension(sourceFilePath);
            _debugLogger?.Error(ex, $"[{fileName}.{memberName}] {message}");
            }

        public static void Close()
            {
            _debugLogger?.Dispose();
            }

        // Превращает невидимые символы в читаемые теги [ESC], [CR], [LF]
        private static string ToRawString(string input)
            {
            if (string.IsNullOrEmpty(input)) return "[EMPTY]";
            StringBuilder sb = new StringBuilder();
            foreach (char c in input)
                {
                if (c == 0x1B) sb.Append("[ESC]");
                else if (c == '\r') sb.Append("[CR]");
                else if (c == '\n') sb.Append("[LF]");
                else if (c == '\t') sb.Append("[TAB]");
                else if (char.IsControl(c)) sb.Append($"[x{(int)c:X2}]");
                else sb.Append(c);
                }
            return sb.ToString();
            }
        }
    }