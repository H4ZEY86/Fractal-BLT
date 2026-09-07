using System;

namespace FractalBridge;

/// <summary>
/// Exception thrown when a CUDA Driver API call fails.
/// </summary>
public class CudaException : Exception
{
    public CudaNative.CUresult ErrorCode { get; }

    public CudaException(CudaNative.CUresult errorCode, string message) 
        : base($"CUDA Error {errorCode}: {message}")
    {
        ErrorCode = errorCode;
    }
}
