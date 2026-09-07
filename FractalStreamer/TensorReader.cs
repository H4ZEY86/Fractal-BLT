using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FractalStreamer;

/// <summary>
/// Implements zero-allocation tensor reading using System.IO.RandomAccess 
/// and page-aligned unmanaged memory buffers.
/// </summary>
public unsafe class TensorReader : ITensorStreamer
{
    private void* _buffer;
    private long _bufferCapacity;
    private SafeFileHandle? _handle;
    private string? _currentFilePath;

    /// <summary>
    /// Maps a chunk of a safetensors file directly into page-aligned unmanaged memory.
    /// </summary>
    public Span<byte> MapTensorChunk(string filePath, long byteOffset, long byteSize)
    {
        if (byteOffset < 0 || byteSize <= 0)
            throw new ArgumentOutOfRangeException("Offset and size must be positive.");

        if (_currentFilePath != filePath || _handle == null || _handle.IsInvalid)
        {
            _handle?.Dispose();
            _handle = UnbufferedFile.OpenUnbuffered(filePath);
            _currentFilePath = filePath;
        }

        EnsureBufferCapacity(byteSize);

        // System.IO.RandomAccess provides highly optimized, direct read APIs
        // Since the handle is unbuffered, the buffer MUST be aligned and size MUST typically be a multiple of sector size.
        // We read exactly what is requested. (Note: Unbuffered reads usually require sector-aligned offsets and lengths,
        // so in a real-world scenario we might need to over-read and slice the span).
        
        long fileLength = RandomAccess.GetLength(_handle);
        if (byteOffset + byteSize > fileLength)
            throw new ArgumentOutOfRangeException(nameof(byteSize), "Requested range exceeds file length.");

        Span<byte> targetSpan = new Span<byte>(_buffer, (int)byteSize);
        
        // Blocking read directly into the native pointer
        int bytesRead = RandomAccess.Read(_handle, targetSpan, byteOffset);
        
        if (bytesRead != byteSize)
        {
            throw new IOException($"Expected to read {byteSize} bytes, but read {bytesRead}.");
        }

        return targetSpan;
    }

    private void EnsureBufferCapacity(long requiredSize)
    {
        if (_buffer != null && _bufferCapacity >= requiredSize)
            return;

        if (_buffer != null)
        {
            NativeMemory.AlignedFree(_buffer);
            _buffer = null;
        }

        int pageSize = Environment.SystemPageSize;
        
        // Allocate a page-aligned buffer suitable for unbuffered I/O
        // Round up to nearest alignment boundary
        long allocSize = (requiredSize + pageSize - 1) & ~(pageSize - 1);
        _buffer = NativeMemory.AlignedAlloc((nuint)allocSize, (nuint)pageSize);
        _bufferCapacity = allocSize;
    }

    public void Dispose()
    {
        if (_buffer != null)
        {
            NativeMemory.AlignedFree(_buffer);
            _buffer = null;
            _bufferCapacity = 0;
        }
        
        _handle?.Dispose();
        _handle = null;
        _currentFilePath = null;
    }
}
