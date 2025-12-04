using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace ATT_Wrapper.Services
    {
    public class ConPtyShell : IDisposable
        {
        public event Action<string> OnOutputReceived;
        public event Action OnExited;

        private Process _process;
        private IntPtr _hPC;
        private FileStream _outPipeStream;
        private Thread _readThread;
        private bool _isRunning;

        // --- Native Import ---
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, IntPtr lpPipeAttributes, int nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int CreatePseudoConsole(COORD size, IntPtr hInput, IntPtr hOutput, uint dwFlags, out IntPtr phPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int ClosePseudoConsole(IntPtr hPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr Attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CreateProcess(
            string lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes, bool bInheritHandles, int dwCreationFlags,
            IntPtr lpEnvironment, string lpCurrentDirectory, ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [StructLayout(LayoutKind.Sequential)]
        struct COORD { public short X; public short Y; }

        // === ВАЖНО: Добавлено CharSet = CharSet.Unicode ===
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct STARTUPINFOEX
            {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
            }

        // === ВАЖНО: Добавлено CharSet = CharSet.Unicode ===
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct STARTUPINFO
            {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
            }

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_INFORMATION
            {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
            }

        private const int EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        private const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;

        private const int STARTF_USESHOWWINDOW = 0x00000001;
        private const short SW_HIDE = 0;

        public void Start(string fileName, string arguments)
            {
            if (_isRunning) throw new InvalidOperationException("Process already running");

            GeminiLogger.Log($"Preparing ConPTY for: {fileName} {arguments}");

            CreatePipe(out IntPtr hPipePTYIn, out IntPtr hPipeWrite, IntPtr.Zero, 0);
            CreatePipe(out IntPtr hPipeRead, out IntPtr hPipePTYOut, IntPtr.Zero, 0);

            COORD consoleSize = new COORD { X = 120, Y = 30 };
            int result = CreatePseudoConsole(consoleSize, hPipePTYIn, hPipePTYOut, 0, out _hPC);

            if (result != 0)
                {
                GeminiLogger.Error(null, $"Failed to create PseudoConsole. Error: {result}");
                throw new Exception("Could not create PseudoConsole.");
                }

            CloseHandle(hPipePTYIn);
            CloseHandle(hPipePTYOut);

            var startupInfoEx = new STARTUPINFOEX();
            startupInfoEx.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();

            // Скрываем окно
            startupInfoEx.StartupInfo.dwFlags = STARTF_USESHOWWINDOW;
            startupInfoEx.StartupInfo.wShowWindow = SW_HIDE;

            IntPtr lpSize = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);
            startupInfoEx.lpAttributeList = Marshal.AllocHGlobal(lpSize);
            InitializeProcThreadAttributeList(startupInfoEx.lpAttributeList, 1, 0, ref lpSize);

            IntPtr phPC = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(phPC, _hPC);

            UpdateProcThreadAttribute(startupInfoEx.lpAttributeList, 0, (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, phPC, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero);

            PROCESS_INFORMATION pInfo;
            string commandLine = $"cmd.exe /c \"{fileName}\" {arguments}";

            if (!CreateProcess(
                null,
                commandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                EXTENDED_STARTUPINFO_PRESENT,
                IntPtr.Zero,
                null,
                ref startupInfoEx,
                out pInfo))
                {
                // [FIX] Захватываем код ошибки ДО интерполяции строки
                int err = Marshal.GetLastWin32Error();
                GeminiLogger.Error(null, $"CreateProcess failed. Error Code: {err}");
                throw new Exception($"CreateProcess failed with error code: {err}");
                }

            _process = Process.GetProcessById(pInfo.dwProcessId);
            _process.EnableRaisingEvents = true;
            _process.Exited += (s, e) => {
                _isRunning = false;
                GeminiLogger.Log($"ConPTY Process {_process.Id} exited");
                OnExited?.Invoke();
            };

            GeminiLogger.Log($"Started ConPTY process: {_process.Id}");

            DeleteProcThreadAttributeList(startupInfoEx.lpAttributeList);
            Marshal.FreeHGlobal(startupInfoEx.lpAttributeList);
            Marshal.FreeHGlobal(phPC);
            CloseHandle(pInfo.hProcess);
            CloseHandle(pInfo.hThread);

            _outPipeStream = new FileStream(new SafeFileHandle(hPipeRead, true), FileAccess.Read);
            _isRunning = true;
            _readThread = new Thread(ReadPtyOutput);
            _readThread.IsBackground = true;
            _readThread.Start();
            }

        private void ReadPtyOutput()
            {
            byte[] buffer = new byte[4096];
            try
                {
                while (_isRunning)
                    {
                    int bytesRead = _outPipeStream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string content = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    OnOutputReceived?.Invoke(content);
                    }
                }
            catch (Exception ex)
                {
                GeminiLogger.Error(ex, "Error reading ConPTY stream");
                }
            }

        public void Kill()
            {
            if (_process != null && !_process.HasExited)
                {
                try
                    {
                    Process.Start(new ProcessStartInfo("taskkill", $"/F /T /PID {_process.Id}")
                        { CreateNoWindow = true, UseShellExecute = false });
                    }
                catch (Exception ex) { GeminiLogger.Error(ex, "Kill failed"); }
                }
            Dispose();
            }

        public void Dispose()
            {
            _isRunning = false;
            if (_hPC != IntPtr.Zero) ClosePseudoConsole(_hPC);
            _outPipeStream?.Dispose();
            _hPC = IntPtr.Zero;
            }
        }
    }