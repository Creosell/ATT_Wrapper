using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ATT_Wrapper.Services
    {
    public class PseudoConsole : IDisposable
        {
        public IntPtr Handle { get; }
        public SafeFileHandle InputWriteSide { get; }
        public SafeFileHandle OutputReadSide { get; }

        public PseudoConsole(int width, int height)
            {
            CreatePipe(out var inputRead, out var inputWrite);
            CreatePipe(out var outputRead, out var outputWrite);

            InputWriteSide = inputWrite;
            OutputReadSide = outputRead;

            var size = new COORD { X = (short)width, Y = (short)height };
            int result = CreatePseudoConsole(size, inputRead.DangerousGetHandle(), outputWrite.DangerousGetHandle(), 0, out var hPC);

            if (result != 0) throw new InvalidOperationException($"Could not create PseudoConsole. Error Code: {result}");

            Handle = hPC;

            inputRead.Dispose();
            outputWrite.Dispose();
            }

        public void Dispose()
            {
            if (Handle != IntPtr.Zero) ClosePseudoConsole(Handle);
            InputWriteSide?.Dispose();
            OutputReadSide?.Dispose();
            }

        private void CreatePipe(out SafeFileHandle readSide, out SafeFileHandle writeSide)
            {
            if (!CreatePipe(out var hRead, out var hWrite, IntPtr.Zero, 0))
                throw new InvalidOperationException("Failed to create pipe");

            readSide = new SafeFileHandle(hRead, true);
            writeSide = new SafeFileHandle(hWrite, true);
            }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, IntPtr lpPipeAttributes, uint nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int CreatePseudoConsole(COORD size, IntPtr hInput, IntPtr hOutput, uint dwFlags, out IntPtr phPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern void ClosePseudoConsole(IntPtr hPC);

        [StructLayout(LayoutKind.Sequential)]
        private struct COORD { public short X; public short Y; }
        }

    public static class PseudoConsoleLauncher
        {
        private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        private const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;

        public static ProcessInformation StartProcessInConPty(PseudoConsole conPty, string command, string arguments)
            {
            var startupInfo = new STARTUPINFOEX();
            startupInfo.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();

            var lpSize = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);

            startupInfo.lpAttributeList = Marshal.AllocHGlobal(lpSize);
            InitializeProcThreadAttributeList(startupInfo.lpAttributeList, 1, 0, ref lpSize);

            // --- ИСПРАВЛЕНИЕ: Передаем указатель на хендл, а не сам хендл ---
            IntPtr ptrHandle = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(ptrHandle, conPty.Handle);

            try
                {
                UpdateProcThreadAttribute(
                    startupInfo.lpAttributeList,
                    0,
                    (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    ptrHandle, // <-- УКАЗАТЕЛЬ
                    (IntPtr)IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero
                );

                var processInfo = new ProcessInformation();
                var cmdLine = $"cmd.exe /c \"{command}\" {arguments}";

                bool success = CreateProcess(
                    null, cmdLine, IntPtr.Zero, IntPtr.Zero, false,
                    EXTENDED_STARTUPINFO_PRESENT, IntPtr.Zero, null,
                    ref startupInfo, out processInfo);

                if (!success) throw new InvalidOperationException($"Could not create process. Error: {Marshal.GetLastWin32Error()}");

                return processInfo;
                }
            finally
                {
                // Освобождаем память
                DeleteProcThreadAttributeList(startupInfo.lpAttributeList);
                Marshal.FreeHGlobal(startupInfo.lpAttributeList);
                Marshal.FreeHGlobal(ptrHandle);
                }
            }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFOEX
            {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
            }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
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
        public struct ProcessInformation
            {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
            }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcess(
            string lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags,
            IntPtr lpEnvironment, string lpCurrentDirectory, ref STARTUPINFOEX lpStartupInfo,
            out ProcessInformation lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr Attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        public static void WaitForExit(IntPtr hProcess)
            {
            WaitForSingleObject(hProcess, 0xFFFFFFFF);
            }
        }
    }