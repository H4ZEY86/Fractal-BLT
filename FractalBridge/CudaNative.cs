using System;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace FractalBridge;

/// <summary>
/// Raw P/Invoke definitions for the CUDA Driver API.
/// Requires nvcuda.dll on Windows or libcuda.so on Linux.
/// </summary>
public static partial class CudaNative
{
    private const string CudaLib = "nvcuda"; // On Linux, use NativeLibrary.SetDllImportResolver to map to libcuda.so

    static CudaNative()
    {
        CudaResolver.Init();
    }

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

    // Convenience wrappers that invoke Check()
    public static void Init(uint flags) => Check(cuInit(flags));
    public static void DeviceGet(out int device, int ordinal) => Check(cuDeviceGet(out device, ordinal));
    public static void CtxCreate(out IntPtr pctx, uint flags, int dev) => Check(cuCtxCreate(out pctx, flags, dev));
    public static void StreamCreate(out IntPtr phStream, uint flags) => Check(cuStreamCreate(out phStream, flags));
    public static void MemHostRegister(IntPtr p, nuint bytesize, uint Flags) => Check(cuMemHostRegister(p, bytesize, Flags));
    public static void MemHostUnregister(IntPtr p) => Check(cuMemHostUnregister(p));
    public static void MemAlloc(out IntPtr dptr, nuint bytesize) => Check(cuMemAlloc(out dptr, bytesize));
    public static void MemFree(IntPtr dptr) => Check(cuMemFree(dptr));
    public static void StreamSynchronize(IntPtr hStream) => Check(cuStreamSynchronize(hStream));
    public static void MemcpyHtoDAsync(IntPtr dstDevice, IntPtr srcHost, nuint byteCount, IntPtr hStream) => Check(cuMemcpyHtoDAsync(dstDevice, srcHost, byteCount, hStream));

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
    public static partial CUresult cuMemcpyHtoDAsync(nint dstDevice, nint srcHost, nuint byteCount, nint hStream);

    [LibraryImport(CudaLib)]
    public static partial CUresult cuGetErrorString(CUresult error, out nint pStr);

    /// <summary>
    /// Evaluates a CUDA result code and throws a detailed CudaException if it indicates failure.
    /// </summary>
    /// <param name="result">The CUresult from a CUDA Driver API call.</param>
    public static void Check(CUresult result)
    {
        if (result != CUresult.CUDA_SUCCESS)
        {
            string errorMessage = "Unknown Error";
            if (cuGetErrorString(result, out nint pStr) == CUresult.CUDA_SUCCESS && pStr != 0)
            {
                errorMessage = Marshal.PtrToStringUTF8(pStr) ?? errorMessage;
            }
            throw new CudaException(result, errorMessage);
        }
    }
}
