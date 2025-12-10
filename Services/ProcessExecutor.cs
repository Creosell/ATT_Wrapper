using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Serilog;

namespace ATT_Wrapper.Services
    {
    public sealed class ProcessExecutor : IDisposable
        {
        private PseudoConsolePipe _inputPipe;
        private PseudoConsolePipe _outputPipe;
        private PseudoConsole _pseudoConsole;
        private Process _process;
        private FileStream _inputStream;
        private Task _outputTask;
        private Task _exitWaitTask;
        private CancellationTokenSource _cancellationTokenSource;

        public event Action<string> OnOutputReceived;
        public event EventHandler OnExited;

        public ProcessExecutor()
            {
            _cancellationTokenSource = new CancellationTokenSource();
            }

        public void Start(string command)
            {
            if (_process != null)
                throw new InvalidOperationException("Process is already running.");

            Log.Information($"Starting: {command}");

            // 1. Create pipes
            _inputPipe = new PseudoConsolePipe();
            _outputPipe = new PseudoConsolePipe();

            // 2. Create pseudoconsole
            _pseudoConsole = PseudoConsole.Create(_inputPipe.ReadSide, _outputPipe.WriteSide, 300, 40);

            // 3. Start process
            _process = ProcessFactory.Start(command, PseudoConsole.PseudoConsoleThreadAttribute, _pseudoConsole.Handle);
            Log.Information($"Process started, PID: {_process.ProcessInfo.dwProcessId}");

            // 4. Setup input stream (for SendInput)
            _inputStream = new FileStream(_inputPipe.WriteSide, FileAccess.Write);

            // 5. Start reading output - CRITICAL: Read raw bytes to avoid buffering
            _outputTask = Task.Run(() => ReadOutputLoop(_outputPipe.ReadSide, _cancellationTokenSource.Token));

            // 6. Start waiting for exit
            _exitWaitTask = Task.Run(() => WaitForExitLoop(_process));
            }

        public void SendInput(string input)
            {
            if (_inputStream == null) return;
            try
                {
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                _inputStream.Write(bytes, 0, bytes.Length);
                _inputStream.Flush();
                Log.Debug($"Sent: {input.Replace("\r", "\\r").Replace("\n", "\\n")}");
                }
            catch (Exception ex)
                {
                Log.Warning(ex, "SendInput error");
                }
            }

        public void Kill()
            {
            Log.Information("Kill() called");

            if (_process != null && _process.ProcessInfo.dwProcessId > 0)
                {
                try
                    {
                    // /F = Force (принудительно)
                    // /T = Tree (убить процесс И всех его потомков/детей)
                    // Это гарантирует, что если cmd.exe запустил python.exe, умрут ОБА.
                    var psi = new System.Diagnostics.ProcessStartInfo
                        {
                        FileName = "taskkill",
                        Arguments = $"/F /T /PID {_process.ProcessInfo.dwProcessId}",
                        CreateNoWindow = true,
                        UseShellExecute = false
                        };
                    System.Diagnostics.Process.Start(psi)?.WaitForExit(2000); // Ждем до 2 секунд
                    Log.Information($"Taskkill executed for PID: {_process.ProcessInfo.dwProcessId}");
                    }
                catch (Exception ex)
                    {
                    // Игнорируем ошибку, если процесс уже умер
                    Log.Warning(ex, "Failed to execute taskkill (process might be already dead)");
                    }
                }

            // После убийства чистим ресурсы
            Dispose();
            }

        // CRITICAL FIX: Read raw bytes without StreamReader to avoid buffering
        private void ReadOutputLoop(SafeFileHandle outputReadSide, CancellationToken token)
            {
            //Log.Debug("ReadOutputLoop started");

            // Use FileStream directly - NO StreamReader!
            using (var fs = new FileStream(outputReadSide, FileAccess.Read))
                {
                try
                    {
                    byte[] buffer = new byte[4096];
                    while (!token.IsCancellationRequested)
                        {
                        // Read bytes directly - this avoids StreamReader buffering
                        int bytesRead = fs.Read(buffer, 0, buffer.Length);

                        if (bytesRead == 0)
                            {
                            Log.Debug("Pipe closed");
                            break;
                            }

                        // Convert to string
                        string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                        //// Log it
                        //string logData = data.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\x1b", "\\x1b");
                        //if (logData.Length > 300)
                        //    {
                        //    Log.Debug($"Read {bytesRead} bytes: {logData.Substring(0, 300)}...");
                        //    }
                        //else
                        //    {
                        //    Log.Debug($"Read {bytesRead} bytes: {logData}");
                        //    }

                        // Fire event
                        OnOutputReceived?.Invoke(data);
                        }
                    }
                catch (IOException ex)
                    {
                    Log.Debug(ex, "Pipe broken");
                    }
                catch (ObjectDisposedException)
                    {
                    Log.Debug("Stream disposed");
                    }
                catch (Exception ex)
                    {
                    Log.Error(ex, "ReadOutput error");
                    }
                }

            Log.Debug("ReadOutputLoop finished");
            }

        private void WaitForExitLoop(Process process)
            {
            using (var waitHandle = new AutoResetEvent(false)
                {
                SafeWaitHandle = new SafeWaitHandle(process.ProcessInfo.hProcess, ownsHandle: false)
                })
                {
                waitHandle.WaitOne();
                }

            Log.Information("Process exited");
            OnExited?.Invoke(this, EventArgs.Empty);
            }

        public void Dispose()
            {
            Log.Debug("Dispose");

            _cancellationTokenSource?.Cancel();

            _inputStream?.Dispose();
            _inputStream = null;

            _process?.Dispose();
            _process = null;

            _pseudoConsole?.Dispose();
            _pseudoConsole = null;

            _inputPipe?.Dispose();
            _inputPipe = null;

            _outputPipe?.Dispose();
            _outputPipe = null;
            }
        }

    // Supporting classes below (unchanged)

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

            startupInfo.StartupInfo.dwFlags = (int)( Native.ProcessApi.STARTF_USESHOWWINDOW | Native.ProcessApi.STARTF_USESTDHANDLES );
            startupInfo.StartupInfo.wShowWindow = Native.ProcessApi.SW_HIDE;

            startupInfo.StartupInfo.hStdInput = IntPtr.Zero;
            startupInfo.StartupInfo.hStdOutput = IntPtr.Zero;
            startupInfo.StartupInfo.hStdError = IntPtr.Zero;

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
            internal const uint STARTF_USESTDHANDLES = 0x00000100;
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

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
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