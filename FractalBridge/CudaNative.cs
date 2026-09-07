using System;
using System.Runtime.InteropServices;

namespace FractalBridge;

/// <summary>
/// Raw P/Invoke definitions for the CUDA Driver API.
/// Requires nvcuda.dll on Windows or libcuda.so on Linux.
/// </summary>
public static partial class CudaNative
{
    private const string CudaLib = "nvcuda"; // On Linux, use NativeLibrary.SetDllImportResolver to map to libcuda.so

    public enum CUresult
    {
        CUDA_SUCCESS = 0,
        // (Other error codes omitted for scaffolding brevity)
    }

    [LibraryImport(CudaLib)]
    public static partial CUresult cuInit(uint flags);

    [LibraryImport(CudaLib)]
    public static partial CUresult cuDeviceGet(out int device, int ordinal);

    [LibraryImport(CudaLib)]
    public static partial CUresult cuCtxCreate(out IntPtr pctx, uint flags, int dev);

    [LibraryImport(CudaLib)]
    public static partial CUresult cuMemAlloc(out IntPtr dptr, nuint bytesize);

    [LibraryImport(CudaLib)]
    public static partial CUresult cuMemFree(IntPtr dptr);

    [LibraryImport(CudaLib)]
    public static partial CUresult cuMemHostRegister(IntPtr p, nuint bytesize, uint Flags);

    [LibraryImport(CudaLib)]
    public static partial CUresult cuMemHostUnregister(IntPtr p);

    [LibraryImport(CudaLib)]
    public static partial CUresult cuStreamCreate(out IntPtr phStream, uint flags);

    [LibraryImport(CudaLib)]
    public static partial CUresult cuStreamSynchronize(IntPtr hStream);

    [LibraryImport(CudaLib)]
    public static partial CUresult cuMemcpyHtoDAsync(IntPtr dstDevice, IntPtr srcHost, nuint ByteCount, IntPtr hStream);
}
