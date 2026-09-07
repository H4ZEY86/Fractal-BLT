using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FractalStreamer;

/// <summary>
/// Provides cross-platform P/Invoke wrappers to open unbuffered file handles,
/// bypassing the OS page cache (FILE_FLAG_NO_BUFFERING on Windows, O_DIRECT on Linux).
/// </summary>
public static class UnbufferedFile
{
    // Windows constants
    private const uint GENERIC_READ = 0x80000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
    private const uint FILE_FLAG_OVERLAPPED = 0x40000000;

    // Linux constants
    private const int O_RDONLY = 0x0000;
    private const int O_DIRECT = 0x4000; // Architecture dependent, typically 0x4000

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("libc", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern SafeFileHandle open(string pathname, int flags);

    /// <summary>
    /// Opens a file with unbuffered I/O flags.
    /// </summary>
    public static SafeFileHandle OpenUnbuffered(string filePath)
    {
        if (OperatingSystem.IsWindows())
        {
            var handle = CreateFile(
                filePath,
                GENERIC_READ,
                FILE_SHARE_READ,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_FLAG_NO_BUFFERING | FILE_FLAG_OVERLAPPED,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }
            return handle;
        }
        else if (OperatingSystem.IsLinux())
        {
            var handle = open(filePath, O_RDONLY | O_DIRECT);
            if (handle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                throw new IOException($"Failed to open file with O_DIRECT. Error code: {error}");
            }
            return handle;
        }
        else
        {
            throw new PlatformNotSupportedException("Unbuffered I/O is only implemented for Windows and Linux.");
        }
    }
}
