using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace ATT_Wrapper.Services
    {
    /// <summary>
    /// Основной класс для управления headless-терминалом.
    /// Объединяет логику HeadlessRunner, ProcessFactory и управления пайпами.
    /// </summary>
    public sealed class ProcessExecutor : IDisposable
        {
        private PseudoConsolePipe _inputPipe;
        private PseudoConsolePipe _outputPipe;
        private PseudoConsole _pseudoConsole;
        private Process _process;
        private StreamWriter _inputWriter;
        private Task _outputTask;
        private Task _exitWaitTask;
        private CancellationTokenSource _cancellationTokenSource;

        // События
        public event Action<string> OnOutputReceived;
        public event EventHandler OnExited;

        public ProcessExecutor()
            {
            _cancellationTokenSource = new CancellationTokenSource();
            }

        /// <summary>
        /// Запускает указанную команду в псевдоконсоли.
        /// </summary>
        public void Start(string command)
            {
            if (_process != null)
                throw new InvalidOperationException("Process is already running.");

            // 1. Создаем пайпы
            _inputPipe = new PseudoConsolePipe();
            _outputPipe = new PseudoConsolePipe();

            // 2. Создаем псевдоконсоль (размер 80x25, как в оригинальном HeadlessRunner)
            _pseudoConsole = PseudoConsole.Create(_inputPipe.ReadSide, _outputPipe.WriteSide, 80, 25);

            // 3. Запускаем процесс
            _process = ProcessFactory.Start(command, PseudoConsole.PseudoConsoleThreadAttribute, _pseudoConsole.Handle);

            // 4. Настраиваем отправку ввода (SendInput)
            // Создаем StreamWriter для записи в _inputPipe.WriteSide
            var fsInput = new FileStream(_inputPipe.WriteSide, FileAccess.Write);
            _inputWriter = new StreamWriter(fsInput, Encoding.UTF8) { AutoFlush = true };

            // 5. Запускаем чтение вывода (OnOutputReceived)
            _outputTask = Task.Run(() => ReadOutputLoop(_outputPipe.ReadSide, _cancellationTokenSource.Token));

            // 6. Запускаем ожидание завершения процесса (OnExited)
            _exitWaitTask = Task.Run(() => WaitForExitLoop(_process));
            }

        /// <summary>
        /// Отправляет текст в стандартный ввод терминала.
        /// </summary>
        public void SendInput(string input)
            {
            if (_inputWriter == null) return;
            try
                {
                _inputWriter.Write(input);
                }
            catch (Exception ex)
                {
                // Игнорируем ошибки записи, если процесс уже умер
                System.Diagnostics.Debug.WriteLine($"[SendInput Error] {ex.Message}");
                }
            }

        /// <summary>
        /// Принудительно завершает процесс.
        /// </summary>
        public void Kill()
            {
            // В оригинальном API не было TerminateProcess, но закрытие псевдоконсоли (Dispose)
            // убивает прикрепленный к ней процесс.
            Dispose();
            }

        private void ReadOutputLoop(SafeFileHandle outputReadSide, CancellationToken token)
            {
            using (var fs = new FileStream(outputReadSide, FileAccess.Read))
            using (var reader = new StreamReader(fs, Encoding.UTF8))
                {
                try
                    {
                    char[] buffer = new char[1024];
                    while (!token.IsCancellationRequested)
                        {
                        // Читаем синхронно
                        int bytesRead = reader.Read(buffer, 0, buffer.Length);

                        // Если 0 - значит пайп закрыт
                        if (bytesRead == 0) break;

                        string data = new string(buffer, 0, bytesRead);
                        OnOutputReceived?.Invoke(data);
                        }
                    }
                catch (IOException) { /* Pipe broken */ }
                catch (ObjectDisposedException) { /* Stream closed */ }
                catch (Exception ex)
                    {
                    System.Diagnostics.Debug.WriteLine($"[ReadOutput Error] {ex.Message}");
                    }
                }
            }

        private void WaitForExitLoop(Process process)
            {
            // Ждем завершения процесса
            using (var waitHandle = new AutoResetEvent(false)
                {
                SafeWaitHandle = new SafeWaitHandle(process.ProcessInfo.hProcess, ownsHandle: false)
                })
                {
                waitHandle.WaitOne();
                }

            OnExited?.Invoke(this, EventArgs.Empty);

            // После завершения процесса очищаем ресурсы (но не полностью Dispose, чтобы можно было прочитать остатки логов)
            }

        public void Dispose()
            {
            _cancellationTokenSource?.Cancel();

            // Закрываем writer ввода
            _inputWriter?.Dispose();
            _inputWriter = null;

            // Освобождаем процесс (это закроет хендлы процесса)
            _process?.Dispose();
            _process = null;

            // Освобождаем псевдоконсоль (это убьет ConHost и cmd.exe, если они еще живы)
            _pseudoConsole?.Dispose();
            _pseudoConsole = null;

            // Закрываем пайпы
            _inputPipe?.Dispose();
            _inputPipe = null;

            _outputPipe?.Dispose();
            _outputPipe = null;
            }
        }

    // ==================================================================================
    // НИЖЕ РАСПОЛОЖЕНЫ ВСЕ ВСПОМОГАТЕЛЬНЫЕ КЛАССЫ ИЗ ВАШИХ ФАЙЛОВ (БЕЗ ИЗМЕНЕНИЙ ЛОГИКИ)
    // ==================================================================================

    /// <summary>
    /// Utility functions around the new Pseudo Console APIs
    /// </summary>
    internal sealed class PseudoConsole : IDisposable
        {
        public static readonly IntPtr PseudoConsoleThreadAttribute = (IntPtr)Native.PseudoConsoleApi.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE;

        public IntPtr Handle { get; }

        private PseudoConsole(IntPtr handle)
            {
            this.Handle = handle;
            }

        internal static PseudoConsole Create(SafeFileHandle inputReadSide, SafeFileHandle outputWriteSide, int width, int height)
            {
            var createResult = Native.PseudoConsoleApi.CreatePseudoConsole(
                new Native.PseudoConsoleApi.COORD { X = (short)width, Y = (short)height },
                inputReadSide, outputWriteSide,
                0, out IntPtr hPC);
            if (createResult != 0)
                {
                throw new InvalidOperationException("Could not create pseudo console. Error Code " + createResult);
                }
            return new PseudoConsole(hPC);
            }

        public void Dispose()
            {
            Native.PseudoConsoleApi.ClosePseudoConsole(Handle);
            }
        }

    internal sealed class PseudoConsolePipe : IDisposable
        {
        public readonly SafeFileHandle ReadSide;
        public readonly SafeFileHandle WriteSide;

        public PseudoConsolePipe()
            {
            if (!Native.PseudoConsoleApi.CreatePipe(out ReadSide, out WriteSide, IntPtr.Zero, 0))
                {
                throw new InvalidOperationException("failed to create pipe");
                }
            }

        void Dispose(bool disposing)
            {
            if (disposing)
                {
                ReadSide?.Dispose();
                WriteSide?.Dispose();
                }
            }

        public void Dispose()
            {
            Dispose(true);
            GC.SuppressFinalize(this);
            }
        }

    internal sealed class Process : IDisposable
        {
        public Process(Native.ProcessApi.STARTUPINFOEX startupInfo, Native.ProcessApi.PROCESS_INFORMATION processInfo)
            {
            StartupInfo = startupInfo;
            ProcessInfo = processInfo;
            }

        public Native.ProcessApi.STARTUPINFOEX StartupInfo { get; }
        public Native.ProcessApi.PROCESS_INFORMATION ProcessInfo { get; }

        private bool disposedValue = false;

        void Dispose(bool disposing)
            {
            if (!disposedValue)
                {
                if (disposing) { }

                if (StartupInfo.lpAttributeList != IntPtr.Zero)
                    {
                    Native.ProcessApi.DeleteProcThreadAttributeList(StartupInfo.lpAttributeList);
                    Marshal.FreeHGlobal(StartupInfo.lpAttributeList);
                    }

                if (ProcessInfo.hProcess != IntPtr.Zero)
                    {
                    Native.ProcessApi.CloseHandle(ProcessInfo.hProcess);
                    }
                if (ProcessInfo.hThread != IntPtr.Zero)
                    {
                    Native.ProcessApi.CloseHandle(ProcessInfo.hThread);
                    }

                disposedValue = true;
                }
            }

        ~Process()
            {
            Dispose(false);
            }

        public void Dispose()
            {
            Dispose(true);
            GC.SuppressFinalize(this);
            }
        }

    static class ProcessFactory
        {
        internal static Process Start(string command, IntPtr attributes, IntPtr hPC)
            {
            var startupInfo = ConfigureProcessThread(hPC, attributes);
            var processInfo = RunProcess(ref startupInfo, command);
            return new Process(startupInfo, processInfo);
            }

        private static Native.ProcessApi.STARTUPINFOEX ConfigureProcessThread(IntPtr hPC, IntPtr attributes)
            {
            var lpSize = IntPtr.Zero;
            var success = Native.ProcessApi.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);
            if (success || lpSize == IntPtr.Zero) throw new InvalidOperationException("Could not calculate attribute list size.");

            var startupInfo = new Native.ProcessApi.STARTUPINFOEX();
            startupInfo.StartupInfo.cb = Marshal.SizeOf<Native.ProcessApi.STARTUPINFOEX>();
            startupInfo.lpAttributeList = Marshal.AllocHGlobal(lpSize);

            startupInfo.StartupInfo.dwFlags = (int)Native.ProcessApi.STARTF_USESHOWWINDOW;
            startupInfo.StartupInfo.wShowWindow = Native.ProcessApi.SW_HIDE;

            success = Native.ProcessApi.InitializeProcThreadAttributeList(startupInfo.lpAttributeList, 1, 0, ref lpSize);
            if (!success) throw new InvalidOperationException("Could not set up attribute list.");

            success = Native.ProcessApi.UpdateProcThreadAttribute(
                startupInfo.lpAttributeList,
                0,
                attributes,
                hPC,
                (IntPtr)IntPtr.Size,
                IntPtr.Zero,
                IntPtr.Zero
            );
            if (!success) throw new InvalidOperationException("Could not set pseudoconsole thread attribute.");

            return startupInfo;
            }

        private static Native.ProcessApi.PROCESS_INFORMATION RunProcess(ref Native.ProcessApi.STARTUPINFOEX sInfoEx, string commandLine)
            {
            int securityAttributeSize = Marshal.SizeOf<Native.ProcessApi.SECURITY_ATTRIBUTES>();
            var pSec = new Native.ProcessApi.SECURITY_ATTRIBUTES { nLength = securityAttributeSize };
            var tSec = new Native.ProcessApi.SECURITY_ATTRIBUTES { nLength = securityAttributeSize };

            var success = Native.ProcessApi.CreateProcess(
                lpApplicationName: null,
                lpCommandLine: commandLine,
                lpProcessAttributes: ref pSec,
                lpThreadAttributes: ref tSec,
                bInheritHandles: false,
                dwCreationFlags: Native.ProcessApi.EXTENDED_STARTUPINFO_PRESENT,
                lpEnvironment: IntPtr.Zero,
                lpCurrentDirectory: null,
                lpStartupInfo: ref sInfoEx,
                lpProcessInformation: out Native.ProcessApi.PROCESS_INFORMATION pInfo
            );
            if (!success)
                {
                throw new InvalidOperationException("Could not create process. " + Marshal.GetLastWin32Error());
                }

            return pInfo;
            }
        }

    namespace Native
        {
        static class ConsoleApi
            {
            internal const int STD_OUTPUT_HANDLE = -11;
            internal const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
            internal const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern SafeFileHandle GetStdHandle(int nStdHandle);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern bool SetConsoleMode(SafeFileHandle hConsoleHandle, uint mode);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern bool GetConsoleMode(SafeFileHandle handle, out uint mode);

            internal delegate bool ConsoleEventDelegate(CtrlTypes ctrlType);

            internal enum CtrlTypes : uint
                {
                CTRL_C_EVENT = 0,
                CTRL_BREAK_EVENT,
                CTRL_CLOSE_EVENT,
                CTRL_LOGOFF_EVENT = 5,
                CTRL_SHUTDOWN_EVENT
                }

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);
            }

        static class PseudoConsoleApi
            {
            internal const uint PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;

            [StructLayout(LayoutKind.Sequential)]
            internal struct COORD
                {
                public short X;
                public short Y;
                }

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern int CreatePseudoConsole(COORD size, SafeFileHandle hInput, SafeFileHandle hOutput, uint dwFlags, out IntPtr phPC);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern int ClosePseudoConsole(IntPtr hPC);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            internal static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, IntPtr lpPipeAttributes, int nSize);
            }

        static class ProcessApi
            {
            internal const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
            internal const uint STARTF_USESHOWWINDOW = 0x00000001;
            internal const short SW_HIDE = 0;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            internal struct STARTUPINFOEX
                {
                public STARTUPINFO StartupInfo;
                public IntPtr lpAttributeList;
                }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            internal struct STARTUPINFO
                {
                public Int32 cb;
                public string lpReserved;
                public string lpDesktop;
                public string lpTitle;
                public Int32 dwX;
                public Int32 dwY;
                public Int32 dwXSize;
                public Int32 dwYSize;
                public Int32 dwXCountChars;
                public Int32 dwYCountChars;
                public Int32 dwFillAttribute;
                public Int32 dwFlags;
                public Int16 wShowWindow;
                public Int16 cbReserved2;
                public IntPtr lpReserved2;
                public IntPtr hStdInput;
                public IntPtr hStdOutput;
                public IntPtr hStdError;
                }

            [StructLayout(LayoutKind.Sequential)]
            internal struct PROCESS_INFORMATION
                {
                public IntPtr hProcess;
                public IntPtr hThread;
                public int dwProcessId;
                public int dwThreadId;
                }

            [StructLayout(LayoutKind.Sequential)]
            internal struct SECURITY_ATTRIBUTES
                {
                public int nLength;
                public IntPtr lpSecurityDescriptor;
                public int bInheritHandle;
                }

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool InitializeProcThreadAttributeList(
                IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool UpdateProcThreadAttribute(
                IntPtr lpAttributeList, uint dwFlags, IntPtr attribute, IntPtr lpValue,
                IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

            [DllImport("kernel32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CreateProcess(
                string lpApplicationName, string lpCommandLine, ref SECURITY_ATTRIBUTES lpProcessAttributes,
                ref SECURITY_ATTRIBUTES lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags,
                IntPtr lpEnvironment, string lpCurrentDirectory, [In] ref STARTUPINFOEX lpStartupInfo,
                out PROCESS_INFORMATION lpProcessInformation);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool DeleteProcThreadAttributeList(IntPtr lpAttributeList);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern bool CloseHandle(IntPtr hObject);
            }
        }
    }