using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Serilog;

namespace ATT_Wrapper.Services
    {
    public class ProcessExecutor : IDisposable
        {
        public event Action<string> OnOutputReceived;
        public event Action OnExited;
        public int CurrentPid => (int)_pi.dwProcessId;

        private IntPtr _hPc = IntPtr.Zero;
        private IntPtr _inputWriteHandle = IntPtr.Zero;
        private IntPtr _outputReadHandle = IntPtr.Zero;
        private SafeFileHandle _inputWriteSafeHandle = null;
        private SafeFileHandle _outputReadSafeHandle = null;
        private StreamWriter _inputWriter;
        private bool _disposed = false;
        private PROCESS_INFORMATION _pi;

        [StructLayout(LayoutKind.Sequential)]
        internal struct COORD
            {
            public short X;
            public short Y;
            }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_INFORMATION
            {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
            }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct STARTUPINFOEX
            {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
            }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct STARTUPINFO
            {
            public uint cb;
            public IntPtr lpReserved;
            public IntPtr lpDesktop;
            public IntPtr lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
            }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SECURITY_ATTRIBUTES
            {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
            }

        // CRITICAL FIX: Correct constant value for PseudoConsole attribute
        private const uint PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;
        private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        private const uint INFINITE = 0xFFFFFFFF;
        private const int ERROR_INSUFFICIENT_BUFFER = 122;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int CreatePseudoConsole(COORD size, IntPtr hInput, IntPtr hOutput, uint dwFlags, out IntPtr phPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, ref SECURITY_ATTRIBUTES lpPipeAttributes, uint nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, uint dwAttributeCount, uint dwFlags, ref IntPtr lpSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr Attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcessW(
            string lpApplicationName,
            StringBuilder lpCommandLine,  // Must be StringBuilder for modification
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern void ClosePseudoConsole(IntPtr hPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        public void Start(string scriptPath, string arguments)
            {
            if (_hPc != IntPtr.Zero)
                throw new InvalidOperationException("Process is already running.");

            var executionDir = Path.GetDirectoryName(scriptPath);

            IntPtr inputReadPipe = IntPtr.Zero;
            IntPtr inputWritePipe = IntPtr.Zero;
            IntPtr outputReadPipe = IntPtr.Zero;
            IntPtr outputWritePipe = IntPtr.Zero;
            IntPtr attributeList = IntPtr.Zero;
            IntPtr hPcValue = IntPtr.Zero;
            IntPtr envPtr = IntPtr.Zero;

            try
                {
                // Security attributes for pipes
                SECURITY_ATTRIBUTES sa = new SECURITY_ATTRIBUTES
                    {
                    nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
                    bInheritHandle = true,
                    lpSecurityDescriptor = IntPtr.Zero
                    };

                // Create pipes for input and output
                if (!CreatePipe(out inputReadPipe, out inputWritePipe, ref sa, 0))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create input pipe.");

                if (!CreatePipe(out outputReadPipe, out outputWritePipe, ref sa, 0))
                    {
                    CloseHandle(inputReadPipe);
                    CloseHandle(inputWritePipe);
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create output pipe.");
                    }

                // Store handles (we'll manage cleanup ourselves)
                _inputWriteHandle = inputWritePipe;
                _outputReadHandle = outputReadPipe;

                // Create pseudo console
                COORD consoleSize = new COORD { X = 250, Y = 9999 };
                int hresult = CreatePseudoConsole(consoleSize, inputReadPipe, outputWritePipe, 0, out _hPc);

                if (hresult != 0 || _hPc == IntPtr.Zero)
                    {
                    throw new Win32Exception(hresult, $"Failed to create pseudo console. HRESULT: 0x{hresult:X}");
                    }

                Log.Information($"Created pseudo console handle: 0x{_hPc.ToInt64():X}");

                // Close the pipe ends that the pseudoconsole now owns
                CloseHandle(inputReadPipe);
                inputReadPipe = IntPtr.Zero;
                CloseHandle(outputWritePipe);
                outputWritePipe = IntPtr.Zero;

                // Prepare startup info with attribute list
                IntPtr requiredSize = IntPtr.Zero;

                // Get required size for attribute list
                if (!InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref requiredSize))
                    {
                    int error = Marshal.GetLastWin32Error();
                    if (error != ERROR_INSUFFICIENT_BUFFER)
                        throw new Win32Exception(error, "Failed to get attribute list size");
                    }

                // Allocate and initialize attribute list
                attributeList = Marshal.AllocHGlobal(requiredSize);

                if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref requiredSize))
                    {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to initialize attribute list");
                    }

                // Allocate memory for the pseudoconsole handle and write it
                hPcValue = Marshal.AllocHGlobal(IntPtr.Size);
                Marshal.WriteIntPtr(hPcValue, _hPc);

                // Update the attribute list with the pseudoconsole handle
                if (!UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    hPcValue,
                    (IntPtr)IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero))
                    {
                    int error = Marshal.GetLastWin32Error();
                    throw new Win32Exception(error, $"Failed to update thread attribute. Error code: {error}");
                    }

                // Setup startup info
                STARTUPINFOEX siEx = new STARTUPINFOEX();
                siEx.StartupInfo.cb = (uint)Marshal.SizeOf<STARTUPINFOEX>();
                siEx.StartupInfo.dwFlags = 0;
                siEx.StartupInfo.wShowWindow = 0;
                siEx.StartupInfo.lpReserved = IntPtr.Zero;
                siEx.StartupInfo.lpDesktop = IntPtr.Zero;
                siEx.StartupInfo.lpTitle = IntPtr.Zero;
                siEx.StartupInfo.lpReserved2 = IntPtr.Zero;
                siEx.StartupInfo.cbReserved2 = 0;
                siEx.StartupInfo.hStdInput = IntPtr.Zero;
                siEx.StartupInfo.hStdOutput = IntPtr.Zero;
                siEx.StartupInfo.hStdError = IntPtr.Zero;
                siEx.lpAttributeList = attributeList;

                Log.Information($"STARTUPINFOEX size: {siEx.StartupInfo.cb}, AttributeList: 0x{attributeList.ToInt64():X}");

                // Build environment block
                var psi = new ProcessStartInfo();
                SetEnvironmentVariables(psi);

                StringBuilder envBlock = new StringBuilder();
                foreach (DictionaryEntry entry in psi.EnvironmentVariables)
                    {
                    envBlock.Append($"{entry.Key}={entry.Value}\0");
                    }
                envBlock.Append("\0");
                envPtr = Marshal.StringToHGlobalUni(envBlock.ToString());

                // CRITICAL FIX: Command line must be mutable for CreateProcessW
                // Try using application name and separate command line
                string applicationName = "cmd.exe";
                StringBuilder commandLineBuilder = new StringBuilder(32768);

                // Build command line - when using applicationName, don't repeat cmd.exe
                if (!string.IsNullOrEmpty(arguments))
                    {
                    commandLineBuilder.Append($"cmd.exe /c \"{scriptPath}\" {arguments}");
                    }
                else
                    {
                    commandLineBuilder.Append($"cmd.exe /c \"{scriptPath}\"");
                    }

                Log.Information($"Application: {applicationName}");
                Log.Information($"Command line: {commandLineBuilder}");
                Log.Information($"Working directory: {executionDir ?? Environment.CurrentDirectory}");

                // CRITICAL FIX: Set bInheritHandles to FALSE (the pseudoconsole handles inheritance)
                // Must combine EXTENDED_STARTUPINFO_PRESENT with CREATE_UNICODE_ENVIRONMENT
                if (!CreateProcessW(
                    null,  // Let the system find cmd.exe
                    commandLineBuilder,  // Pass StringBuilder directly
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,  // bInheritHandles = false for pseudoconsole
                    EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT,
                    envPtr,
                    executionDir ?? Environment.CurrentDirectory,
                    ref siEx,
                    out PROCESS_INFORMATION pi))
                    {
                    int error = Marshal.GetLastWin32Error();
                    throw new Win32Exception(error, $"Failed to create process. Error code: {error}");
                    }

                _pi = pi;

                Log.Information($"Started process PID: {pi.dwProcessId}");

                // Setup input writer using SafeFileHandle
                _inputWriteSafeHandle = new SafeFileHandle(_inputWriteHandle, ownsHandle: true);
                var inputStream = new FileStream(_inputWriteSafeHandle, FileAccess.Write, 4096, isAsync: false);
                _inputWriter = new StreamWriter(inputStream, Encoding.UTF8) { AutoFlush = true };

                // Setup output reader using SafeFileHandle
                _outputReadSafeHandle = new SafeFileHandle(_outputReadHandle, ownsHandle: true);
                var outputStream = new FileStream(_outputReadSafeHandle, FileAccess.Read, 4096, isAsync: false);
                StreamReader reader = new StreamReader(outputStream, Encoding.UTF8);

                // Start reading output
                Task.Run(() => ReadStreamAsync(reader));

                // Watch for process exit
                Task.Run(() =>
                {
                    WaitForSingleObject(pi.hProcess, INFINITE);
                    OnExited?.Invoke();
                    CloseHandle(pi.hProcess);
                    CloseHandle(pi.hThread);
                });
                }
            catch
                {
                // Cleanup on error
                if (inputReadPipe != IntPtr.Zero) CloseHandle(inputReadPipe);
                if (outputWritePipe != IntPtr.Zero) CloseHandle(outputWritePipe);

                if (_hPc != IntPtr.Zero)
                    {
                    ClosePseudoConsole(_hPc);
                    _hPc = IntPtr.Zero;
                    }

                throw;
                }
            finally
                {
                // Always cleanup temporary resources
                if (attributeList != IntPtr.Zero)
                    {
                    DeleteProcThreadAttributeList(attributeList);
                    Marshal.FreeHGlobal(attributeList);
                    }
                if (hPcValue != IntPtr.Zero)
                    {
                    Marshal.FreeHGlobal(hPcValue);
                    }
                if (envPtr != IntPtr.Zero)
                    {
                    Marshal.FreeHGlobal(envPtr);
                    }
                }
            }

        private void SetEnvironmentVariables(ProcessStartInfo psi)
            {
            psi.EnvironmentVariables["JATLAS_LOG_LEVEL"] = "INFO";
            psi.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
            psi.EnvironmentVariables["FORCE_COLOR"] = "1";
            psi.EnvironmentVariables["CLICOLOR_FORCE"] = "1";
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            psi.EnvironmentVariables["TERM"] = "xterm-256color";
            psi.EnvironmentVariables["NO_COLOR"] = "0";
            psi.EnvironmentVariables["COLUMNS"] = "250";
            psi.EnvironmentVariables["WIDTH"] = "250";
            }

        public void SendInput(string input)
            {
            try
                {
                _inputWriter?.WriteLine(input);
                }
            catch (Exception ex)
                {
                Log.Warning(ex, "Failed to send input");
                }
            }

        public void Kill()
            {
            if (_pi.dwProcessId == 0) return;

            try
                {
                Process.Start(new ProcessStartInfo("taskkill", $"/F /T /PID {CurrentPid}")
                    {
                    CreateNoWindow = true,
                    UseShellExecute = false
                    });
                }
            catch (Exception ex)
                {
                Log.Error(ex, "Kill failed");
                }
            }

        private async Task ReadStreamAsync(StreamReader reader)
            {
            char[] buffer = new char[1024];
            StringBuilder lineBuffer = new StringBuilder();

            try
                {
                while (true)
                    {
                    int charsRead = await reader.ReadAsync(buffer, 0, buffer.Length);
                    if (charsRead == 0) break;

                    lineBuffer.Append(buffer, 0, charsRead);
                    string content = lineBuffer.ToString();

                    int splitIndex;
                    char[] separators = { '\n', '\r' };

                    while (( splitIndex = content.IndexOfAny(separators) ) >= 0)
                        {
                        string line = content.Substring(0, splitIndex);

                        if (line.Length > 0)
                            {
                            OnOutputReceived?.Invoke(line);
                            }

                        int nextCharIdx = splitIndex + 1;
                        if (nextCharIdx < content.Length &&
                            ( ( content[splitIndex] == '\r' && content[nextCharIdx] == '\n' ) ||
                             ( content[splitIndex] == '\n' && content[nextCharIdx] == '\r' ) ))
                            {
                            nextCharIdx++;
                            }
                        content = content.Substring(nextCharIdx);
                        }

                    lineBuffer.Clear();
                    lineBuffer.Append(content);
                    }

                // Output any remaining content
                if (lineBuffer.Length > 0)
                    {
                    OnOutputReceived?.Invoke(lineBuffer.ToString());
                    }
                }
            catch (Exception ex)
                {
                Log.Debug($"Stream read ended: {ex.Message}");
                }
            }

        public void Dispose()
            {
            if (_disposed) return;
            _disposed = true;

            try
                {
                // Close the input writer (which will dispose the SafeFileHandle)
                _inputWriter?.Dispose();
                _inputWriter = null;

                // Dispose SafeFileHandles (they'll close the native handles)
                _inputWriteSafeHandle?.Dispose();
                _inputWriteSafeHandle = null;

                _outputReadSafeHandle?.Dispose();
                _outputReadSafeHandle = null;

                // Close the pseudoconsole
                if (_hPc != IntPtr.Zero)
                    {
                    ClosePseudoConsole(_hPc);
                    _hPc = IntPtr.Zero;
                    }

                // Close process handles
                if (_pi.hProcess != IntPtr.Zero)
                    {
                    CloseHandle(_pi.hProcess);
                    _pi.hProcess = IntPtr.Zero;
                    }

                if (_pi.hThread != IntPtr.Zero)
                    {
                    CloseHandle(_pi.hThread);
                    _pi.hThread = IntPtr.Zero;
                    }
                }
            catch (Exception ex)
                {
                Log.Error(ex, "Error during ProcessExecutor disposal");
                }
            }
        }
    }