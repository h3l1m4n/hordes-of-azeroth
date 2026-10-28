using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace HordesOfAzeroth.Launcher
{
    // Wine Mono's Process.GetProcessesByName can omit running Windows processes.
    static class GameProcessSnapshot
    {
        const uint SnapshotProcesses = 2;
        const int NoMoreFiles = 18;
        static readonly IntPtr InvalidHandle = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct ProcessEntry
        {
            public uint Size, Usage, ProcessId;
            public IntPtr DefaultHeapId;
            public uint ModuleId, Threads, ParentProcessId;
            public int PriorityClassBase;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string ExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr handle);

        public static bool Contains(string exeName)
        {
            IntPtr snapshot = CreateToolhelp32Snapshot(SnapshotProcesses, 0);
            if (snapshot == InvalidHandle)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf(typeof(ProcessEntry)) };
                bool found = Process32FirstW(snapshot, ref entry);
                while (found)
                {
                    if (string.Equals(entry.ExeFile, exeName, StringComparison.OrdinalIgnoreCase))
                        return true;
                    found = Process32NextW(snapshot, ref entry);
                }
                int error = Marshal.GetLastWin32Error();
                if (error != NoMoreFiles)
                    throw new Win32Exception(error);
                return false;
            }
            finally
            {
                CloseHandle(snapshot);
            }
        }
    }
}
