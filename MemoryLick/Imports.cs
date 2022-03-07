using System;
using System.Runtime.InteropServices;

namespace MemoryLick
{
#if OS_WINDOWS
    public class Imports
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(UInt32 dwDesiredAccess, Int32 bInheritHandle, UInt32 dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern Int32 CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern Int32 VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize,
            uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern Int32 ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [In, Out] byte[] buffer,
            UInt32 size, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            byte[] lpBuffer,
            Int32 nSize,
            out IntPtr lpNumberOfBytesWritten);
    }
    #else
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct iovec
    {
        public void* iov_base;
        public int iov_len;
    }
    public class Imports
    {
        [DllImport("libc", SetLastError = true)]
        public static extern unsafe int process_vm_writev(int pid,
            iovec* local_iov,
            ulong liovcnt,
            iovec* remote_iov,
            ulong riovcnt,
            ulong flags);
        
        [DllImport("libc")]
        public static extern unsafe int process_vm_readv(int pid,
            iovec* local_iov,
            ulong liovcnt,
            iovec* remote_iov,
            ulong riovcnt,
            ulong flags);
    }
#endif
}