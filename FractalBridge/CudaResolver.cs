using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace FractalBridge;

/// <summary>
/// Registers a platform‑specific DllImport resolver so that the generic library name "nvcuda"
/// resolves to the correct native CUDA driver library on Windows ("nvcuda.dll") and Linux ("libcuda.so").
/// This enables the existing P/Invoke signatures in CudaNative.cs to work without hard‑coding platform names.
/// </summary>
public static class CudaResolver
{
    static CudaResolver()
    {
        // Register the resolver for this assembly.
        NativeLibrary.SetDllImportResolver(typeof(CudaResolver).Assembly, ResolveCudaLibrary);
    }

    /// <summary>
    /// Forces initialization of the static constructor.
    /// </summary>
    public static void Init() { }

    private static IntPtr ResolveCudaLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        // Only intercept the generic "nvcuda" name used in the P/Invoke signatures.
        if (!string.Equals(libraryName, "nvcuda", StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero; // Let the default resolver handle other libraries.

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // On Windows the CUDA driver library is nvcuda.dll.
            return NativeLibrary.Load("nvcuda.dll", assembly, searchPath);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // On Linux the driver library is typically libcuda.so (often a symlink to the versioned .so).
            return NativeLibrary.Load("libcuda.so", assembly, searchPath);
        }
        else
        {
            // Unsupported OS – let the P/Invoke fail with a clear error.
            return IntPtr.Zero;
        }
    }
}
